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
    let board: Board?
}

/// One fetch, then one entry per full minute (like the app's cards) in which the minutes are recomputed.
/// Fetches again after 15 min, or earlier when fewer than 3 of the fetched departures are left (same rule as the app),
/// but never sooner than 5 min: iOS only grants a few dozen reloads a day. After an error: 5 min, with the old data.
struct Provider: AppIntentTimelineProvider {
    func placeholder(in context: Context) -> Entry { Entry(date: .now, pin: nil, board: nil) }

    func snapshot(for config: ConfigIntent, in context: Context) async -> Entry {
        await timeline(for: config, in: context).entries[0]
    }

    func timeline(for config: ConfigIntent, in context: Context) async -> Timeline<Entry> {
        guard let pin = config.pin?.pin else { return Timeline(entries: [placeholder(in: context)], policy: .never) }
        // The app reloads the widgets after each fetch: show that answer instead of fetching again. iOS may run the
        // reload a bit later, so anything younger than the widget's own minimum interval (5 min) counts as fresh.
        let cached = BoardCache.load(pin)
        let board = if let cached, let at = cached.fetchedAt, at.timeIntervalSinceNow > -5 * 60 { cached } else {
            await pin.board() ?? Board(departures: [], fetchedAt: nil, offline: true)   // nil: cancelled by WidgetKit
        }

        let now = Date.now
        // Offline or no departures (night, before the first trip): look again after 5 min.
        let reload = board.offline || upcoming(board.departures, now: now).isEmpty ? now.addingTimeInterval(5 * 60)
            : nextFetch(board, now: now, interval: 15 * 60, minimum: 5 * 60)
        let nextMinute = Calendar.current.dateInterval(of: .minute, for: now)!.end
        let dates = [now] + stride(from: nextMinute, to: reload, by: 60)
        return Timeline(entries: dates.map { Entry(date: $0, pin: pin, board: board) }, policy: .after(reload))
    }
}

// MARK: - View

struct WidgetView: View {
    let entry: Entry

    var body: some View {
        // Margins come from WidgetKit (16 pt, Apple's standard); the content just has to fit inside them.
        VStack(alignment: .leading, spacing: 4) {
            if let pin = entry.pin {
                Text(shortName(pin.stop.name)).font(.headline).lineLimit(1).widgetAccentable()
                Text(pin.title).font(.caption).foregroundStyle(.secondary).lineLimit(1)
                BoardView(pin: pin, board: entry.board, now: entry.date, compact: true).font(.caption)
                Spacer(minLength: 0)
                if let status = entry.board?.status(now: entry.date) {
                    Text(status).font(.caption2).foregroundStyle(.secondary).lineLimit(1)
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
