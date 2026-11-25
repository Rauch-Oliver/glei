// Shared by the app and the widget: models, API calls and display rules.
import SwiftUI
import WidgetKit

/// The glei API (Cloud Run service "glei").
let apiBase = URL(string: "https://glei-739185299212.europe-west1.run.app/api/")!

/// Petrol from the glei icon.
let gleiColor = Color(red: 0.08, green: 0.54, blue: 0.48)

struct Stop: Codable, Hashable, Identifiable {
    let id: String      // "at:46:3171"
    let name: String    // "Graz Krenngasse"
}

/// Times are UTC. The API's `minutes` is ignored: it's stale by the time it's shown.
struct Departure: Codable, Hashable {
    let line: String
    let destination: String
    let plannedTime: Date
    let estimatedTime: Date?
    let mode: String?
    let cancelled: Bool
    let direction: String?   // "outward" / "inward"

    var time: Date { estimatedTime ?? plannedTime }

    func minutes(from now: Date) -> Int { max(0, Int((time.timeIntervalSince(now) / 60).rounded())) }
}

/// A pinned stop, optionally one line in one direction. Each widget shows one pin.
struct Pin: Codable, Hashable, Identifiable {
    let stop: Stop
    var line: String?
    var direction: String?
    var label: String?   // main destination of the direction, e.g. "Andritz"

    /// Without a direction from TRIAS, a line is split by destination (see `lineGroups`), so the label identifies it.
    var id: String { "\(stop.id)|\(line ?? "")|\(direction ?? label ?? "")" }
    var title: String { line.map { "\(lineName($0)) → \(shortName(label ?? ""))" } ?? "Alle Linien" }

    func departures() async throws -> [Departure] {
        let all = try await API.departures(stop.id, line: line, direction: direction)
        return line != nil && direction == nil ? all.filter { $0.destination == label } : all
    }
}

/// Pins live in the App Group so the widget can read them.
nonisolated(unsafe) let sharedDefaults = UserDefaults(suiteName: "group.at.glei.app")!   // UserDefaults is thread-safe
let pinsKey = "pins"

func loadPins() -> [Pin] {
    (try? JSONDecoder().decode([Pin].self, from: sharedDefaults.data(forKey: pinsKey) ?? Data())) ?? []
}

enum API {
    static func stops(_ query: String) async throws -> [Stop] {
        try await get(apiBase.appending(path: "stops/search").appending(queryItems: [.init(name: "query", value: query)]))
    }

    static func departures(_ stopId: String, line: String? = nil, direction: String? = nil, limit: Int = 100) async throws -> [Departure] {
        var query = [URLQueryItem(name: "limit", value: "\(limit)")]
        if let line { query.append(.init(name: "line", value: line)) }
        if let direction { query.append(.init(name: "direction", value: direction)) }
        return try await get(apiBase.appending(path: "departures").appending(path: stopId).appending(queryItems: query))
    }

    private static func get<T: Decodable>(_ url: URL) async throws -> T {
        let (data, response) = try await URLSession.shared.data(from: url)
        guard (response as? HTTPURLResponse)?.statusCode == 200 else { throw URLError(.badServerResponse) }
        let decoder = JSONDecoder()
        decoder.dateDecodingStrategy = .custom { decoder in   // .iso8601 rejects fractional seconds
            let text = try decoder.singleValueContainer().decode(String.self)
            guard let date = (try? Date(text, strategy: .iso8601))
                    ?? (try? Date.ISO8601FormatStyle(includingFractionalSeconds: true).parse(text)) else {
                throw DecodingError.dataCorrupted(.init(codingPath: decoder.codingPath, debugDescription: "Bad date: \(text)"))
            }
            return date
        }
        return try decoder.decode(T.self, from: data)
    }
}

// MARK: - Display rules (same as the other glei clients)

/// Running departures not yet gone (they stay 30 s as "jetzt"), by time.
func upcoming(_ departures: [Departure], now: Date) -> [Departure] {
    departures.filter { !$0.cancelled && $0.time > now.addingTimeInterval(-30) }.sorted { $0.time < $1.time }
}

/// One line in one direction, labelled with its most frequent destination.
struct LineGroup: Identifiable, Hashable {
    let id: String
    let line: String
    let mode: String?
    let direction: String?
    let label: String
    let next: [Departure]
}

/// Departures grouped by line and direction, lines in natural order (3, 23, 64, 64E, N5).
func lineGroups(_ departures: [Departure], now: Date) -> [LineGroup] {
    Dictionary(grouping: upcoming(departures, now: now)) { "\($0.line)|\($0.direction ?? $0.destination)" }
        .map { key, list in
            let label = Dictionary(grouping: list, by: \.destination)
                .max { ($0.value.count, $1.value[0].time) < ($1.value.count, $0.value[0].time) }!.key
            return LineGroup(id: key, line: list[0].line, mode: list[0].mode, direction: list[0].direction,
                             label: label, next: Array(list.prefix(3)))
        }
        .sorted { a, b in
            let order = a.line.compare(b.line, options: .numeric)
            return order == .orderedSame ? a.label < b.label : order == .orderedAscending
        }
}

/// "3er" for numeric lines (how Graz says it), "Linie N5" otherwise.
func lineName(_ line: String) -> String { line.allSatisfy(\.isNumber) ? "\(line)er" : "Linie \(line)" }

/// "Graz Krenngasse" → "Krenngasse".
func shortName(_ name: String) -> String { name.hasPrefix("Graz ") ? String(name.dropFirst(5)) : name }

/// "jetzt · 7 · 15 min"
func minutesText(_ departures: [Departure], now: Date) -> String {
    let parts = departures.map { $0.minutes(from: now) }.map { $0 == 0 ? "jetzt" : "\($0)" }
    return parts.joined(separator: " · ") + (parts.last == "jetzt" ? "" : " min")
}

/// "3  Andritz      4 min"; without the destination when the card or widget is for one line anyway.
struct DepartureRow: View {
    let departure: Departure
    let now: Date
    var showsDestination = true
    var compact = false

    var body: some View {
        HStack {
            LineBadge(line: departure.line, mode: departure.mode, compact: compact)
            if showsDestination { Text(shortName(departure.destination)).lineLimit(1) }
            Spacer(minLength: 0)
            Text(minutesText([departure], now: now)).bold().monospacedDigit()
        }
        .accessibilityElement(children: .combine)
    }
}

/// Line number like on the Graz stop signs: trams plain, buses boxed, night lines inverted.
struct LineBadge: View {
    let line: String
    let mode: String?
    /// Smaller for widgets, so 3 rows fit inside the standard 16 pt margins.
    var compact = false
    /// Tinted or clear Home Screen: no fills, they'd turn into solid blocks.
    @Environment(\.widgetRenderingMode) private var renderingMode

    var body: some View {
        let night = line.hasPrefix("N")
        let filled = night && renderingMode == .fullColor
        Text(line)
            .font((compact ? Font.caption : .subheadline).weight(.heavy))
            .foregroundStyle(filled ? .white : .primary)
            .frame(minWidth: compact ? 28 : 34, minHeight: compact ? 18 : 22)
            .background(filled ? Color.blue : .clear, in: .rect(cornerRadius: 4))
            .overlay { if mode != "tram" && !filled { RoundedRectangle(cornerRadius: 4).stroke(lineWidth: 1.5) } }
            .accessibilityLabel(lineName(line))
    }
}
