# glei

Live-Abfahrten für Bus und Bim in der Steiermark („Der 3er kommt glei.“) auf Basis der [TRIAS-Schnittstelle](https://www.verbundlinie.at/de/kundenservice/weitere-infostellen/faqs-hilfe/faq-zur-ogd-service-schnittstelle-trias) der Verbund Linie.

## Aufbau

| Ordner | Inhalt |
|--------|--------|
| [`core/`](core) | Gemeinsamer TRIAS-Code: Modelle, Services, TRIAS-Client (XML-Builder/-Parser, Caching) + Tests |
| [`api/`](api) | REST API – [Doku](api/README.md) |
| [`web/`](web) | Web-App: Haltestellenmonitor im Stil der Grazer Anzeigen (Preact + Vite), wird von der API unter `/` ausgeliefert |
| [`alexa/`](alexa) | Alexa-Skill („Alexa, frag Bim Graz …“) – [Einrichtung](alexa/README.md) |
| [`windows/`](windows) | Tray-App für Windows: Minuten bis zur nächsten Abfahrt im Infobereich – [Doku](windows/README.md) |
| [`linux/`](linux) | Dieselbe Tray-App für Linux (Avalonia, AppImage) – [Doku](linux/README.md) |

```
api/TriasDeparture.Api            (Controller, Program.cs, Swagger)
  │
  ▼
core/TriasDeparture.Application   (DepartureService)
  │
  ▼
core/TriasDeparture.Infrastructure (TriasHttpClient, XmlBuilder, XmlParser)
  │
  ▼
TRIAS (Verbund Linie)
```

### Caching (core)

| Daten | Cache-Key | TTL |
|-------|-----------|-----|
| Haltestellen | `stops:{query}` | 24 Stunden |
| Abfahrten | `departures:{stopId}` | 15 Sekunden |

## Entwicklung

```bash
dotnet build
dotnet test
```

Voraussetzung: .NET 10 SDK. Tests: xUnit + Shouldly.

Web-App (Node 20+), `/api` wird an die öffentliche API weitergeleitet (`GLEI_API=http://localhost:5000` für eine lokale):

```bash
cd web
npm install
npm run dev
```

## Lizenz

[MIT](LICENSE)
