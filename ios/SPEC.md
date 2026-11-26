# glei für iOS – Spezifikation

Stand: 2026-11-25 · Status: Entwurf

Eine iPhone-App mit **angehefteten Haltestellen** und ein **Homescreen-Widget** dazu. Datenquelle ist die glei API (`api/`).

- In der **App** heftet man Haltestellen an: alle Linien, oder eine Linie in eine Richtung. Jede ist eine Karte mit den nächsten 3 Abfahrten, live.
- Jedes **Widget** zeigt **einen** dieser Pins. Für mehrere Haltestellen legt man mehrere Widgets an.

## 1. Rahmen

| Punkt | Entscheidung |
|---|---|
| Plattform | iPhone, ab iOS 17 (für `AppIntentConfiguration`, `ContentUnavailableView`). Getestet mit iOS 27 |
| Technik | Swift 6, SwiftUI, WidgetKit, App Intents. **Keine Abhängigkeiten**, kein Projektgenerator |
| Warum nativ | Web-Apps (PWA) können unter iOS keine Widgets anbieten |
| Nutzer | Vorerst nur ich (kein App Store) |
| Verteilung | Direkt aus Xcode aufs eigene iPhone |
| Signierung | Eigenes Apple-Team, steht in `ios/Local.xcconfig` (nicht im Git) |
| Ordner | `ios/` (eigenes Xcode-Projekt, unabhängig von der .NET-Solution) |
| Oberfläche | Deutsch (Entwicklungssprache `de`) |

## 2. Funktionen

### 2.1 App

```
┌────────────────────────────────────┐
│ glei                            ＋ │
│ ┌────────────────────────────────┐ │
│ │ Reininghausstraße  4er → Lieb. │ │
│ │  4                       1 min │ │
│ │  4                      16 min │ │
│ │  4                      31 min │ │
│ └────────────────────────────────┘ │
│ ┌────────────────────────────────┐ │
│ │ Krenngasse         Alle Linien │ │
│ │  3  Andritz              2 min │ │
│ │ [60] Lustbühel          12 min │ │
│ │  3  Jakominiplatz       15 min │ │
│ └────────────────────────────────┘ │
└────────────────────────────────────┘
```

- **Karten:** eine pro Pin, in der Reihenfolge des Anheftens. Kopf: Haltestelle (ohne „Graz “) und „Alle Linien“ bzw. „4er → Liebenau“. Darunter die nächsten 3 Abfahrten: Linie als Badge, Ziel, Minuten (`jetzt` bei 0).
  - Bei einer einzelnen Linie fällt das Ziel weg, es steht schon im Kopf.
  - Alle Karten werden gemeinsam abgerufen und ändern sich gemeinsam. Die Minuten werden jede volle Minute lokal neu berechnet.
  - **Laden:** Vor der ersten Antwort zeigt eine Karte Platzhalterzeilen (ausgegraut), keinen eigenen Ladekreis. Der einzige Ladeindikator ist der des Herunterziehens.
  - **Stand:** eine Zeile unter der letzten Karte für alle: „Stand 19:39“. Ist eine Karte offline: „offline · Stand“ mit den ältesten angezeigten Daten, ohne Daten nur „offline“. Daten von gestern oder älter mit „gestern“ bzw. Datum („offline · Stand gestern 22:15“).
  - **Offline:** wie unter Windows, Android und im Web: die letzten Abfahrten bleiben ausgegraut stehen, darunter „offline · Stand 19:39“. Ohne Daten „Keine Verbindung“.
- **Anheften (＋):** ein Sheet mit Haltestellensuche (ab 2 Zeichen, 300 ms Debounce, `GET /api/stops/search`). Eine Haltestelle öffnet die Linienauswahl:
  - **„Alle Linien“**, oder
  - **eine Linie in eine Richtung**, aus den gerade fahrenden Abfahrten nach Linie und Richtung gruppiert, beschriftet mit dem häufigsten Ziel („3 → Andritz · 4 · 11 · 19 min“).
  - Tippen heftet an und schließt das Sheet. Ein schon vorhandener Pin wird nicht doppelt angelegt.
- **Aktualisieren:** Liste nach unten ziehen. Lädt alle Karten neu (der Indikator bleibt, bis alle geantwortet haben) und damit auch die Widgets. Dasselbe in der Linienauswahl.
- **Entfernen:** Karte nach links wischen oder lange drücken → „Entfernen“.
- **Leer:** „Noch nichts angeheftet – Mit + eine Haltestelle suchen und anheften. Jedes Widget zeigt eine davon.“
- **Liniensymbole** wie auf den Grazer Haltestellenschildern: Bim ohne Rahmen, Bus mit Rahmen, Nachtlinien (`N…`) blau hinterlegt.

### 2.2 Widget

| Größe | Inhalt |
|---|---|
| Klein (2×2) | Haltestelle, Linie, 3 Abfahrten (Ziel gekürzt), „Stand 19:39“ |
| Mittel (4×2) | dasselbe mit vollem Ziel |

- **Einstellen:** Widget lange drücken → „Widget bearbeiten“ → **Haltestelle** = einer der Pins. Ein neues Widget zeigt gleich den ersten Pin.
- Ohne Pin bzw. wenn der Pin in der App entfernt wurde: „In der App eine Haltestelle anheften, dann hier „Widget bearbeiten“.“
- Darstellung wie die Karten in der App (gemeinsame Ansicht `BoardView`): **Keine Abfahrten:** „Keine Abfahrten“. **Offline:** letzte Abfahrten ausgegraut, „offline · Stand 19:39“. **Ohne Daten:** „Keine Verbindung“.
- **Tippen** öffnet die App.
- **Getönter / klarer Homescreen** (iOS 18+, Liquid Glass): Hintergrund über `containerBackground`, den das System durch Glas ersetzt. Der Haltestellenname ist `widgetAccentable`. Nachtlinien werden dort nur umrandet statt gefüllt, sonst würden sie ein einfarbiger Block.

## 3. Aktualisierung

| Was | Wann | Wie |
|---|---|---|
| **App: Karten** | alle 60 s, früher wenn weniger als 3 der abgerufenen Abfahrten übrig sind (frühestens nach 15 s). Sofort bei geänderten Pins, beim Herunterziehen und wenn die App aus dem Hintergrund zurückkommt; im Hintergrund pausiert. Kontrollzentrum, Mitteilungen oder Face ID (`.inactive`) unterbrechen nicht. Schlagen alle Pins fehl, 1 → 2 → 5 min (wie Windows/Linux); ein einzelner kaputter Pin bremst die anderen nicht | ein gemeinsamer Abruf für alle Karten (parallel, Ergebnisse gemeinsam übernommen); Timer, Herunterziehen und Pin-Änderungen laufen alle darüber, ein abgebrochener Abruf gilt nicht als offline |
| **Widget: aus der App** | nach jedem Abruf der App | Die App speichert jede Antwort pro Pin in der App Group (`BoardCache`) und ruft danach `WidgetCenter.reloadAllTimelines()` auf (nicht, wenn alle Abrufe fehlschlugen); zählt bei geöffneter App nicht zum Budget. Ist die gespeicherte Antwort jünger als 5 min (das Mindestintervall des Widgets; iOS führt das Neuladen manchmal verzögert aus), nimmt das Widget sie, statt selbst abzurufen: App und Widget zeigen dieselben Daten und denselben Stand |
| **Widget: Daten abrufen** (Netz) | nach 15 min, oder früher nach derselben Regel wie die App (weniger als 3 übrig). Nie früher als nach 5 min (Tagesbudget von iOS). Ohne Abfahrten und nach einem Fehler nach 5 min, nach einem Fehler mit den letzten Daten aus dem `BoardCache` | Timeline-Policy `.after(…)` |
| **Widget: Minuten neu berechnen** (lokal) | zu jeder vollen Minute, wie die Karten der App | ein Timeline-Eintrag pro voller Minute aus einem Abruf |
| **Widget: Pins geändert** | sofort | `WidgetCenter.reloadAllTimelines()` aus der App |

- Die Minuten werden immer lokal aus `estimatedTime ?? plannedTime` berechnet. Das Feld `minutes` der API wird ignoriert.
- Abfahrten bleiben noch 30 s als „jetzt“ stehen, ausgefallene werden nicht angezeigt (wie die Web-App).
- Wie oft ein Widget tatsächlich neu lädt, entscheidet iOS (Tagesbudget). An sehr belebten Haltestellen kann das frühere Nachladen das Budget aufbrauchen, dann aktualisiert iOS seltener.

## 4. Daten & Speicher

Die Pins liegen als JSON in den `UserDefaults` der **App Group** `group.at.glei.app`, Schlüssel `pins`, damit App und Widget sie teilen:

```json
[
  { "stop": { "id": "at:46:30263", "name": "Graz Reininghausstraße" },
    "line": "4", "direction": "outward", "label": "Liebenau" },
  { "stop": { "id": "at:46:3171", "name": "Graz Krenngasse" } }
]
```

- **ID eines Pins:** `stopId|line|direction`. Liefert TRIAS für eine Linie keine Richtung, wird stattdessen das Ziel (`label`) verwendet und auch danach gefiltert, weil die Linienauswahl solche Linien nach Ziel trennt.
- Das Widget speichert nur die ID seines Pins (in seiner Widget-Konfiguration).
- Die letzte erfolgreiche Antwort pro Pin liegt ebenfalls in der App Group (Schlüssel `board.<Pin-ID>`, `BoardCache`), damit das Widget die Daten der App übernimmt und offline die letzten Abfahrten zeigt. Beim Entfernen eines Pins wird sie gelöscht.
- Die API-Basis-URL steht in `Shared/Glei.swift` (die öffentliche Cloud-Run-URL des Service `glei`).
- Kein Login, keine Telemetrie, nichts außer den Pins und ihren letzten Abfahrten.

## 5. API

Keine Änderungen. Genutzt werden:

- `GET /api/stops/search?query=…`
- `GET /api/departures/{stopId}?limit=100` (Linienauswahl)
- `GET /api/departures/{stopId}?line=&direction=&limit=100` (Karten, Widget; die API liefert ohnehin höchstens 20, siehe 9.)

## 6. Architektur

```
ios/
  Glei.xcodeproj           Xcode-Projekt (App, Widget, Tests)
  Signing.xcconfig         bindet Local.xcconfig ein (Team, nicht im Git)
  Shared/Glei.swift        App + Widget: Modelle, Pin, API, Anzeigeregeln, LineBadge, DepartureRow
  Glei/GleiApp.swift       App: Karten, Suche, Linienauswahl
  GleiWidget/GleiWidget.swift
                           Widget: Pin-Auswahl (App Intent), Timeline, Ansicht
  GleiTests/               Swift Testing: JSON, Filter, Gruppierung, Texte, Pin-IDs
```

- **Gemeinsamer Code:** `Shared/Glei.swift` wird in App und Widget mitkompiliert. Die Anzeigeregeln (Gruppierung, häufigstes Ziel, Linienreihenfolge, Texte) entsprechen denen von `windows/` und `web/`.
- **Konfiguration des Widgets:** `AppIntentConfiguration` mit einem Parameter (`PinEntity`). Die Auswahl liefert eine `EntityQuery`, die die Pins aus der App Group liest.
- Keine Pakete, kein Netzwerk-Framework: `URLSession` + `JSONDecoder` mit ISO-8601-Datumsangaben.

## 7. Build

- In Xcode öffnen und ausführen, siehe [README](README.md). Tests mit `⌘U` oder:
  ```bash
  xcodebuild -project ios/Glei.xcodeproj -scheme Glei -destination 'platform=iOS Simulator,name=iPhone Air' test
  ```
- Kein CI, keine Releases (kein App Store).

## 8. Nicht im Umfang (v1)

- App Store / TestFlight
- Sperrbildschirm-Widgets, das neue extragroße Widget (4×6, iOS 27), Live Activities
- Tippen aufs Widget öffnet die passende Karte (Deep Link), Aktualisieren-Knopf im Widget
- Reihenfolge der Karten ändern
- „towards“ (Richtung als Zielhaltestelle über alle Linien, siehe Android)
- iPad

## 9. Offene Punkte

- **Nur 20 Abfahrten pro Haltestelle:** Die API fragt TRIAS nach 20 Abfahrten für die ganze Haltestelle und filtert danach nach Linie. Für ein Widget mit einer Linie an einer belebten Haltestelle bleiben so oft nur wenige übrig, deshalb lädt es früher nach. Besser: Fragt man nach einer Linie, holt die API mehr Ergebnisse von TRIAS (betrifft `core/`, also auch die anderen Clients).
- **Große Schrift:** Bei den größten Textgrößen passen im Widget keine 3 Zeilen mehr. Widgets scrollen nicht, also müssten es dann weniger Zeilen sein.
- **Vorschau in der Widget-Galerie:** zeigt ohne Pins nur den Hinweis zum Anheften, keine Beispieldaten.
