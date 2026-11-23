/** Line number in the style of the Graz stop signs: trams plain, buses boxed, night lines inverted. */
export function LineBadge({ line, mode }: { line: string; mode: string | null }) {
  const kind = /^N/i.test(line) ? 'night' : mode === 'tram' ? 'tram' : 'bus';
  return <span class={`badge ${kind}`}>{line}</span>;
}
