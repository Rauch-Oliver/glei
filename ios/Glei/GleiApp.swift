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
    @Environment(\.scenePhase) private var scenePhase
    @State private var adding = false
    /// Last answer per pin ID; missing = not loaded yet.
    @State private var boards: [String: Board] = [:]
    /// The one fetch of all pins; the timer, pull to refresh and pin changes all go through it.
    @State private var inFlight: Task<Void, Never>?
    /// Fetches in a row in which every pin failed (backoff).
    @State private var errors = 0
    /// When the timer fetches next; set after each fetch.
    @State private var nextRefresh = Date.distantFuture

    private var pins: [Pin] { (try? JSONDecoder().decode([Pin].self, from: pinData)) ?? [] }

    private func save(_ pins: [Pin]) {
        pinData = (try? JSONEncoder().encode(pins)) ?? Data()
        WidgetCenter.shared.reloadAllTimelines()
    }

    private func remove(_ pin: Pin) {
        save(pins.filter { $0.id != pin.id })
        boards[pin.id] = nil
        BoardCache.remove(pin)
    }

    /// Joins the running fetch, or starts one. `restart` cancels the running one first (pins changed).
    /// Returns once a fetch has finished; when the joined one is replaced meanwhile, waits for the replacement,
    /// so the pull to refresh indicator stays until the answers are shown.
    private func refresh(restart: Bool = false) async {
        if restart { cancelFetch() }
        if inFlight == nil { inFlight = Task { await fetchAll() } }
        while let task = inFlight {
            await task.value
            if inFlight == task { inFlight = nil }
        }
    }

    private func cancelFetch() {
        inFlight?.cancel()
        inFlight = nil
    }

    /// Fetches all pins in parallel and shows the answers together, so the cards change at once.
    /// Then schedules the next fetch: after 60 s, earlier when a card runs out of departures (at least 15 s);
    /// only when every pin failed 1, 2, then 5 min, like Windows/Linux.
    private func fetchAll() async {
        let pins = pins
        let fetched = await withTaskGroup(of: (String, Board?).self) { group in
            for pin in pins { group.addTask { (pin.id, await pin.board()) } }
            var fetched: [String: Board] = [:]
            for await (id, board) in group { fetched[id] = board }
            return fetched
        }
        let pinned = Set(self.pins.map(\.id))
        for pin in pins where !pinned.contains(pin.id) { BoardCache.remove(pin) }   // removed while loading
        guard !Task.isCancelled else { return }   // a cancelled fetch isn't "offline"; the restarted one shows its answers

        boards.merge(fetched.filter { pinned.contains($0.key) }) { $1 }
        let online = fetched.values.filter { !$0.offline }
        errors = online.isEmpty && !fetched.isEmpty ? errors + 1 : 0
        nextRefresh = errors > 0 ? .now.addingTimeInterval(retryDelay(afterErrors: errors))
            : online.map { nextFetch($0, now: .now, interval: 60, minimum: 15) }.min() ?? .now.addingTimeInterval(60)
        // Widgets take the new answers from `BoardCache`; free while the app is in the foreground.
        if !online.isEmpty { WidgetCenter.shared.reloadAllTimelines() }
    }

    var body: some View {
        let pins = pins   // decoded once per update
        let foreground = scenePhase != .background   // Control Center, banners or Face ID (.inactive) don't interrupt
        NavigationStack {
            List {
                ForEach(pins) { pin in
                    Section {
                        PinCard(pin: pin, board: boards[pin.id])
                            .swipeActions {
                                Button("Entfernen", role: .destructive) { remove(pin) }.tint(.red)
                            }
                            .contextMenu {   // swiping isn't discoverable; long press offers the same
                                Button("Entfernen", systemImage: "trash", role: .destructive) { remove(pin) }
                            }
                    } footer: {
                        // One line for all cards, they are fetched together.
                        if pin == pins.last, let status = combinedStatus(pins.compactMap { boards[$0.id] }) { Text(status) }
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
            .refreshable { await refresh() }
            // Right away when pins change or the app comes to the foreground; in the background nothing runs.
            .task(id: RefreshTrigger(pins: pinData, active: foreground)) {
                if foreground { await refresh(restart: true) } else { cancelFetch() }
            }
            // The timer: restarts whenever a fetch (also a pull) sets the next time.
            .task(id: ScheduleTrigger(at: nextRefresh, active: foreground)) {
                guard foreground else { return }
                try? await Task.sleep(for: .seconds(max(0, nextRefresh.timeIntervalSinceNow)))
                if !Task.isCancelled { await refresh() }
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

private struct RefreshTrigger: Equatable {
    let pins: Data
    let active: Bool
}

private struct ScheduleTrigger: Equatable {
    let at: Date
    let active: Bool
}

/// One pin: stop, line and the next 3 departures; minutes recomputed every minute.
struct PinCard: View {
    let pin: Pin
    let board: Board?

    var body: some View {
        TimelineView(.everyMinute) { context in
            VStack(alignment: .leading, spacing: 8) {
                VStack(alignment: .leading, spacing: 2) {   // stacked, so long names and large text don't collide
                    Text(shortName(pin.stop.name)).font(.headline)
                    Text(pin.title).font(.subheadline).foregroundStyle(.secondary)
                }
                BoardView(pin: pin, board: board, now: context.date)
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
                guard text.count >= 2 else { stops = []; searched = ""; return }
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
                    ForEach(lineGroups(failed ? [] : departures ?? [], now: context.date)) { group in   // no stale lines after a failed refresh
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
