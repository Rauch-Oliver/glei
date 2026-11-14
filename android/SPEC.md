# glei Widget (Android) – Spezifikation

Stand: 2026-11-09 · Status: Entwurf

Ein Android-Homescreen-Widget, das für **eine Haltestelle** und **eine Richtung** die **nächsten 3 Abfahrten** in Minuten zeigt. Dazu eine kleine App, in der man das Widget einrichtet. Datenquelle ist die glei API (`api/`).

## 1. Rahmen

| Punkt | Entscheidung |
|---|---|
| Plattform | Android zuerst. iOS später, eigener Ordner `ios/` |
| Nutzer | Nur ich (keine Store-Veröffentlichung) |
| Verteilung | Direkt aus Android Studio / `adb install`, kein Play Store |
| Sprache | Kotlin |
| UI App | Jetpack Compose (Material 3) |
| UI Widget | Jetpack Glance |
| Geräte | Ein Android-Handy |
| Mindestversion | Android 10 (API 29), Ziel: aktuelles SDK |
| Ordner | `android/` (eigenes Gradle-Projekt, unabhängig von der .NET-Solution) |
| Oberfläche | Deutsch |

## 2. Funktionen

### 2.1 Widget

- Zeigt die **nächsten 3 Abfahrten** für die konfigurierte Kombination aus Haltestelle, optional Linie, und Richtung.
- Jede Zeile enthält:
  - die Linie als Badge (`3`, `N5`, `64E`)
  - das Ziel (`Andritz`)
  - die Minuten bis zur Abfahrt (`4 min`, `jetzt` bei 0)
- Gibt es Echtzeitdaten, steht ein Echtzeit-Punkt neben den Minuten. Sonst wird die Plan-Zeit verwendet, ohne Punkt.
- Fällt eine Fahrt aus, wird die Zeile durchgestrichen und mit „fällt aus“ markiert. Sie zählt **nicht** zu den 3 Abfahrten.
- In der Kopfzeile stehen Haltestelle und Richtung, z. B. „Krenngasse → Andritz“ bzw. „Krenngasse · 3er → Andritz“.
- In der Fußzeile steht „Stand 16:04“, die Zeit des letzten erfolgreichen Abrufs.
- **Tippen aufs Widget** lädt neu.
- **Tippen auf die Kopfzeile** öffnet die Konfiguration des Widgets.
- Es können **mehrere Widgets** gleichzeitig existieren, jedes mit eigener Konfiguration (z. B. Hin- und Rückweg).
- Größe: Standard 3×2 Zellen, in der Größe veränderbar. Bei 2×1 wird nur die nächste Abfahrt gezeigt (kompakt).

### 2.2 Konfiguration

Die Konfiguration öffnet sich automatisch beim Platzieren des Widgets (`android:configure`). Ab Android 12 lässt sie sich später erneut öffnen (`reconfigurable`).

1. **Haltestelle suchen:** ein Suchfeld mit Ergebnissen aus `GET /api/stops/search?query=…`. Die Suche startet ab 2 Zeichen, mit 300 ms Debounce.
2. **Linie (optional):** eine Liste der Linien, die an dieser Haltestelle gerade fahren (aus den aktuellen Abfahrten), plus **„Alle Linien“**.
3. **Richtung:**
   - **Mit Linie:** Auswahl zwischen den beiden Richtungen der Linie. Jede Richtung wird mit ihren Zielen beschriftet, z. B. „→ Andritz“ / „→ Puntigam, Lazarettgürtel“, und als `outward`/`inward` gespeichert.
   - **Alle Linien:** Richtung = **Zielhaltestelle**, ausgewählt über dieselbe Haltestellensuche. Angezeigt werden alle Fahrten, die diese Haltestelle später noch anfahren, z. B. „Richtung Jakominiplatz“.
4. **Vorschau:** die aktuellen 3 Abfahrten mit genau diesem Filter, bevor man speichert.
5. **Speichern** setzt das Widget.

Die App ohne Widget, also beim Start über das Launcher-Icon, zeigt eine Liste der eingerichteten Widgets mit Bearbeiten-Funktion, plus einen Hinweis, wie man ein Widget hinzufügt.

## 3. Aktualisierung

Android-Widgets dürfen sich nicht beliebig oft aktualisieren. Darum werden Datenabruf und Anzeige getrennt:

| Was | Wann | Wie |
|---|---|---|
| **Daten abrufen** (Netz) | alle 15 min im Hintergrund, beim Tippen, und beim Neuzeichnen, wenn die Daten älter als 2 min sind | WorkManager (periodisch 15 min; das ist das Android-Minimum) + einmaliger Worker |
| **Minuten neu berechnen** (lokal) | jede Minute, nur bei eingeschaltetem Bildschirm | nicht-weckender `AlarmManager`-Alarm zur vollen Minute → Glance `update()`. Bei Bildschirm aus feuert er nicht, also kein Akkuverbrauch |

- Abgerufen und gespeichert werden **bis zu 10** kommende Abfahrten mit absoluter Zeit (`estimatedTime ?? plannedTime`). Die Minuten rechnet das Widget selbst aus. Das Feld `minutes` der API wird ignoriert, weil es beim Anzeigen schon veraltet sein kann.
- Abfahrten in der Vergangenheit fallen beim Neuzeichnen weg, und es rücken gespeicherte nach.
- Bleiben weniger als 3 Abfahrten übrig, wird sofort neu abgerufen.
- **Offline / Fehler:** Das Widget zeigt die letzten Daten weiter an, ausgegraut, mit „Stand 15:48 · offline“. Gibt es gar keine Daten, steht dort „Keine Verbindung – tippen zum Neuladen“.
- **Keine Abfahrten** (z. B. nachts): „Keine Abfahrten in den nächsten Stunden“.

## 4. Daten & Speicher

Für jedes Widget wird in DataStore unter der `appWidgetId` gespeichert:

```kotlin
data class WidgetConfig(
    val stopId: String,          // "at:46:3171"
    val stopName: String,        // "Krenngasse"
    val line: String?,           // "3" oder null = alle Linien
    val direction: String?,      // "outward" / "inward" (nur mit Linie)
    val directionLabel: String,  // "Andritz" bzw. Name der Zielhaltestelle
    val towardsStopId: String?,  // nur bei "alle Linien"
)

data class WidgetState(
    val departures: List<Departure>,  // bis zu 10, absolute Zeiten (UTC → lokale Zeit)
    val fetchedAt: Instant,
    val error: Boolean,
)
```

- Löscht man ein Widget, werden auch seine Daten gelöscht (`onDeleted`).
- Es gibt keinen Login, keine Analytics, und es werden keine Daten außer dieser Konfiguration gespeichert.

## 5. Benötigte API-Änderungen (`api/`)

Die API kann bisher nur nach Linie filtern. Für das Widget kommt hinzu:

```
GET /api/departures/{stopId}?line=3&direction=outward&towards=at:46:xxxx&limit=10
```

| Parameter | Bedeutung |
|---|---|
| `line` | optional, wie `/line/{line}` |
| `direction` | optional, `outward` oder `inward`, nur zusammen mit `line` sinnvoll |
| `towards` | optional, Stop-ID: nur Fahrten, die diese Haltestelle später noch anfahren (braucht Onward Calls) |
| `limit` | optional, Standard: alle |

- **Core:** Der Parser liest bei den Onward Calls zusätzlich die **Stop-IDs** (`StopPointRef`), nicht nur die Namen. Für `towards` wird nach ID gefiltert, nicht per Namensvergleich.
- **Bestehende Endpunkte bleiben**, die neuen Parameter sind rein additiv.
- Abfahrten mit `cancelled: true` werden weiter geliefert. Gefiltert wird im Widget.

## 6. Architektur (App)

```
android/
  app/
    data/        ApiClient (Ktor + kotlinx.serialization), Repository, DataStore
    widget/      GleiWidget (Glance), GleiWidgetReceiver, RefreshWorker, MinuteTicker
    config/      ConfigActivity + Compose-Screens (Suche, Linie, Richtung, Vorschau)
    ui/          Theme (Material You)
```

- **Farben:** Material You (dynamische Farben aus dem Hintergrundbild, `GlanceTheme` + `dynamicColorScheme`), hell/dunkel nach System. Die Fallback-Farben, falls keine dynamischen Farben verfügbar sind, kommen aus dem Alexa-Icon: Petrol #14897A, Gelb #FBBF24.

- Die API-Basis-URL kommt aus `local.properties` → `BuildConfig.API_BASE_URL` (Cloud-Run-URL des Service `glei`), nicht im Code fest.
- Tests: Unit-Tests für Filter, Minuten-Berechnung und Nachrücken von Abfahrten; ein Compose-UI-Test für den Konfigurationsablauf.

## 7. Nicht im Umfang (v1)

- iOS (folgt mit WidgetKit, `ios/`)
- Play-Store-Veröffentlichung
- Benachrichtigungen („Dein 3er fährt in 5 min“)
- Mehrere Haltestellen in einem Widget
- Fahrplanauskunft / Routing

## 8. Offene Punkte

- **Rate Limiting** der öffentlichen API: Für einen Nutzer ist das unkritisch (ein Widget ruft bei offenem Bildschirm höchstens alle 2 min ab). Vor einer Veröffentlichung nachziehen.
- **Kompakte 2×1-Variante:** Erst einbauen, wenn 3×2 steht.
