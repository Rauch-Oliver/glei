# glei für Linux

Eine kleine App im Panel (Tray), wie [glei für Windows](../windows/README.md).

- Das **Symbol** zeigt die Minuten bis zur nächsten Abfahrt deines Favoriten (Haltestelle, Linie und Richtung). Ein gelber Punkt heißt: Echtzeit.
- Beim **Darüberfahren** siehst du die nächsten 3 Abfahrten (sofern das Panel Tooltips anzeigt).
- Ein **Klick** (bzw. „Öffnen“ im Menü) öffnet ein Fenster mit deinem Favoriten und einer Suche nach anderen Haltestellen.

Die Daten kommen von der glei API (`api/`). Die Spezifikation steht in [SPEC.md](SPEC.md).

## Voraussetzungen

- Ein Panel mit **StatusNotifierItem/AppIndicator**-Unterstützung:
  - **GNOME:** Erweiterung [AppIndicator and KStatusNotifierItem Support](https://extensions.gnome.org/extension/615/appindicator-support/). Bei Ubuntu ist sie schon dabei und aktiv.
  - **KDE Plasma, Xfce, Cinnamon, MATE, Budgie:** funktioniert ohne Weiteres.
- Für das AppImage: **FUSE 2** (`sudo apt install libfuse2t64`, auf älteren Ubuntus `libfuse2`).
- x64 oder ARM64. Läuft unter X11 und Wayland (über XWayland).

## Installieren

1. Unter [Releases](https://github.com/geisi93/glei/releases) die neueste Version **„glei für Linux …“** öffnen und das `.AppImage` für deine Architektur laden (`linux-x64` bzw. `linux-arm64`).
2. Ausführbar machen und starten:
   ```bash
   chmod +x glei*.AppImage
   ./glei*.AppImage
   ```
   Am besten vorher an einen festen Ort legen (z. B. `~/Applications/`) – Autostart und Menüeintrag zeigen auf genau diese Datei.
3. glei legt beim ersten Start einen Eintrag im App-Menü an, startet ab dann bei jeder Anmeldung mit, und öffnet einmalig gleich das Fenster, damit du deine Haltestelle wählen kannst. Updates lädt es selbst aus GitHub Releases und spielt sie beim nächsten Start ein.

**Kein Symbol zu sehen?** Unter GNOME ohne die AppIndicator-Erweiterung gibt es keinen Tray. Erweiterung installieren/aktivieren, abmelden und wieder anmelden.

## Bedienung

| Aktion | Was passiert |
|---|---|
| Klick aufs Symbol | Fenster öffnen/schließen. Öffnet der Klick stattdessen das Menü (je nach Panel, z. B. Ubuntu): dort **Öffnen** |
| glei nochmal starten (App-Menü) | Öffnet das Fenster der laufenden App |
| Lostippen | Haltestelle suchen. Mit ↓ in die Ergebnisse, `Enter` öffnet die Haltestelle |
| ☆ bei einer Linie | Diese Linie in diese Richtung wird dein Favorit |
| `Esc` | Einen Schritt zurück, beim zweiten Mal Fenster schließen |
| Menü | Öffnen · Aktualisieren · Beim Anmelden starten · Info · Beenden |

Gespeichert wird nur in `~/.config/glei/settings.json` (Favorit, letzte Haltestellen; gleiches Format wie unter Windows). Das Log liegt in `~/.local/state/glei/logs/`. Eine installierte Version legt außerdem `~/.config/autostart/glei.desktop`, `~/.local/share/applications/glei.desktop` und das Icon unter `~/.local/share/icons/hicolor/256x256/apps/glei.png` an.

**Deinstallieren:** glei beenden, das AppImage und die oben genannten Dateien löschen.

## Entwickeln

```bash
dotnet run --project linux/TriasDeparture.Linux
dotnet test linux/TriasDeparture.Linux.Tests
```

Eine lokale Version zum Testen, ohne AppImage:

```bash
dotnet publish linux/TriasDeparture.Linux -c Release -r linux-x64 --self-contained false -o linux/publish
linux/publish/glei
```

- Andere API (z. B. lokal): Umgebungsvariable `GLEI_API_URL=http://localhost:5199/` setzen, oder `ApiBaseUrl` in `appsettings.json` neben der Programmdatei ändern.
- Eine lokale Version (kein AppImage von Velopack) trägt sich **nicht** in Autostart und App-Menü ein und sucht keine Updates.
- Zum Ausprobieren ohne die eigenen Einstellungen: `XDG_CONFIG_HOME=/tmp/glei-cfg XDG_STATE_HOME=/tmp/glei-state dotnet run --project linux/TriasDeparture.Linux`.

### Gemeinsamer Code mit Windows

Die plattformunabhängigen Teile der Windows-App (API-Client, Einstellungen, Abfahrtslogik, Tooltip-Texte, Updates) werden **nicht kopiert**, sondern als verlinkte Dateien mitkompiliert (siehe `TriasDeparture.Linux.csproj`). Änderungen dort gelten für beide Apps; die Windows-Tests dazu laufen auch in `TriasDeparture.Linux.Tests` mit.

## Release

```bash
git tag linux-v0.1.0
git push origin linux-v0.1.0
```

GitHub Actions ([linux-release.yml](../.github/workflows/linux-release.yml)) testet, baut x64 und ARM64 (self-contained), packt beides mit [Velopack](https://velopack.io) als AppImage und legt das GitHub Release an. Die Version kommt aus dem Tag.
