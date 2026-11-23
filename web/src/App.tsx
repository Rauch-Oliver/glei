import { useRoute } from './route';
import { Home } from './Home';
import { LinePicker } from './LinePicker';
import { Monitor } from './Monitor';
import { Datenschutz, Impressum } from './Legal';

export function App() {
  const route = useRoute();
  switch (route.page) {
    case 'monitor': return <Monitor key={location.hash} {...route} />;
    case 'stop': return <LinePicker key={route.stopId} {...route} />;
    case 'impressum': return <Impressum />;
    case 'datenschutz': return <Datenschutz />;
    default: return <Home />;
  }
}
