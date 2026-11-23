// Everything user-specific lives in this browser's localStorage only.

export interface SavedMonitor {
  stopId: string;
  stopName: string;
  /** Selected line|direction keys; empty = all lines. */
  keys: string[];
  /** Human-readable summary, e.g. "3 → Andritz, 60 → Lustbühel". */
  summary: string;
}

export interface RecentStop { id: string; name: string }

function read<T>(key: string, fallback: T): T {
  try {
    const raw = localStorage.getItem(key);
    return raw ? (JSON.parse(raw) as T) : fallback;
  } catch { return fallback; }
}

function write(key: string, value: unknown) {
  try { localStorage.setItem(key, JSON.stringify(value)); } catch { /* private mode etc. */ }
}

const MONITORS = 'glei.monitors', RECENT = 'glei.recentStops';

export const loadMonitors = () => read<SavedMonitor[]>(MONITORS, []);

export const monitorId = (m: Pick<SavedMonitor, 'stopId' | 'keys'>) => `${m.stopId}#${[...m.keys].sort().join(',')}`;

export function saveMonitor(m: SavedMonitor) {
  const list = loadMonitors().filter(x => monitorId(x) !== monitorId(m));
  write(MONITORS, [m, ...list]);
}

export function removeMonitor(m: SavedMonitor) {
  write(MONITORS, loadMonitors().filter(x => monitorId(x) !== monitorId(m)));
}

export const loadRecent = () => read<RecentStop[]>(RECENT, []);

export function addRecent(s: RecentStop) {
  write(RECENT, [s, ...loadRecent().filter(x => x.id !== s.id)].slice(0, 5));
}
