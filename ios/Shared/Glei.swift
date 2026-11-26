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

    /// Fetches the pin. On failure the last answer stays, marked offline (like the other glei clients).
    /// nil when cancelled: that's not a failure, and nothing is cached.
    func board() async -> Board? {
        do {
            let departures = try await departures()
            try Task.checkCancellation()
            let board = Board(departures: departures, fetchedAt: .now)
            if loadPins().contains(where: { $0.id == id }) { BoardCache.save(board, for: self) }   // not if removed meanwhile
            return board
        } catch {
            if Task.isCancelled { return nil }
            var stale = BoardCache.load(self) ?? Board(departures: [], fetchedAt: nil)
            stale.offline = true
            return stale
        }
    }
}

/// The last answer for a pin. `fetchedAt == nil`: never loaded ("Keine Verbindung").
struct Board: Codable, Hashable {
    var departures: [Departure]
    var fetchedAt: Date?
    var offline = false

    /// "Stand 19:39" or "offline · Stand 19:39", as on Windows, Android and the web.
    func status(now: Date = .now) -> String? { fetchedAt == nil ? nil : combinedStatus([self], now: now) }
}

/// The status line for boards fetched together. Any offline: "offline · Stand" with the oldest data shown
/// (just "offline" without any); otherwise "Stand" of the latest fetch. Older than today with "gestern" or the date.
func combinedStatus(_ boards: [Board], now: Date = .now) -> String? {
    func stand(_ date: Date) -> String { "Stand \(standTime(date, now: now))" }
    let offline = boards.filter(\.offline)
    if !offline.isEmpty { return offline.compactMap(\.fetchedAt).min().map { "offline · \(stand($0))" } ?? "offline" }
    return boards.compactMap(\.fetchedAt).max().map(stand)
}

/// Last good answer per pin in the App Group: the widget shows what the app just loaded, and keeps it when offline.
enum BoardCache {
    private static func key(_ pin: Pin) -> String { "board.\(pin.id)" }

    static func load(_ pin: Pin) -> Board? {
        sharedDefaults.data(forKey: key(pin)).flatMap { try? JSONDecoder().decode(Board.self, from: $0) }
    }

    static func save(_ board: Board, for pin: Pin) {
        sharedDefaults.set(try? JSONEncoder().encode(board), forKey: key(pin))
    }

    static func remove(_ pin: Pin) { sharedDefaults.removeObject(forKey: key(pin)) }
}

/// "19:39", "gestern 22:15", or "24. Nov., 22:15".
func standTime(_ date: Date, now: Date) -> String {
    let calendar = Calendar.current
    let time = date.formatted(date: .omitted, time: .shortened)
    if calendar.isDate(date, inSameDayAs: now) { return time }
    if let yesterday = calendar.date(byAdding: .day, value: -1, to: now), calendar.isDate(date, inSameDayAs: yesterday) {
        return "gestern \(time)"
    }
    return date.formatted(.dateTime.day().month().hour().minute())
}

// MARK: - Refresh rules (same as the other glei clients)

/// When to fetch again: after `interval`, or earlier once fewer than 3 of the fetched departures are left
/// (when the API had 3 or fewer: once the first is gone). Never sooner than `minimum`.
func nextFetch(_ board: Board, now: Date, interval: TimeInterval, minimum: TimeInterval) -> Date {
    let next = upcoming(board.departures, now: now)
    let refill = (next.count > 3 ? next[next.count - 3] : next.first)?.time.addingTimeInterval(30) ?? .distantFuture
    return max(now.addingTimeInterval(minimum), min(now.addingTimeInterval(interval), refill))
}

/// Wait after the n-th failed fetch in a row: 1, 2, then 5 minutes (Windows/Linux backoff).
func retryDelay(afterErrors errors: Int) -> TimeInterval { [60, 120, 300][min(max(errors, 1), 3) - 1] }

/// Pins live in the App Group so the widget can read them.
nonisolated(unsafe) let sharedDefaults = UserDefaults(suiteName: "group.at.glei.app")!   // UserDefaults is thread-safe
let pinsKey = "pins"

func loadPins() -> [Pin] {
    (try? JSONDecoder().decode([Pin].self, from: sharedDefaults.data(forKey: pinsKey) ?? Data())) ?? []
}

enum API {
    static func stops(_ query: String) async throws -> [Stop] {
        let url = apiBase.appending(path: "stops/search").appending(queryItems: [.init(name: "query", value: query)])
        // URLQueryItem leaves "+" as is, which the server reads as a space ("P+R" → "P R").
        return try await get(URL(string: url.absoluteString.replacingOccurrences(of: "+", with: "%2B"))!)
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
        return try decoder.decode(T.self, from: data)
    }

    /// ISO 8601 dates, with or without fractional seconds (`.iso8601` rejects those).
    static var decoder: JSONDecoder {
        let decoder = JSONDecoder()
        decoder.dateDecodingStrategy = .custom { decoder in
            let text = try decoder.singleValueContainer().decode(String.self)
            guard let date = (try? Date(text, strategy: .iso8601))
                    ?? (try? Date.ISO8601FormatStyle(includingFractionalSeconds: true).parse(text)) else {
                throw DecodingError.dataCorrupted(.init(codingPath: decoder.codingPath, debugDescription: "Bad date: \(text)"))
            }
            return date
        }
        return decoder
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

/// The next 3 departures of a pin, shared by app cards and widgets so both look and behave the same.
/// Not loaded yet: placeholder rows (no spinner; the pull to refresh indicator is the only one).
/// Offline: the last departures greyed out.
struct BoardView: View {
    let pin: Pin
    let board: Board?
    let now: Date
    var compact = false

    var body: some View {
        if let board, board.fetchedAt != nil {
            let next = upcoming(board.departures, now: now).prefix(3)
            Group {
                if next.isEmpty { Text("Keine Abfahrten").foregroundStyle(.secondary) }
                ForEach(Array(next.enumerated()), id: \.offset) {   // two departures can be identical
                    DepartureRow(departure: $0.element, now: now, showsDestination: pin.line == nil, compact: compact)
                }
            }
            .opacity(board.offline ? 0.5 : 1)
        } else if board != nil {
            Text("Keine Verbindung").foregroundStyle(.secondary)
        } else {
            ForEach(0..<3, id: \.self) { _ in
                DepartureRow(departure: .placeholder(at: now), now: now, showsDestination: pin.line == nil, compact: compact)
            }
            .redacted(reason: .placeholder)
            .accessibilityHidden(true)
        }
    }
}

extension Departure {
    /// Stand-in for a row while loading, shown redacted.
    static func placeholder(at now: Date) -> Departure {
        Departure(line: "00", destination: "Haltestelle", plannedTime: now.addingTimeInterval(10 * 60),
                  estimatedTime: nil, mode: "tram", cancelled: false, direction: nil)
    }
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
