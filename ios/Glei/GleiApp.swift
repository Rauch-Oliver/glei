import SwiftUI
import WidgetKit

@main
struct GleiApp: App {
    var body: some Scene {
        WindowGroup { PinsView().tint(gleiColor) }
    }
}

/// The pinned stops as cards with live departures.
struct PinsView: View {
    @AppStorage(pinsKey, store: sharedDefaults) private var pinData = Data()
    @State private var adding = false
    /// Last successful answer per pin ID, and the pins whose last fetch failed.
    @State private var loaded: [String: (departures: [Departure], at: Date)] = [:]
    @State private var failed: Set<String> = []

    private var pins: [Pin] { (try? JSONDecoder().decode([Pin].self, from: pinData)) ?? [] }

    private func save(_ pins: [Pin]) {
        pinData = (try? JSONEncoder().encode(pins)) ?? Data()
        WidgetCenter.shared.reloadAllTimelines()
    }

    /// Fetches all pins in parallel; returns when all answered, so pull to refresh can wait for it.
    private func load() async {
        await withTaskGroup(of: (String, [Departure]?).self) { group in
            for pin in pins { group.addTask { (pin.id, try? await pin.departures()) } }
            for await (id, departures) in group {
                if let departures {
                    loaded[id] = (departures, .now)
                    failed.remove(id)
                } else {
                    failed.insert(id)
                }
            }
        }
    }

    var body: some View {
        NavigationStack {
            List {
                ForEach(pins) { pin in
                    Section {
                        PinCard(pin: pin, departures: loaded[pin.id]?.departures, fetchedAt: loaded[pin.id]?.at,
                                failed: failed.contains(pin.id))
                            .swipeActions {
                                Button("Entfernen", role: .destructive) { save(pins.filter { $0.id != pin.id }) }.tint(.red)
                            }
                            .contextMenu {   // swiping isn't discoverable; long press offers the same
                                Button("Entfernen", systemImage: "trash", role: .destructive) {
                                    save(pins.filter { $0.id != pin.id })
                                }
                            }
                    }
                }
            }
            .overlay {
                if pins.isEmpty {
                    ContentUnavailableView("Noch nichts angeheftet", systemImage: "pin",
                                           description: Text("Mit + eine Haltestelle suchen und anheften. Jedes Widget zeigt eine davon."))
                }
            }
            .navigationTitle("glei")
            .navigationBarTitleDisplayMode(.inline)
            .refreshable {
                await load()
                WidgetCenter.shared.reloadAllTimelines()   // free while the app is open
            }
            .task(id: pinData) {   // restarts right away when pins are added or removed
                while !Task.isCancelled {
                    await load()
                    try? await Task.sleep(for: .seconds(60))
                }
            }
            .toolbar { Button("Anheften", systemImage: "plus") { adding = true } }
            .sheet(isPresented: $adding) {
                SearchView { pin in
                    if !pins.contains(where: { $0.id == pin.id }) { save(pins + [pin]) }
                    adding = false
                }
            }
        }
    }
}

/// One pin: stop, line and the next 3 departures; minutes recomputed every minute.
struct PinCard: View {
    let pin: Pin
    let departures: [Departure]?
    let fetchedAt: Date?
    let failed: Bool

    var body: some View {
        TimelineView(.everyMinute) { context in
            VStack(alignment: .leading, spacing: 8) {
                VStack(alignment: .leading, spacing: 2) {   // stacked, so long names and large text don't collide
                    Text(shortName(pin.stop.name)).font(.headline)
                    Text(pin.title).font(.subheadline).foregroundStyle(.secondary)
                }
                if let departures {
                    let next = upcoming(departures, now: context.date).prefix(3)
                    if next.isEmpty { Text("Keine Abfahrten").foregroundStyle(.secondary) }
                    ForEach(Array(next.enumerated()), id: \.offset) {   // two departures can be identical
                        DepartureRow(departure: $0.element, now: context.date, showsDestination: pin.line == nil)
                    }
                    if failed, let fetchedAt {
                        Text("Stand \(fetchedAt, style: .time) · offline").font(.caption).foregroundStyle(.orange)
                    }
                } else if failed {
                    Text("Keine Verbindung").foregroundStyle(.secondary)
                } else {
                    ProgressView()
                }
            }
        }
    }
}

/// Search a stop, then pin all lines or one line + direction.
struct SearchView: View {
    let onPick: (Pin) -> Void
    @Environment(\.dismiss) private var dismiss
    @State private var query = ""
    @State private var stops: [Stop] = []
    @State private var searched = ""   // the query `stops` belongs to
    @State private var failed = false

    var body: some View {
        NavigationStack {
            List(stops) { stop in
                NavigationLink(stop.name) { LinesView(stop: stop, onPick: onPick) }
            }
            .overlay {
                if stops.isEmpty && searched == query && query.count >= 2 {
                    if failed {
                        ContentUnavailableView("Keine Verbindung", systemImage: "wifi.slash")
                    } else {
                        ContentUnavailableView.search(text: query)
                    }
                }
            }
            .navigationTitle("Haltestelle")
            .navigationBarTitleDisplayMode(.inline)
            .toolbar {
                ToolbarItem(placement: .cancellationAction) { Button("Abbrechen", role: .cancel) { dismiss() } }
            }
            .searchable(text: $query, placement: .navigationBarDrawer(displayMode: .always), prompt: "Haltestelle suchen")
            .task(id: query) {
                let text = query
                guard text.count >= 2 else { return stops = [] }
                // Debounce. The next keystroke cancels this task; then keep the current results until new ones arrive.
                try? await Task.sleep(for: .milliseconds(300))
                guard !Task.isCancelled else { return }
                let result = try? await API.stops(text)
                guard !Task.isCancelled else { return }
                stops = result ?? []
                failed = result == nil
                searched = text
            }
        }
    }
}

/// The lines currently running at a stop; tapping one pins it.
struct LinesView: View {
    let stop: Stop
    let onPick: (Pin) -> Void
    @State private var departures: [Departure]?
    @State private var failed = false

    var body: some View {
        TimelineView(.everyMinute) { context in
            List {
                Section {
                    Button("Alle Linien") { onPick(Pin(stop: stop)) }
                }
                Section("Eine Linie") {
                    if failed { Text("Keine Verbindung").foregroundStyle(.secondary) }
                    ForEach(lineGroups(departures ?? [], now: context.date)) { group in
                        Button {
                            onPick(Pin(stop: stop, line: group.line, direction: group.direction, label: group.label))
                        } label: {
                            HStack {
                                LineBadge(line: group.line, mode: group.mode)
                                Text("→ \(shortName(group.label))").lineLimit(1)
                                Spacer()
                                Text(minutesText(group.next, now: context.date)).monospacedDigit().foregroundStyle(.secondary)
                            }
                        }
                        .foregroundStyle(.primary)
                    }
                }
            }
        }
        .overlay { if departures == nil && !failed { ProgressView() } }
        .navigationTitle(shortName(stop.name))
        .navigationBarTitleDisplayMode(.inline)
        .refreshable { await load() }
        .task { await load() }
    }

    private func load() async {
        do {
            departures = try await API.departures(stop.id)
            failed = false
        } catch {
            failed = true
        }
    }
}
