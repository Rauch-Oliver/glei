import { useEffect, useState } from 'preact/hooks';

// Hash routes, so a monitor can be bookmarked/shared and the app works on any static host:
//   #/                                   home (search + saved monitors)
//   #/stop/<id>?name=<name>              choose lines/directions
//   #/monitor/<id>?name=<name>&l=<keys>  the display

export type Route =
  | { page: 'home' }
  | { page: 'stop'; stopId: string; name: string; keys: string[] }
  | { page: 'monitor'; stopId: string; name: string; keys: string[] };

export function parse(hash: string): Route {
  const [path, query = ''] = hash.replace(/^#/, '').split('?');
  const params = new URLSearchParams(query);
  const m = /^\/(stop|monitor)\/(.+)$/.exec(path);
  if (!m) return { page: 'home' };
  const keys = (params.get('l') ?? '').split(',').filter(Boolean);
  return { page: m[1] as 'stop' | 'monitor', stopId: decodeURIComponent(m[2]), name: params.get('name') ?? '', keys };
}

export function href(page: 'stop' | 'monitor', stopId: string, name: string, keys: string[] = []): string {
  const params = new URLSearchParams({ name });
  if (keys.length) params.set('l', keys.join(','));
  return `#/${page}/${encodeURIComponent(stopId)}?${params}`;
}

export function useRoute(): Route {
  const [route, setRoute] = useState(() => parse(location.hash));
  useEffect(() => {
    const on = () => setRoute(parse(location.hash));
    addEventListener('hashchange', on);
    return () => removeEventListener('hashchange', on);
  }, []);
  return route;
}

/** "Graz Moserhofgasse" → "Moserhofgasse" for display; other towns keep their prefix. */
export const shortName = (name: string) => name.replace(/^Graz\s+/, '');
