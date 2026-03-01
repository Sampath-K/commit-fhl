import React from 'react';
import ReactDOM from 'react-dom/client';
import { FluentProvider, teamsLightTheme } from '@fluentui/react-components';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import * as microsoftTeams from '@microsoft/teams-js';
import { initI18n } from './i18n';
import { App } from './App';

// Initialize Teams SDK and locale-driven i18n before rendering
microsoftTeams.app.initialize().then(() => {
  microsoftTeams.app.getContext().then(ctx => {
    const locale = ctx.app.locale ?? 'en';
    initI18n(locale);
    mount();
  }).catch(() => {
    // Running outside Teams (browser dev mode) — use default locale
    initI18n('en');
    mount();
  });
}).catch(() => {
  initI18n('en');
  mount();
});

function mount(): void {
  const queryClient = new QueryClient({
    defaultOptions: {
      queries: {
        staleTime: 30_000,   // 30s — balance freshness vs. request rate
        retry: 2,
      },
    },
  });

  ReactDOM.createRoot(document.getElementById('root')!).render(
    <React.StrictMode>
      <QueryClientProvider client={queryClient}>
        <FluentProvider theme={teamsLightTheme}>
          <App />
        </FluentProvider>
      </QueryClientProvider>
    </React.StrictMode>
  );
}
