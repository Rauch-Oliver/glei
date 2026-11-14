# glei Tray (Windows) – Spezifikation

Stand: 2026-11-14 · Status: Entwurf

Eine kleine Windows-App, die im **Infobereich der Taskleiste** (System Tray) lebt. Sie gibt es in **einer Variante** mit **einem Favoriten** (Haltestelle + Linie + Richtung):

- Das **Tray-Icon** zeigt die Minuten bis zur nächsten Abfahrt des Favoriten.
- Der **Tooltip** zeigt die nächsten 3 Abfahrten.
- Ein **Klick** öffnet ein Popup mit den Abfahrten des Favoriten und einer **Suche nach anderen Haltestellen**.

Datenquelle ist die glei API (`api/`).

## 1. Rahmen

| Punkt | Entscheidung |
|---|---|
| Plattform | Windows 10 (ab 1809) und Windows 11, x64 + ARM64 |
| Technik | .NET 10, WPF mit Fluent-Theme (`ThemeMode="System"`), Tray über [H.NotifyIcon.Wpf](https://github.com/HavenDV/H.NotifyIcon) |
| Warum keine PWA | Browser erlauben kein Tray-Icon, eine installierte PWA bekommt nur einen Taskleisten-/Startmenü-Eintrag |
| Verteilung | Installer über **GitHub Releases**, gebaut von GitHub Actions, mit **Velopack** (Setup.exe + Auto-Update) |
| Signierung | Vorerst keine. Beim ersten Start erscheint die SmartScreen-Warnung („Weitere Informationen → Trotzdem ausführen“) |
| Installation | Pro Benutzer nach `%LOCALAPPDATA%`, keine Admin-Rechte |
| Ordner | `windows/` |
| Oberfläche | Deutsch |

## 2. Funktionen

### 2.1 Tray-Icon

- Das Icon zeigt die **Minuten bis zur nächsten Abfahrt** des Favoriten als Zahl, gerendert als Icon (16/20/24/32 px, je nach DPI-Skalierung).

  | Zustand | Icon |
  |---|---|
  | 0–59 min | Zahl, z. B. `4`. `0` heißt „fährt jetzt“ |
  | ≥ 60 min | `h` |
  | Keine Abfahrten | `–` |
  | Offline / Fehler | die letzte Zahl ausgegraut. Ohne Daten `!` |
  | Kein Favorit gesetzt | das statische glei-Icon (Tram) |

- Die Schriftfarbe passt sich an die Taskleiste an: weiß bei dunkler, schwarz bei heller Taskleiste. Die Erkennung erfolgt über `SystemUsesLightTheme` in der Registry, mit Reaktion auf Theme-Wechsel.
- Gibt es Echtzeitdaten, hat das Icon einen kleinen Punkt unten rechts, in der Akzentfarbe.
- **Tooltip** (beim Darüberfahren), zum Beispiel:
  ```
  Krenngasse · 3er → Andritz
  4 min · 11 min · 19 min
  Stand 16:04
  ```
- **Linksklick:** öffnet das Popup bzw. schließt es.
- **Rechtsklick:** Kontextmenü mit „Aktualisieren“, „Mit Windows starten“ (✓), „Info“ und „Beenden“.

### 2.2 Popup

Das Popup öffnet sich über dem Infobereich, wie die Windows-Flyouts (Lautstärke, WLAN). Es schließt bei Klick daneben oder mit `Esc`. Größe ca. 360 × 480 px, mit Mica-Hintergrund ab Windows 11.

```
┌────────────────────────────────────┐
│ ★ Krenngasse · 3er → Andritz    ⚙ │
│   3  Andritz            4 min  ●   │
│   3  Andritz           11 min  ●   │
│   3  Andritz           19 min      │
│                         Stand 16:04│
├────────────────────────────────────┤
│ 🔍 Haltestelle suchen …            │
│                                    │
│  (Suchergebnisse / Abfahrten)      │
└────────────────────────────────────┘
```

**Favoriten-Bereich (oben):**
- Die nächsten **3 Abfahrten** des Favoriten: Linie als Badge, Ziel, Minuten, und der Echtzeit-Punkt.
- Ausgefallene Fahrten werden durchgestrichen mit „fällt aus“ angezeigt und zählen nicht zu den 3.
- Mit ⚙ ändert man den Favoriten. Das öffnet denselben Ablauf wie „Als Favorit setzen“, siehe unten.
- Ist kein Favorit gesetzt, steht hier „Noch kein Favorit – such unten eine Haltestelle“.

**Suche (unten):**
1. Ein Eingabefeld, mit `Strg+F` oder einfach durch Lostippen beim Öffnen. Die Suche startet ab 2 Zeichen, mit 300 ms Debounce, über `GET /api/stops/search`. Ergebnisse lassen sich mit den Pfeiltasten und `Enter` auswählen.
2. **Haltestelle gewählt:** Die Abfahrten werden **nach Linie und Richtung gruppiert** angezeigt, je Gruppe mit den nächsten 3, z. B.:
   ```
   3er → Andritz           4 · 11 · 19 min
   3er → Krenngasse        2 ·  9 · 17 min
   ```
3. **Pro Gruppe** gibt es die Aktion **„★ Als Favorit“**. Sie speichert Haltestelle, Linie und Richtung (`outward`/`inward`), und das Tray-Icon wechselt sofort.
4. Mit `Esc` bzw. ← geht es zurück zum leeren Suchfeld. Die zuletzt gesuchten **5 Haltestellen** werden unter dem Suchfeld als Schnellwahl angezeigt.

Die Anzeige der gesuchten Haltestelle wird alle 30 s aktualisiert, solange das Popup offen ist.

### 2.3 Erststart

- Nach der Installation startet die App sofort und legt das Icon in den Infobereich. Dazu erscheint eine Windows-Benachrichtigung: „glei läuft im Infobereich – klick aufs Symbol, um deine Haltestelle festzulegen.“
- **Mit Windows starten** ist standardmäßig **an**. Umgesetzt über den Registry-Schlüssel `HKCU\…\Run`. Abschaltbar im Kontextmenü.
- Hinweis an den Nutzer: Windows 11 versteckt neue Tray-Icons im Überlauf (^). Die App zeigt beim Erststart einmalig einen Tipp, wie man das Icon dauerhaft anheftet: *Einstellungen → Personalisierung → Taskleiste → Weitere Symbole der Taskleiste → glei: Ein*.

## 3. Aktualisierung

| Was | Wann |
|---|---|
| **Abruf Favorit** (Netz) | alle **60 s**, und sofort bei: Popup öffnen, Entsperren, Aufwachen aus dem Standby, Netz wieder da |
| **Minuten neu berechnen** (lokal) | zu jeder vollen Minute (Timer), aus den gespeicherten absoluten Zeiten |
| **Pause** | gesperrter Bildschirm, Standby, Akku-Sparmodus. Erkennung über `SystemEvents.SessionSwitch`, `PowerModeChanged` |

- Abgerufen werden **bis zu 10** kommende Abfahrten mit `estimatedTime ?? plannedTime`. Die Minuten rechnet die App selbst aus. Das Feld `minutes` der API wird ignoriert.
- Abfahrten in der Vergangenheit fallen weg, und es rücken gespeicherte nach. Sind weniger als 3 übrig, wird sofort neu abgerufen.
- **Fehler:** Es wird mit Backoff erneut versucht (60 s → 2 → 5 min). Die letzten Daten bleiben ausgegraut sichtbar, mit „offline · Stand 15:48“.
- **Keine Abfahrten** (z. B. nachts): „Keine Abfahrten in den nächsten Stunden“.

## 4. Daten & Einstellungen

Die Einstellungen liegen in `%APPDATA%\glei\settings.json`. Das ist getrennt vom Installationsordner von Velopack (`%LOCALAPPDATA%\glei`), damit Updates sie nie anfassen:

```json
{
  "favorite": {
    "stopId": "at:46:3171",
    "stopName": "Krenngasse",
    "line": "3",
    "direction": "outward",
    "directionLabel": "Andritz"
  },
  "recentStops": [{ "stopId": "at:46:30263", "stopName": "Reininghausstraße" }],
  "startWithWindows": true
}
```

- Die API-Basis-URL steht in `appsettings.json` neben der .exe (die öffentliche Cloud-Run-URL des Service `glei`). Für Entwicklung lässt sie sich mit der Umgebungsvariable `GLEI_API_URL` überschreiben.
- Es gibt keinen Login, keine Telemetrie, und es wird nichts außer der Einstellungsdatei gespeichert.
- Das Log liegt in `%APPDATA%\glei\logs\` (Serilog, rollierend, 7 Tage), damit Fehler nachvollziehbar sind.

## 5. Benötigte API-Änderungen (`api/`)

Die API filtert bisher nur nach Linie, nicht nach Richtung. Für die App kommen optionale, rein additive Query-Parameter hinzu:

```
GET /api/departures/{stopId}?line=3&direction=outward&limit=10
```

| Parameter | Bedeutung |
|---|---|
| `line` | optional, wie `/line/{line}` |
| `direction` | optional, `outward` / `inward` |
| `limit` | optional, Standard: alle |

Für die gruppierte Ansicht der gesuchten Haltestelle reicht `GET /api/departures/{stopId}`. Gruppiert wird in der App nach `line` + `direction`, beschriftet mit dem häufigsten `destination` der Gruppe.

Diese Parameter braucht auch die Android-Spezifikation (`android/SPEC.md`). Die Umsetzung erfolgt also einmal für beide.

## 6. Architektur

```
windows/
  TriasDeparture.Windows/         WPF-App (net10.0-windows)
    App.xaml                      Start, Single Instance (Mutex), Velopack-Hooks
    Tray/                         TrayIconService, IconRenderer (Zahl → Icon), Tooltip
    Popup/                        PopupWindow (Positionierung am Tray), ViewModels
    Data/                         GleiApiClient (HttpClient), DepartureStore, SettingsStore
    Scheduling/                   RefreshScheduler (60 s, Pause bei Sperre/Standby, Backoff)
  TriasDeparture.Windows.Tests/   xUnit + Shouldly: Minuten, Nachrücken, Gruppierung, IconRenderer-Zustände
```

- **MVVM** mit CommunityToolkit.Mvvm.
- **Modelle:** Projektverweis auf `core/TriasDeparture.Domain` (`DepartureDto`, `StopDto`). Damit bleiben API und App bei den Typen synchron. Die App spricht **nur mit der glei API**, nie direkt mit TRIAS, weil die TRIAS-URL nicht in eine verteilte App gehört.
- **Single Instance:** Ein zweiter Start öffnet nur das Popup der laufenden Instanz.

## 7. Build & Release

- **GitHub Actions** auf Tag `windows-v*`, z. B. `windows-v0.1.0`:
  1. `dotnet test windows/…Tests`
  2. `dotnet publish` (self-contained, win-x64 und win-arm64)
  3. `vpk pack` → `Setup.exe` + Update-Pakete
  4. Upload als GitHub Release
- **Auto-Update:** Velopack prüft beim Start und dann alle 24 h die GitHub Releases. Ein Update wird im Hintergrund geladen und beim nächsten Start angewendet.
- Der Cloud-Build-Trigger der API ignoriert `windows/**`, weil seine Included Files nur `api/**` und `core/**` umfassen. Es ist also nichts zu ändern.

## 8. Nicht im Umfang (v1)

- Mehrere Favoriten
- Richtung als Zielhaltestelle über alle Linien („towards“, siehe Android)
- Benachrichtigungen („Dein 3er fährt in 5 min“)
- Code-Signierung, Microsoft Store, winget
- macOS/Linux

## 9. Offene Punkte

- ~~Rate Limiting der öffentlichen API~~ – umgesetzt: pro IP 60 Anfragen Burst, 30/min. Die App braucht ca. 1/min, beim Suchen ein paar mehr. Bei `429` wartet sie `Retry-After` ab.
- **Code-Signierung:** Erst wenn andere die App wirklich nutzen. Möglichkeiten: Azure Trusted Signing ca. 10 $/Monat oder ein OV-Zertifikat.
- **Icon-Lesbarkeit:** Zweistellige Zahlen in 16 px (100 % Skalierung) müssen getestet werden. Notfalls schmalere Schrift.
