import { useRoute } from './route';
import { Home } from './Home';
import { LinePicker } from './LinePicker';
import { Monitor } from './Monitor';

export function App() {
  const route = useRoute();
  switch (route.page) {
    case 'monitor': return <Monitor key={location.hash} {...route} />;
    case 'stop': return <LinePicker key={route.stopId} {...route} />;
    default: return <Home />;
  }
}
