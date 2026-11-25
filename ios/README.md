# glei für iOS

Eine iPhone-App mit deinen Haltestellen, und ein Widget für den Homescreen.

- In der **App** heftest du Haltestellen an: alle Linien, oder eine Linie in eine Richtung (z. B. „3er → Andritz“). Jede ist eine Karte mit den nächsten 3 Abfahrten, live jede Minute.
- Jedes **Widget** zeigt eine davon. Für mehrere Haltestellen legst du mehrere Widgets an.

Die Daten kommen von der glei API (`api/`). Die Spezifikation steht in [SPEC.md](SPEC.md).

## Voraussetzungen

- Ein Mac mit **Xcode 16** oder neuer
- Ein iPhone mit **iOS 17** oder neuer
- Eine **Apple-ID** in Xcode (*Xcode → Settings → Accounts*). Ein kostenloser Account reicht. Damit läuft die App allerdings nur 7 Tage, danach neu aus Xcode installieren. Mit dem bezahlten Developer Program ein Jahr.

## Einrichten

1. Deine **Team-ID** herausfinden: *Xcode → Settings → Accounts → (deine Apple-ID) → Team*. Oder im [Developer-Konto](https://developer.apple.com/account) unter *Membership*.
2. Die Datei `ios/Local.xcconfig` anlegen. Sie wird nicht eingecheckt:
   ```
   DEVELOPMENT_TEAM = ABCDE12345
   ```
   > Das Team **nicht** unter *Signing & Capabilities* auswählen: Xcode würde es in `project.pbxproj` schreiben, und es landet im Git.
3. `ios/Glei.xcodeproj` öffnen, das iPhone wählen, Scheme `Glei` ausführen (`⌘R`).
   - Beim ersten Mal auf dem iPhone: *Einstellungen → Datenschutz & Sicherheit → Entwicklermodus* einschalten, und unter *Allgemein → VPN & Geräteverwaltung* dem eigenen Zertifikat vertrauen.
4. **Sind die IDs schon vergeben?** Xcode meldet dann einen Fehler bei der Bundle-ID oder der App Group. Dann eigene verwenden:
   - Bundle-IDs (`at.glei.app`, `at.glei.app.widget`): in Xcode bei den Targets *Glei* und *GleiWidget* unter *General*.
   - App Group (`group.at.glei.app`): in `Shared/Glei.swift` und in beiden `.entitlements`-Dateien.

## Bedienung

| Aktion | Was passiert |
|---|---|
| ＋ | Haltestelle suchen, dann „Alle Linien“ oder eine Linie wählen. Heftet sie als Karte an |
| Liste nach unten ziehen | Alle Karten und die Widgets aktualisieren |
| Karte nach links wischen oder lange drücken | „Entfernen“ |
| Homescreen lange drücken → ＋ → glei | Widget hinzufügen (klein oder mittel). Zeigt zuerst die erste angeheftete Haltestelle |
| Widget lange drücken → „Widget bearbeiten“ | Andere angeheftete Haltestelle wählen |
| Tippen aufs Widget | Öffnet die App |

- Das Widget ruft die Abfahrten ab und zählt die Minuten danach selbst herunter. Neue Daten holt es nach spätestens 15 Minuten, bei vielen Abfahrten früher. Wie oft genau, entscheidet iOS.
- Gespeichert wird nur die Liste der angehefteten Haltestellen (auf dem iPhone, in der App Group).

## Entwickeln

- Projektaufbau:

  | Datei | Inhalt |
  |---|---|
  | `Shared/Glei.swift` | App + Widget: Modelle, Pins, API-Aufrufe, Anzeigeregeln |
  | `Glei/GleiApp.swift` | App: Karten, Suche, Linienauswahl |
  | `GleiWidget/GleiWidget.swift` | Widget: Pin-Auswahl, Timeline, Ansicht |
  | `GleiTests/GleiTests.swift` | Tests |

- Tests: `⌘U` in Xcode, oder
  ```bash
  xcodebuild -project ios/Glei.xcodeproj -scheme Glei -destination 'platform=iOS Simulator,name=iPhone Air' test
  ```
- **Simulator:** Widget hinzufügen wie am iPhone. Beim Bauen ohne Signierung (`CODE_SIGNING_ALLOWED=NO`) bleibt das Widget leer, weil iOS seiner Konfiguration ohne Signatur nicht traut. Aus Xcode heraus passiert das nicht.
- **Andere API** (z. B. lokal): `apiBase` in `Shared/Glei.swift` ändern.
- Keine Abhängigkeiten, kein Projektgenerator: neue Dateien in Xcode zum passenden Target hinzufügen. `Shared/` gehört zu beiden (*Glei* und *GleiWidget*).
