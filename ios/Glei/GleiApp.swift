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

    private var pins: [Pin] { (try? JSONDecoder().decode([Pin].self, from: pinData)) ?? [] }

    private func save(_ pins: [Pin]) {
        pinData = (try? JSONEncoder().encode(pins)) ?? Data()
        WidgetCenter.shared.reloadAllTimelines()
    }

    var body: some View {
        NavigationStack {
            List {
                ForEach(pins) { pin in
                    Section {
                        PinCard(pin: pin)
                            .swipeActions {
                                Button("Entfernen", role: .destructive) { save(pins.filter { $0.id != pin.id }) }.tint(.red)
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

/// One pin: stop, line and the next 3 departures, refreshed every minute.
struct PinCard: View {
    let pin: Pin
    @State private var departures: [Departure]?
    @State private var fetchedAt: Date?
    @State private var failed = false

    var body: some View {
        TimelineView(.everyMinute) { context in
            VStack(alignment: .leading, spacing: 8) {
                HStack(alignment: .firstTextBaseline) {
                    Text(shortName(pin.stop.name)).font(.headline)
                    Spacer()
                    Text(pin.title).font(.subheadline).foregroundStyle(.secondary)
                }
                if let departures {
                    let next = upcoming(departures, now: context.date).prefix(3)
                    if next.isEmpty { Text("Keine Abfahrten").foregroundStyle(.secondary) }
                    ForEach(Array(next), id: \.self) {
                        DepartureRow(departure: $0, now: context.date, showsDestination: pin.line == nil)
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
        .task {
            while !Task.isCancelled {
                do {
                    departures = try await pin.departures()
                    fetchedAt = .now
                    failed = false
                } catch {
                    failed = true
                }
                try? await Task.sleep(for: .seconds(60))
            }
        }
    }
}

/// Search a stop, then pin all lines or one line + direction.
struct SearchView: View {
    let onPick: (Pin) -> Void
    @State private var query = ""
    @State private var stops: [Stop] = []

    var body: some View {
        NavigationStack {
            List(stops) { stop in
                NavigationLink(stop.name) { LinesView(stop: stop, onPick: onPick) }
            }
            .navigationTitle("Haltestelle")
            .navigationBarTitleDisplayMode(.inline)
            .searchable(text: $query, placement: .navigationBarDrawer(displayMode: .always), prompt: "Haltestelle suchen")
            .task(id: query) {
                try? await Task.sleep(for: .milliseconds(300))   // debounce; cancelled by the next keystroke
                guard query.count >= 2, !Task.isCancelled else { return stops = [] }
                stops = (try? await API.stops(query)) ?? []
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
        List {
            Section {
                Button("Alle Linien") { onPick(Pin(stop: stop)) }
            }
            Section("Eine Linie") {
                if failed { Text("Keine Verbindung").foregroundStyle(.secondary) }
                ForEach(lineGroups(departures ?? [], now: .now)) { group in
                    Button {
                        onPick(Pin(stop: stop, line: group.line, direction: group.direction, label: group.label))
                    } label: {
                        HStack {
                            LineBadge(line: group.line, mode: group.mode)
                            Text("→ \(shortName(group.label))").lineLimit(1)
                            Spacer()
                            Text(minutesText(group.next, now: .now)).monospacedDigit().foregroundStyle(.secondary)
                        }
                    }
                    .foregroundStyle(.primary)
                }
            }
        }
        .overlay { if departures == nil && !failed { ProgressView() } }
        .navigationTitle(shortName(stop.name))
        .task {
            do { departures = try await API.departures(stop.id) } catch { failed = true }
        }
    }
}
