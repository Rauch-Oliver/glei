import { useEffect, useMemo, useState } from 'preact/hooks';
import { getDepartures, type Departure } from './api';
import { groupByLine, minutesUntil, type LineGroup } from './board';
import { saveMonitor } from './storage';
import { href, shortName } from './route';
import { LineBadge } from './LineBadge';

interface Props { stopId: string; name: string; keys: string[] }

const summarize = (groups: LineGroup[], keys: string[]) =>
  groups.filter(g => keys.includes(g.key)).map(g => `${g.line} → ${g.label}`).join(', ');

/** Step 2: pick which lines and directions the monitor shows. */
export function LinePicker({ stopId, name, keys: initial }: Props) {
  const [deps, setDeps] = useState<Departure[] | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [selected, setSelected] = useState<Set<string>>(() => new Set(initial));

  useEffect(() => {
    const ctrl = new AbortController();
    getDepartures(stopId, ctrl.signal).then(setDeps).catch(e => { if (e.name !== 'AbortError') setError(e.message); });
    return () => ctrl.abort();
  }, [stopId]);

  const now = Date.now();
  const groups = useMemo(() => (deps ? groupByLine(deps, now) : []), [deps]);
  // Keys saved earlier whose line doesn't run right now (e.g. at night) stay selected.
  const missing = [...selected].filter(k => !groups.some(g => g.key === k));

  const toggle = (k: string) => setSelected(s => {
    const n = new Set(s);
    if (n.has(k)) n.delete(k); else n.add(k);
    return n;
  });

  const keys = [...selected];
  const open = () => { location.hash = href('monitor', stopId, name, keys); };
  const save = () => {
    saveMonitor({ stopId, stopName: name, keys, summary: summarize(groups, keys) });
    open();
  };

  return (
    <main class="page">
      <nav class="crumbs"><a href="#/">← Zurück</a></nav>
      <section class="card">
        <h1 class="stop-title">{shortName(name)}</h1>
        <p class="muted">Welche Linien und Richtungen soll dein Monitor zeigen? Nichts gewählt = alle.</p>

        {error && <p class="error">{error}</p>}
        {!deps && !error && <p class="muted">Lade Abfahrten …</p>}
        {deps && groups.length === 0 && <p class="muted">Gerade keine Abfahrten an dieser Haltestelle.</p>}

        <ul class="list">
          {groups.map(g => (
            <li key={g.key}>
              <label class={selected.has(g.key) ? 'pick on' : 'pick'}>
                <input type="checkbox" checked={selected.has(g.key)} onChange={() => toggle(g.key)} />
                <LineBadge line={g.line} mode={g.mode} />
                <span class="dest">→ {g.label}</span>
                <span class="mins">{g.next.map(d => minutesUntil(d, now)).join(' · ')} min</span>
              </label>
            </li>
          ))}
        </ul>
        {missing.length > 0 && (
          <p class="muted small">Außerdem gewählt (fährt gerade nicht): {missing.map(k => k.replace('|', ' ')).join(', ')}</p>
        )}

        <div class="actions">
          {groups.length > 1 && (
            <button class="btn ghost" onClick={() => setSelected(selected.size ? new Set() : new Set(groups.map(g => g.key)))}>
              {selected.size ? 'Auswahl leeren' : 'Alle wählen'}
            </button>
          )}
          <span class="spacer" />
          <button class="btn ghost" onClick={open}>Anzeigen</button>
          <button class="btn" onClick={save}>★ Speichern & anzeigen</button>
        </div>
      </section>
    </main>
  );
}
