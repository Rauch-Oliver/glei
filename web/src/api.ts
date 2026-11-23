export interface Stop { id: string; name: string }

export interface Departure {
  line: string;
  destination: string;
  minutes: number;
  plannedTime: string;
  estimatedTime: string | null;
  mode: string | null;
  cancelled: boolean;
  direction: 'outward' | 'inward' | null;
}

export class ApiError extends Error {
  constructor(public status: number, message: string) { super(message); }
}

async function get<T>(path: string, signal?: AbortSignal): Promise<T> {
  const res = await fetch(path, { signal, headers: { Accept: 'application/json' } });
  if (!res.ok) {
    const msg = res.status === 429 ? 'Zu viele Anfragen – kurz warten'
      : res.status >= 500 ? 'Fahrplanauskunft gerade nicht erreichbar'
      : `Fehler ${res.status}`;
    throw new ApiError(res.status, msg);
  }
  return res.json() as Promise<T>;
}

export const searchStops = (query: string, signal?: AbortSignal) =>
  get<Stop[]>(`/api/stops/search?query=${encodeURIComponent(query)}`, signal);

export const getDepartures = (stopId: string, signal?: AbortSignal) =>
  get<Departure[]>(`/api/departures/${encodeURIComponent(stopId)}?limit=100`, signal);
