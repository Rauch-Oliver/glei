import AppIntents
import SwiftUI
import WidgetKit

// Each widget shows one of the pins from the app, chosen under "Widget bearbeiten".

struct PinEntity: AppEntity {
    static let typeDisplayRepresentation: TypeDisplayRepresentation = "Haltestelle"
    static let defaultQuery = PinQuery()

    let pin: Pin
    var id: String { pin.id }

    var displayRepresentation: DisplayRepresentation {
        DisplayRepresentation(title: "\(shortName(pin.stop.name))", subtitle: "\(pin.title)")
    }
}

struct PinQuery: EntityQuery {
    func entities(for ids: [String]) async throws -> [PinEntity] {
        loadPins().filter { ids.contains($0.id) }.map(PinEntity.init)
    }

    func suggestedEntities() async throws -> [PinEntity] { loadPins().map(PinEntity.init) }

    /// A new widget shows the first pin right away.
    func defaultResult() async -> PinEntity? { loadPins().first.map(PinEntity.init) }
}

struct ConfigIntent: WidgetConfigurationIntent {
    static let title: LocalizedStringResource = "Abfahrten"

    @Parameter(title: "Haltestelle") var pin: PinEntity?
}

// MARK: - Timeline

struct Entry: TimelineEntry {
    let date: Date
    let pin: Pin?
    let departures: [Departure]?   // nil = loading failed
    var fetchedAt = Date.now
}

/// Fetches once, then one entry per minute in which the minutes are recomputed. Fetches again after 15 min,
/// or earlier when only 2 of the fetched departures are left (the API only looks 20 departures ahead per stop).
struct Provider: AppIntentTimelineProvider {
    func placeholder(in context: Context) -> Entry { Entry(date: .now, pin: nil, departures: []) }

    func snapshot(for config: ConfigIntent, in context: Context) async -> Entry {
        await timeline(for: config, in: context).entries[0]
    }

    func timeline(for config: ConfigIntent, in context: Context) async -> Timeline<Entry> {
        guard let pin = config.pin?.pin else { return Timeline(entries: [placeholder(in: context)], policy: .never) }
        guard let departures = try? await pin.departures() else {
            let failed = Entry(date: .now, pin: pin, departures: nil)
            return Timeline(entries: [failed], policy: .after(.now.addingTimeInterval(5 * 60)))
        }
        let next = upcoming(departures, now: .now)
        let refill = next.count > 3 ? next[next.count - 3].time.addingTimeInterval(30) : .distantFuture
        let reload = max(.now.addingTimeInterval(60), min(.now.addingTimeInterval(15 * 60), refill))
        let entries = stride(from: Date.now, to: reload, by: 60).map { Entry(date: $0, pin: pin, departures: departures) }
        return Timeline(entries: entries, policy: .after(reload))
    }
}

// MARK: - View

struct WidgetView: View {
    let entry: Entry

    var body: some View {
        VStack(alignment: .leading, spacing: 6) {
            if let pin = entry.pin {
                Text(shortName(pin.stop.name)).font(.headline).lineLimit(1).widgetAccentable()
                Text(pin.title).font(.caption).foregroundStyle(.secondary).lineLimit(1)
                if let departures = entry.departures {
                    let next = upcoming(departures, now: entry.date).prefix(3)
                    if next.isEmpty { Text("Keine Abfahrten").font(.caption) }
                    ForEach(Array(next), id: \.self) {
                        DepartureRow(departure: $0, now: entry.date, showsDestination: pin.line == nil).font(.caption)
                    }
                    Spacer(minLength: 0)
                    Text("Stand \(entry.fetchedAt, style: .time)").font(.caption2).foregroundStyle(.secondary)
                } else {
                    Text("Keine Verbindung").font(.caption).foregroundStyle(.secondary)
                }
            } else {
                Text("glei").font(.headline)
                Text("In der App eine Haltestelle anheften, dann hier „Widget bearbeiten“.")
                    .font(.caption).foregroundStyle(.secondary)
            }
        }
        .frame(maxWidth: .infinity, maxHeight: .infinity, alignment: .topLeading)
    }
}

@main
struct GleiWidget: Widget {
    var body: some WidgetConfiguration {
        AppIntentConfiguration(kind: "glei", intent: ConfigIntent.self, provider: Provider()) { entry in
            WidgetView(entry: entry).containerBackground(.background, for: .widget)
        }
        .configurationDisplayName("Abfahrten")
        .description("Eine angeheftete Haltestelle aus der glei-App.")
        .supportedFamilies([.systemSmall, .systemMedium])
    }
}
