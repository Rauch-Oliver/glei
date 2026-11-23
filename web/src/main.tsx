import { render } from 'preact';
import '@fontsource/doto/900.css';
import './styles.css';
import { App } from './App';

render(<App />, document.getElementById('app')!);
