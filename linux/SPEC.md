# glei Tray (Linux) – Spezifikation

Stand: 2026-11-18 · Status: Entwurf

Die Linux-Variante der [Windows-Tray-App](../windows/SPEC.md). Funktionen, Verhalten und Texte sind **gleich**. Dieses Dokument beschreibt nur, was unter Linux anders ist. Alles, was hier nicht steht, gilt wie in der Windows-Spezifikation.

- Das **Tray-Icon** zeigt die Minuten bis zur nächsten Abfahrt des Favoriten.
- Der **Tooltip** zeigt die nächsten 3 Abfahrten.
- Ein **Klick** öffnet ein Popup mit den Abfahrten des Favoriten und einer **Suche nach anderen Haltestellen**.

## 1. Rahmen

| Punkt | Entscheidung |
|---|---|
| Plattform | Linux-Desktops mit StatusNotifierItem-Host (KDE Plasma, GNOME mit AppIndicator-Erweiterung, Xfce, Cinnamon, MATE, Budgie), x64 + ARM64 |
| Technik | .NET 10, [Avalonia](https://avaloniaui.net) 12 mit Fluent-Theme. Tray über Avalonias `TrayIcon` (StatusNotifierItem über D-Bus) |
| Warum Avalonia | WPF gibt es nur unter Windows. Avalonia ist XAML/MVVM wie WPF, das ViewModel und die Logik lassen sich fast unverändert übernehmen |
| Anzeige | X11 und Wayland (über XWayland) |
| Verteilung | **AppImage** über **GitHub Releases**, gebaut von GitHub Actions, mit **Velopack** (inkl. Auto-Update) |
| Signierung | Keine (unter Linux nicht üblich) |
| Installation | Keine: AppImage herunterladen, ausführbar machen, starten. Keine Root-Rechte |
| Ordner | `linux/` |
| Oberfläche | Deutsch |

## 2. Funktionen – Unterschiede zu Windows

### 2.1 Tray-Icon

- Zustände wie unter Windows (Zahl, `h`, `–`, ausgegraut, `!`, App-Icon ohne Favorit), mit Echtzeit-Punkt.
- Das Icon wird in **64 × 64 px** gerendert. Der Tray-Host skaliert es auf die Panel-Größe, eine DPI-Abfrage wie unter Windows entfällt.
- **Schriftfarbe:**
  - **GNOME:** immer weiß. Die obere Leiste ist im hellen wie im dunklen Modus dunkel.
  - **Andere Desktops:** nach dem System-Theme (Avalonia `ActualThemeVariant`, über das Freedesktop-Portal): schwarz bei hellem, weiß bei dunklem Theme, mit Reaktion auf Theme-Wechsel.
- **Tooltip:** Avalonia setzt den Text als `Title` des StatusNotifierItems. Ob und wie er erscheint, entscheidet der Host (KDE zeigt ihn, GNOMEs AppIndicator-Erweiterung nicht).
- **Linksklick:** öffnet bzw. schließt das Popup (`Activate`). Manche Hosts (u. a. Ubuntus AppIndicator-Erweiterung) öffnen bei Linksklick stattdessen das Menü, deshalb steht **„Öffnen“** als erster Menüeintrag.
- **Menü:** „Öffnen“, „Aktualisieren“, „Beim Anmelden starten“ (✓), „Info“, „Beenden“.

### 2.2 Popup

- Gleicher Aufbau und gleiche Bedienung wie unter Windows (Favorit oben, Suche unten, Gruppierung nach Linie und Richtung, ☆/★, `Esc`, `Strg+F`, Lostippen, ↓ in die Ergebnisse).
- Fenster ohne Rahmen, immer oben, nicht in der Taskleiste, ca. 360 × 480 px. Schließt bei Fokusverlust oder `Esc`.
- **Position:** Linux sagt einer App nicht, wo ihr Tray-Icon sitzt. Deshalb:
  1. Liegt ein Panel oben (Arbeitsbereich beginnt unter dem Bildschirmrand): **oben rechts** darunter.
  2. Liegt es unten: **unten rechts** darüber.
  3. Ist der Arbeitsbereich unbekannt (z. B. Wayland): unter GNOME oben rechts, sonst unten rechts.
- Symbole aus Unicode (← ☆ ★) statt Segoe Fluent Icons.

### 2.3 Erststart

- Beim ersten Start erscheint eine Desktop-Benachrichtigung über `notify-send`: „glei läuft im Panel – klick aufs Symbol (oder wähl „Öffnen“), um deine Haltestelle festzulegen“, mit dem Hinweis auf die GNOME-Erweiterung. Fehlt `notify-send`, entfällt sie.
- Ist noch kein Favorit gesetzt, öffnet sich das Popup gleich mit. So kommt man auch an die App, wenn das Panel gar keinen Tray hat.
- **Beim Anmelden starten** ist standardmäßig **an**. Umgesetzt über `~/.config/autostart/glei.desktop` ([XDG Autostart](https://specifications.freedesktop.org/autostart-spec/)). Abschaltbar im Menü. Gespeichert im selben Feld wie unter Windows (`startWithWindows`), damit das Einstellungsformat gleich bleibt.
- Zusätzlich legt die App einen **Eintrag im App-Menü** an (`~/.local/share/applications/glei.desktop` + Icon unter `~/.local/share/icons/hicolor/256x256/apps/glei.png`). Damit lässt sie sich nach „Beenden“ wieder starten.
- `Exec=` zeigt auf die AppImage-Datei (`$APPIMAGE`), nicht auf den temporären Mount-Ordner. Beide `.desktop`-Dateien werden bei jedem Start aktualisiert, falls das AppImage verschoben wurde.
- Nur das installierte AppImage trägt sich ein. Ein Entwicklungs-Build fasst Autostart und App-Menü nie an, der Menüpunkt „Beim Anmelden starten“ ist dort ausgegraut.

## 3. Aktualisierung

Wie unter Windows (60 s, Backoff 1 → 2 → 5 min, `Retry-After`, Nachrücken, sofort neu bei Popup öffnen/Aufwachen/Entsperren/Netz wieder da). Die Erkennung von Sperre und Standby erfolgt über D-Bus statt `SystemEvents`:

| Ereignis | Quelle |
|---|---|
| Standby / Aufwachen | logind `org.freedesktop.login1.Manager.PrepareForSleep(bool)` (System-Bus) |
| Bildschirm gesperrt / entsperrt | `org.gnome.ScreenSaver.ActiveChanged(bool)` und `org.freedesktop.ScreenSaver.ActiveChanged(bool)` (Session-Bus) |
| Netz wieder da | `NetworkChange.NetworkAvailabilityChanged` (.NET, netlink) |

- Alle D-Bus-Signale sind optional. Fehlt eines, wird das nur geloggt.
- **Rückfall:** Liegen zwei Timer-Ticks (alle 5 s) mehr als 30 s auseinander, war der Rechner im Standby. Dann wird sofort neu abgerufen und der Fehlerzähler zurückgesetzt, auch ohne D-Bus-Signal.

## 4. Daten & Einstellungen

Nach der [XDG Base Directory Specification](https://specifications.freedesktop.org/basedir-spec/):

| Was | Wo |
|---|---|
| Einstellungen | `$XDG_CONFIG_HOME/glei/settings.json` (Standard `~/.config/glei/settings.json`), **gleiches Format** wie unter Windows |
| Log | `$XDG_STATE_HOME/glei/logs/` (Standard `~/.local/state/glei/logs/`), Serilog, rollierend, 7 Tage |
| Single-Instance-Lock und -Socket | `$XDG_RUNTIME_DIR/glei.lock`, `glei.sock` |

- API-Basis-URL wie unter Windows: `GLEI_API_URL`, sonst `appsettings.json` neben der Programmdatei, sonst die öffentliche URL.
- Kein Login, keine Telemetrie. User-Agent `glei-linux/<Version>`.

## 5. API

Keine Änderungen. Die App nutzt dieselben Endpunkte wie die Windows-App (`/api/stops/search`, `/api/departures/{stopId}?line=&direction=&limit=`).

## 6. Architektur

```
linux/
  TriasDeparture.Linux/             Avalonia-App (net10.0), Programmdatei „glei“
    Program.cs                      Velopack-Hooks, Single Instance, Avalonia-Start
    App.axaml                       Fluent-Theme, Logging, Verdrahtung
    SingleInstance.cs               flock-Lockdatei + Unix-Socket (statt Mutex/EventWaitHandle)
    LinuxPaths.cs                   XDG-Ordner
    Data/DesktopIntegration.cs      Autostart- und App-Menü-.desktop-Dateien
    Tray/                           TrayController, IconRenderer, FavoriteMonitor, SessionEvents (D-Bus), InfoWindow
    Popup/                          PopupWindow (+ Platzierung), PopupViewModel
    Shared/ (verlinkt)              aus windows/TriasDeparture.Windows: AppInfo, UpdateService, GleiApiClient,
                                    Settings, Departures/*, TrayPresenter
  TriasDeparture.Linux.Tests/       xUnit + Shouldly: Linux-Teile + die verlinkten Windows-Tests der gemeinsamen Logik
```

- **Gemeinsamer Code:** Die plattformunabhängigen Dateien der Windows-App werden als **verlinkte Dateien** (`<Compile Include="..\..\windows\…" Link="Shared\…" />`) mitkompiliert und behalten ihre Namespaces `TriasDeparture.Windows.*`. Es gibt also eine Quelle für API-Client, Einstellungen, Abfahrtslogik und Tooltip-Texte. `FavoriteMonitor` und `PopupViewModel` sind Kopien, weil sie WPFs `DispatcherTimer` verwenden. Unter Linux kommt Avalonias Timer zum Einsatz, der Monitor hat zusätzlich die Standby-Erkennung über den Zeitsprung.
- **Modelle:** Projektverweis auf `core/TriasDeparture.Domain`, wie unter Windows.
- **Single Instance:** Der erste Start hält eine exklusive Lockdatei (`flock`, wird vom Kernel beim Prozessende freigegeben) und lauscht auf einem Unix-Socket. Ein zweiter Start verbindet sich und öffnet damit das Popup der laufenden Instanz.

## 7. Build & Release

- **GitHub Actions** auf Tag `linux-v*`, z. B. `linux-v0.1.0` ([linux-release.yml](../.github/workflows/linux-release.yml)):
  1. `dotnet test linux/…Tests`
  2. `dotnet publish` (self-contained), je ein Job für `linux-x64` (`ubuntu-latest`) und `linux-arm64` (`ubuntu-24.04-arm`), nacheinander
  3. `vpk pack` → AppImage + Update-Pakete, ein Velopack-Kanal pro Architektur
  4. Upload in ein gemeinsames GitHub Release „glei für Linux …“
- **Auto-Update:** wie unter Windows. Velopack prüft beim Start (nach 30 s) und dann alle 24 h. Ein Update wird im Hintergrund geladen und ersetzt das AppImage beim Beenden.
- Linux- und Windows-Releases sind getrennt (eigene Tags), die Versionen dürfen auseinanderlaufen.

## 8. Nicht im Umfang (v1)

- Wie unter Windows: mehrere Favoriten, „towards“, Benachrichtigungen zu Abfahrten
- Pakete für Distributionen (`.deb`, `.rpm`), Flatpak/Flathub, Snap, AUR
- Natives Wayland (Avalonia läuft über XWayland) und XEmbed-Trays ohne StatusNotifierItem
- macOS

## 9. Offene Punkte

- **Tray ohne StatusNotifierItem-Host** (z. B. reines GNOME ohne Erweiterung): kein Symbol. Die App bleibt über das App-Menü (zweiter Start öffnet das Popup) erreichbar. Eine Erkennung mit Hinweis wäre möglich (`org.kde.StatusNotifierWatcher` auf dem Session-Bus prüfen).
- **Popup-Position unter Wayland:** XWayland kennt die Panels nicht immer. Die Ecke wird dann geraten (siehe 2.2). Manche Compositor ignorieren die gewünschte Position auch ganz.
- **Fokus beim Öffnen:** Je nach Fenstermanager verhindert der Focus-Stealing-Schutz, dass das Popup beim Öffnen aus dem Menü den Fokus bekommt. Dann schließt es nicht automatisch bei Klick daneben, sondern erst mit `Esc` oder einem weiteren Klick.
- **Gemeinsames Projekt:** Statt verlinkter Dateien könnte die gemeinsame Logik in ein eigenes Projekt (z. B. `desktop/TriasDeparture.Desktop`) mit neutralem Namespace wandern. Das ändert die Windows-App und sollte zusammen mit ihr getestet werden.
