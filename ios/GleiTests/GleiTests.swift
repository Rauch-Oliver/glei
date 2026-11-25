import Foundation
import Testing
@testable import Glei

private let now = Date(timeIntervalSince1970: 1_790_000_000)

private func departure(_ line: String, _ destination: String, in minutes: Double, cancelled: Bool = false,
                       direction: String? = "outward") -> Departure {
    Departure(line: line, destination: destination, plannedTime: now.addingTimeInterval(minutes * 60),
              estimatedTime: nil, mode: "tram", cancelled: cancelled, direction: direction)
}

@Test func decodesApiResponse() throws {
    let json = #"[{"line":"3","destination":"Andritz","minutes":4,"plannedTime":"2026-10-07T16:05:00Z","estimatedTime":"2026-10-07T16:06:00.5Z","mode":"tram","cancelled":false,"direction":"outward"}]"#
    let result = try API.decoder.decode([Departure].self, from: Data(json.utf8))
    #expect(result[0].line == "3")
    #expect(result[0].plannedTime == (try Date("2026-10-07T16:05:00Z", strategy: .iso8601)))
    #expect(result[0].time.timeIntervalSince(result[0].plannedTime) == 60.5)   // fractional seconds accepted
}

@Test func upcomingDropsGoneAndCancelled() {
    let list = [departure("3", "A", in: 5), departure("3", "A", in: -1), departure("3", "A", in: 2, cancelled: true),
                departure("3", "A", in: -0.2)]
    #expect(upcoming(list, now: now).map { $0.minutes(from: now) } == [0, 5])
}

@Test func groupsInNaturalLineOrder() {
    let list = [departure("N5", "Puntigam", in: 1), departure("64E", "Ragnitz", in: 2), departure("23", "Hbf", in: 3),
                departure("3", "Krenngasse", in: 4, direction: "inward"), departure("3", "Andritz", in: 5),
                departure("3", "Schulzentrum", in: 6), departure("3", "Andritz", in: 7), departure("64", "Ragnitz", in: 8)]
    let groups = lineGroups(list, now: now)
    #expect(groups.map(\.line) == ["3", "3", "23", "64", "64E", "N5"])
    #expect(groups.map(\.label).prefix(2) == ["Andritz", "Krenngasse"])   // most frequent destination
}

@Test func pins() {
    let stop = Stop(id: "at:46:3171", name: "Graz Krenngasse")
    #expect(Pin(stop: stop).title == "Alle Linien")
    #expect(Pin(stop: stop, line: "3", direction: "outward", label: "Graz Andritz").title == "3er → Andritz")
    #expect(Pin(stop: stop).id != Pin(stop: stop, line: "3", direction: "outward").id)
    // The label (most frequent destination) changes over time; it must not make a new pin.
    #expect(Pin(stop: stop, line: "3", direction: "outward", label: "Andritz").id
            == Pin(stop: stop, line: "3", direction: "outward", label: "Schulzentrum").id)
    // Without a direction the destination tells the pins apart.
    #expect(Pin(stop: stop, line: "3", label: "Andritz").id != Pin(stop: stop, line: "3", label: "Puntigam").id)
}

@Test func texts() {
    #expect(lineName("3") == "3er")
    #expect(lineName("N5") == "Linie N5")
    #expect(shortName("Graz Krenngasse") == "Krenngasse")
    #expect(shortName("Wien Krenngasse") == "Wien Krenngasse")
    #expect(minutesText([departure("3", "A", in: 4), departure("3", "A", in: 11)], now: now) == "4 · 11 min")
    #expect(minutesText([departure("3", "A", in: 0), departure("3", "A", in: 7)], now: now) == "jetzt · 7 min")
    #expect(minutesText([departure("3", "A", in: 0)], now: now) == "jetzt")
}
