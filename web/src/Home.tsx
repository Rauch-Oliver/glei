import { useEffect, useRef, useState } from 'preact/hooks';
import { searchStops, type Stop } from './api';
import { addRecent, loadMonitors, loadRecent, monitorId, removeMonitor, type SavedMonitor } from './storage';
import { href, shortName } from './route';

export function Home() {
  const [query, setQuery] = useState('');
  const [results, setResults] = useState<Stop[] | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [active, setActive] = useState(0);
  const [monitors, setMonitors] = useState(loadMonitors);
  const recent = loadRecent();
  const input = useRef<HTMLInputElement>(null);

  useEffect(() => { input.current?.focus(); }, []);

  // Debounced search from 2 characters on.
  useEffect(() => {
    const q = query.trim();
    if (q.length < 2) { setResults(null); setError(null); return; }
    const ctrl = new AbortController();
    const t = setTimeout(() => {
      searchStops(q, ctrl.signal)
        .then(r => { setResults(r); setActive(0); setError(null); })
        .catch(e => { if (e.name !== 'AbortError') setError(e.message); });
    }, 300);
    return () => { clearTimeout(t); ctrl.abort(); };
  }, [query]);

  const choose = (s: Stop) => {
    addRecent(s);
    location.hash = href('stop', s.id, s.name);
  };

  const onKey = (e: KeyboardEvent) => {
    if (!results?.length) return;
    if (e.key === 'ArrowDown') { setActive(i => Math.min(i + 1, results.length - 1)); e.preventDefault(); }
    if (e.key === 'ArrowUp') { setActive(i => Math.max(i - 1, 0)); e.preventDefault(); }
    if (e.key === 'Enter') choose(results[active]);
  };

  const remove = (m: SavedMonitor) => { removeMonitor(m); setMonitors(loadMonitors()); };

  return (
    <main class="page">
      <header class="brand">
        <span class="h-sign" aria-hidden="true">H</span>
        <div>
          <h1>glei</h1>
          <p>Dein Haltestellenmonitor für Bus und Bim in der Steiermark</p>
        </div>
      </header>

      <section class="card">
        <label class="field">
          <span>Haltestelle suchen</span>
          <input ref={input} type="search" placeholder="z. B. Jakominiplatz" value={query}
            onInput={e => setQuery(e.currentTarget.value)} onKeyDown={onKey} autocomplete="off" />
        </label>
        {error && <p class="error">{error}</p>}
        {results && results.length === 0 && <p class="muted">Keine Haltestelle gefunden.</p>}
        {results && results.length > 0 && (
          <ul class="list" role="listbox">
            {results.map((s, i) => (
              <li key={s.id}>
                <button class={i === active ? 'item active' : 'item'} onClick={() => choose(s)}>{s.name}</button>
              </li>
            ))}
          </ul>
        )}
        {!results && recent.length > 0 && (
          <>
            <h2 class="sub">Zuletzt gesucht</h2>
            <div class="chips">
              {recent.map(s => <button key={s.id} class="chip" onClick={() => choose(s)}>{shortName(s.name)}</button>)}
            </div>
          </>
        )}
      </section>

      {monitors.length > 0 && (
        <section class="card">
          <h2>Meine Monitore</h2>
          <ul class="list">
            {monitors.map(m => (
              <li key={monitorId(m)} class="saved">
                <a class="item" href={href('monitor', m.stopId, m.stopName, m.keys)}>
                  <strong>{shortName(m.stopName)}</strong>
                  <small>{m.summary || 'Alle Linien'}</small>
                </a>
                <a class="icon-btn" title="Bearbeiten" href={href('stop', m.stopId, m.stopName, m.keys)}>✎</a>
                <button class="icon-btn" title="Entfernen" onClick={() => remove(m)}>✕</button>
              </li>
            ))}
          </ul>
        </section>
      )}

      <footer class="foot">
        Daten: <a href="https://www.verbundlinie.at" target="_blank" rel="noopener">Verbund Linie</a> (TRIAS) ·
        Deine Monitore werden nur in diesem Browser gespeichert.
        <br /><a href="#/impressum">Impressum</a> · <a href="#/datenschutz">Datenschutz</a>
      </footer>
    </main>
  );
}
