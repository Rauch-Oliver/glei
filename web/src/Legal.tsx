import { contactUrl, operator, sourceUrl } from './operator';

const Contact = () => <a href={contactUrl} target="_blank" rel="noopener">{contactUrl.replace('https://', '')}</a>;

export function Impressum() {
  return (
    <main class="page legal">
      <nav class="crumbs"><a href="#/">← Zurück</a></nav>
      <section class="card">
        <h1>Impressum</h1>
        <p class="muted small">Offenlegung gemäß § 25 Mediengesetz und Informationen gemäß § 5 E-Commerce-Gesetz</p>

        <h2>Medieninhaber und Betreiber</h2>
        <p>{operator.name}<br />{operator.place}<br />Kontakt: <Contact /></p>

        <h2>Grundlegende Richtung</h2>
        <p>Private, nicht-kommerzielle Website, die Abfahrtszeiten des öffentlichen Verkehrs in der Steiermark anzeigt.</p>

        <h2>Datenquelle</h2>
        <p>
          Fahrplan- und Echtzeitdaten: <a href="https://www.verbundlinie.at" target="_blank" rel="noopener">Verbund Linie</a> (Steirischer
          Verkehrsverbund), TRIAS-Schnittstelle. glei ist kein Angebot der Verbund Linie oder der Holding Graz.
        </p>

        <h2>Haftung</h2>
        <p>
          Alle Angaben ohne Gewähr. Abfahrtszeiten können von der Wirklichkeit abweichen. Für Inhalte verlinkter
          externer Seiten sind ausschließlich deren Betreiber verantwortlich.
        </p>

        <h2>Quellcode</h2>
        <p><a href={sourceUrl} target="_blank" rel="noopener">{sourceUrl.replace('https://', '')}</a> (MIT-Lizenz)</p>
      </section>
    </main>
  );
}

export function Datenschutz() {
  return (
    <main class="page legal">
      <nav class="crumbs"><a href="#/">← Zurück</a></nav>
      <section class="card">
        <h1>Datenschutz</h1>

        <h2>Kurz gesagt</h2>
        <p>Keine Cookies, kein Tracking, kein Konto. Deine Monitore bleiben in deinem Browser.</p>

        <h2>Verantwortlicher</h2>
        <p>{operator.name}, {operator.place}, Kontakt: <Contact /></p>

        <h2>Hosting und Server-Logs</h2>
        <p>
          Die Website läuft auf Google Cloud Run (Google Cloud EMEA Ltd., Rechenzentrum in Belgien). Beim Aufruf
          verarbeitet der Server deine IP-Adresse, Zeitpunkt, aufgerufene Adresse und Browserkennung. Das ist nötig, um
          die Seite auszuliefern, Missbrauch zu begrenzen (Anfragen pro IP-Adresse) und Fehler zu finden. Die Protokolle
          werden nach höchstens 30 Tagen gelöscht. Rechtsgrundlage ist unser berechtigtes Interesse am sicheren Betrieb
          (Art. 6 Abs. 1 lit. f DSGVO). Mit Google besteht ein Auftragsverarbeitungsvertrag; eine Übermittlung in die
          USA ist nicht ausgeschlossen und durch das EU-US Data Privacy Framework abgesichert.
        </p>

        <h2>Abfrage der Abfahrten</h2>
        <p>
          Suchbegriffe und Haltestellen leitet unser Server an die Schnittstelle der Verbund Linie weiter. Deine
          IP-Adresse wird dabei nicht weitergegeben.
        </p>

        <h2>Speicherung in deinem Browser</h2>
        <p>
          Gespeicherte Monitore und zuletzt gesuchte Haltestellen liegen im lokalen Speicher (localStorage) deines
          Browsers. Sie werden nie an uns übertragen. Du löschst sie über „Entfernen“ oder indem du die Website-Daten in
          deinem Browser löschst. Diese Speicherung ist für die von dir gewünschte Funktion erforderlich (§ 165 Abs. 3
          TKG 2021) und braucht keine Einwilligung.
        </p>

        <h2>Schriften und Drittanbieter</h2>
        <p>Alle Dateien kommen von unserem Server. Es werden keine externen Schriften, Skripte oder Analysedienste geladen.</p>

        <h2>Deine Rechte</h2>
        <p>
          Du hast das Recht auf Auskunft, Berichtigung, Löschung, Einschränkung der Verarbeitung und Widerspruch. Schreib
          dazu über <Contact />. Du kannst dich auch bei der österreichischen{' '}
          <a href="https://www.dsb.gv.at" target="_blank" rel="noopener">Datenschutzbehörde</a> beschweren.
        </p>
      </section>
    </main>
  );
}
