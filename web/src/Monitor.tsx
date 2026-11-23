import { useEffect, useState } from 'preact/hooks';
import { getDepartures, type Departure } from './api';
import { clock, effectiveTime, groupKey, minutesUntil, upcoming } from './board';
import { href, shortName } from './route';

interface Props { stopId: string; name: string; keys: string[] }

const REFRESH_MS = 30_000;
const ROWS = 8;

/** The display itself, styled after the dot-matrix monitors at Graz stops. */
export function Monitor({ stopId, name, keys }: Props) {
  const [deps, setDeps] = useState<Departure[] | null>(null);
  const [updated, setUpdated] = useState<number | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [now, setNow] = useState(Date.now());

  // Poll while the tab is visible; refresh immediately when it becomes visible again.
  useEffect(() => {
    let timer: number | undefined;
    let ctrl: AbortController | undefined;
    const load = async () => {
      clearTimeout(timer);
      ctrl?.abort();
      ctrl = new AbortController();
      try {
        const d = await getDepartures(stopId, ctrl.signal);
        setDeps(d); setUpdated(Date.now()); setError(null);
      } catch (e) {
        if ((e as Error).name === 'AbortError') return;
        setError((e as Error).message);
      }
      if (!document.hidden) timer = setTimeout(load, REFRESH_MS);
    };
    const onVisible = () => { if (!document.hidden) load(); else clearTimeout(timer); };
    load();
    document.addEventListener('visibilitychange', onVisible);
    return () => { clearTimeout(timer); ctrl?.abort(); document.removeEventListener('visibilitychange', onVisible); };
  }, [stopId]);

  // Minutes are recomputed locally from the absolute times.
  useEffect(() => {
    const t = setInterval(() => setNow(Date.now()), 5_000);
    return () => clearInterval(t);
  }, []);

  // Keep the screen on while the monitor is shown (e.g. on a tablet at the door).
  useEffect(() => {
    let lock: WakeLockSentinel | undefined;
    const request = () => navigator.wakeLock?.request('screen').then(l => { lock = l; }).catch(() => {});
    request();
    const onVisible = () => { if (!document.hidden) request(); };
    document.addEventListener('visibilitychange', onVisible);
    return () => { lock?.release(); document.removeEventListener('visibilitychange', onVisible); };
  }, []);

  const rows = deps
    ? upcoming(deps, now).filter(d => keys.length === 0 || keys.includes(groupKey(d))).slice(0, ROWS)
    : [];
  const stale = error && updated;

  const fullscreen = () => {
    if (document.fullscreenElement) document.exitFullscreen();
    else document.documentElement.requestFullscreen?.().catch(() => {});
  };

  return (
    <main class="monitor-page">
      <nav class="monitor-tools">
        <a href="#/" title="Startseite">←</a>
        <a href={href('stop', stopId, name, keys)} title="Linien wählen">✎</a>
        <button onClick={fullscreen} title="Vollbild">⛶</button>
      </nav>

      <div class="post">
        <div class="frame">
          <div class={stale ? 'display stale' : 'display'}>
            <div class="d-head">
              <span class="d-stop">{shortName(name)}</span>
              <span class="d-clock">{clock(now)}</span>
            </div>
            <div class="d-rows">
              {!deps && !error && <div class="d-msg">Lade …</div>}
              {error && !deps && <div class="d-msg">{error}</div>}
              {deps && rows.length === 0 && <div class="d-msg">Derzeit keine Abfahrten</div>}
              {rows.map(d => <Row key={`${d.line}${d.plannedTime}${d.destination}`} d={d} now={now} />)}
            </div>
          </div>
        </div>
        <div class="signs">
          <span class="h-sign big" aria-hidden="true">H</span>
          <span class="sign-name">{shortName(name)}</span>
        </div>
      </div>

      <p class="monitor-foot">
        {stale ? `offline · Stand ${clock(updated!)}` : updated ? `Stand ${clock(updated)} · aktualisiert alle 30 s` : ''}
        {' · '}<span class="rt-dot" /> Echtzeit
        {' · '}<a href="#/impressum">Impressum</a>
      </p>
    </main>
  );
}

function Row({ d, now }: { d: Departure; now: number }) {
  const min = minutesUntil(d, now);
  const realtime = d.estimatedTime !== null;
  const time = d.cancelled ? 'fällt aus'
    : min === 0 ? 'jetzt'
    : min >= 60 ? clock(effectiveTime(d))
    : `${min}'`;
  return (
    <div class={d.cancelled ? 'd-row cancelled' : 'd-row'}>
      <span class="d-line">{d.line}</span>
      <span class="d-dest">{d.destination}</span>
      <span class={min === 0 && !d.cancelled ? 'd-min blink' : 'd-min'}>
        {time}
        {/* Always reserve the dot's slot so minutes line up whether or not there's realtime data. */}
        <span class={realtime && !d.cancelled ? 'rt-dot' : 'rt-dot off'} title={realtime ? 'Echtzeit' : 'Fahrplanzeit'} />

      </span>
    </div>
  );
}
