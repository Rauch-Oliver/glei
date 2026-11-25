# glei (iOS)

App mit angehefteten Haltestellen und ein Homescreen-Widget dazu. Daten von der glei API (`api/`).

- **App:** Haltestellen anheften, alle Linien oder eine Linie in eine Richtung. Jede ist eine Karte mit den nächsten 3 Abfahrten, live jede Minute. Mit `+` suchen und anheften, zum Entfernen die Karte nach links wischen.
- **Widget:** zeigt eine der angehefteten Haltestellen. Welche, stellt man unter „Widget bearbeiten“ ein. Für mehrere Haltestellen legt man mehrere Widgets an.

App und Widget teilen sich die Pins über die App Group `group.at.glei.app`.

| Datei | Inhalt |
|---|---|
| `Shared/Glei.swift` | Modelle, Pins, API-Aufrufe, Anzeigeregeln (App + Widget) |
| `Glei/GleiApp.swift` | App: Karten, Suche, Linienauswahl |
| `GleiWidget/GleiWidget.swift` | Widget: Pin-Auswahl, Timeline, Ansicht |

## Starten

1. `ios/Glei.xcodeproj` in Xcode öffnen (iOS 17+).
2. Bei `Glei` und `GleiWidget` unter *Signing & Capabilities* das eigene Team wählen. Xcode registriert dann die App Group. Sind die IDs `at.glei.app` bzw. `group.at.glei.app` schon vergeben, eigene nehmen. Die Gruppe muss dann auch in `Shared/Glei.swift` und in beiden `.entitlements`-Dateien geändert werden.
3. Scheme `Glei` ausführen, in der App Haltestellen anheften.
4. Homescreen lange drücken → „+“ → glei → Widget hinzufügen. Es zeigt zuerst den ersten Pin. Ändern unter „Widget bearbeiten“.

## Aktualisierung

Das Widget ruft die API einmal ab und rechnet die Minuten danach jede Minute selbst neu. Nach 15 Minuten wird neu abgerufen, nach einem Fehler nach 5. Wie oft iOS Widgets tatsächlich aktualisiert, entscheidet das System. Ändern sich die Pins in der App, werden die Widgets sofort neu geladen.

Tests: `⌘U` in Xcode.
