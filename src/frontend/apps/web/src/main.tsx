import React from 'react';
import ReactDOM from 'react-dom/client';
import { BrowserRouter } from 'react-router-dom';
import { Toaster } from 'sonner';
import './index.css';
import { App } from './App';
import { useThemeStore } from '@/store/theme';

function ThemedToaster() {
  const theme = useThemeStore((s) => s.theme);
  return <Toaster theme={theme} position="bottom-right" richColors />;
}

ReactDOM.createRoot(document.getElementById('root')!).render(
  <React.StrictMode>
    <BrowserRouter>
      <App />
      <ThemedToaster />
    </BrowserRouter>
  </React.StrictMode>,
);
