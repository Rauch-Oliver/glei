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
  - Abruf aller Karten gemeinsam alle 60 s und sofort, wenn ein Pin dazukommt oder wegfällt. Die Minuten werden jede volle Minute lokal neu berechnet.
  - **Offline:** ohne Daten „Keine Verbindung“, mit alten Daten zusätzlich „Stand 19:39 · offline“ (orange).
- **Anheften (＋):** ein Sheet mit Haltestellensuche (ab 2 Zeichen, 300 ms Debounce, `GET /api/stops/search`). Eine Haltestelle öffnet die Linienauswahl:
  - **„Alle Linien“**, oder
  - **eine Linie in eine Richtung**, aus den gerade fahrenden Abfahrten nach Linie und Richtung gruppiert, beschriftet mit dem häufigsten Ziel („3 → Andritz · 4 · 11 · 19 min“).
  - Tippen heftet an und schließt das Sheet. Ein schon vorhandener Pin wird nicht doppelt angelegt.
- **Aktualisieren:** Liste nach unten ziehen. Lädt alle Karten neu (der Indikator bleibt, bis alle geantwortet haben) und lädt auch die Widgets neu. Dasselbe in der Linienauswahl.
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
- **Keine Abfahrten:** „Keine Abfahrten“. **Fehler:** „Keine Verbindung“.
- **Tippen** öffnet die App.
- **Getönter / klarer Homescreen** (iOS 18+, Liquid Glass): Hintergrund über `containerBackground`, den das System durch Glas ersetzt. Der Haltestellenname ist `widgetAccentable`. Nachtlinien werden dort nur umrandet statt gefüllt, sonst würden sie ein einfarbiger Block.

## 3. Aktualisierung

| Was | Wann | Wie |
|---|---|---|
| **Widget: Daten abrufen** (Netz) | nach 15 min, oder früher: wenn nur noch 2 der abgerufenen Abfahrten übrig sind, bei 3 oder weniger schon wenn die erste abgefahren ist. Nie früher als nach 5 min (Tagesbudget von iOS). Ohne Abfahrten und nach einem Fehler nach 5 min | Timeline-Policy `.after(…)` |
| **Widget: Minuten neu berechnen** (lokal) | jede Minute | ein Timeline-Eintrag pro Minute aus einem Abruf |
| **Widget: Pins geändert** | sofort | `WidgetCenter.reloadAllTimelines()` aus der App |
| **App: Karten** | alle 60 s, solange sichtbar, sofort bei geänderten Pins und beim Herunterziehen | eine `.task`-Schleife für alle Karten, parallel abgerufen; `.refreshable` wartet auf denselben Abruf |
| **Widget: aus der App** | beim Herunterziehen | `WidgetCenter.reloadAllTimelines()`; zählt bei geöffneter App nicht zum Budget |

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
- Die API-Basis-URL steht in `Shared/Glei.swift` (die öffentliche Cloud-Run-URL des Service `glei`).
- Kein Login, keine Telemetrie, nichts außer den Pins.

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
