# glei für Windows

Eine kleine App im Infobereich der Taskleiste.

- Das **Symbol** zeigt die Minuten bis zur nächsten Abfahrt deines Favoriten (Haltestelle, Linie und Richtung). Ein gelber Punkt heißt: Echtzeit.
- Beim **Darüberfahren** siehst du die nächsten 3 Abfahrten.
- Ein **Klick** öffnet ein Fenster mit deinem Favoriten und einer Suche nach anderen Haltestellen.

Die Daten kommen von der glei API (`api/`). Die Spezifikation steht in [SPEC.md](SPEC.md).

## Installieren

1. Unter [Releases](https://github.com/geisi93/glei/releases) die neueste Version **„glei für Windows …“** öffnen und `glei-win-x64-Setup.exe` laden (bzw. `win-arm64` für ARM-Geräte).
2. Starten. Weil die App nicht signiert ist, warnt Windows SmartScreen: **Weitere Informationen → Trotzdem ausführen**.
3. glei installiert sich pro Benutzer (keine Admin-Rechte) und startet sofort. Updates lädt es selbst aus GitHub Releases und spielt sie beim nächsten Start ein.

**Symbol dauerhaft anzeigen:** Windows 11 versteckt neue Symbole im Überlauf (^). Zum Anheften: *Einstellungen → Personalisierung → Taskleiste → Weitere Symbole der Taskleiste → glei: Ein*.

## Bedienung

| Aktion | Was passiert |
|---|---|
| Klick aufs Symbol | Fenster öffnen/schließen |
| Lostippen | Haltestelle suchen. Mit ↓ in die Ergebnisse, `Enter` öffnet die Haltestelle |
| ☆ bei einer Linie | Diese Linie in diese Richtung wird dein Favorit |
| `Esc` | Einen Schritt zurück, beim zweiten Mal Fenster schließen |
| Rechtsklick | Aktualisieren · Mit Windows starten · Info · Beenden |

Gespeichert wird nur in `%APPDATA%\glei\settings.json` (Favorit, letzte Haltestellen). Das Log liegt in `%APPDATA%\glei\logs\`.

## Entwickeln

```bash
dotnet run --project windows/TriasDeparture.Windows
dotnet test windows/TriasDeparture.Windows.Tests
```

Eine lokale Version zum Testen, ohne Installer:

```bash
dotnet publish windows/TriasDeparture.Windows -c Release -r win-x64 --self-contained false -o windows/publish
windows/publish/glei.exe
```

- Andere API (z. B. lokal): Umgebungsvariable `GLEI_API_URL=http://localhost:5199/` setzen, oder `ApiBaseUrl` in `appsettings.json` neben der exe ändern.
- Eine lokale Version (nicht installiert) trägt sich **nicht** in den Autostart ein und sucht keine Updates.

## Release

```bash
git tag windows-v0.1.0
git push origin windows-v0.1.0
```

GitHub Actions ([windows-release.yml](../.github/workflows/windows-release.yml)) testet, baut x64 und ARM64 (self-contained), packt beides mit [Velopack](https://velopack.io) und legt das GitHub Release mit den Setup-Dateien an. Die Version kommt aus dem Tag.
