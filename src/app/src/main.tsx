import React from 'react';
import ReactDOM from 'react-dom/client';
import { FluentProvider, teamsLightTheme } from '@fluentui/react-components';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import * as microsoftTeams from '@microsoft/teams-js';
import { initI18n } from './i18n';
import { App } from './App';

// Fallback userId when running outside Teams or Teams SDK fails.
// Points at Alex's real AAD OID in the demo tenant (7k2cc2.onmicrosoft.com).
// This ensures cards show in browser-mode and when Teams SDK is unavailable.
const DEV_FALLBACK_USER_ID = 'f7a02de7-e195-4894-bc23-f7f74b696cbd';

// Version stamp — visible in DevTools console to confirm which build is running
console.info('[Commit] app version 1.0.3 loaded');

// Initialize Teams SDK and locale-driven i18n before rendering
microsoftTeams.app.initialize().then(() => {
  // Fetch context and SSO token in parallel — token may fail outside Teams, that's fine
  const contextPromise = microsoftTeams.app.getContext();
  const tokenPromise   = microsoftTeams.authentication.getAuthToken().catch((err: unknown) => {
    // Log the exact error so we can diagnose SSO failures in Teams DevTools console
    console.error('[Commit SSO] getAuthToken failed:', err);
    return null;
  });

  Promise.all([contextPromise, tokenPromise]).then(([ctx, token]) => {
    const locale  = ctx.app.locale ?? 'en';
    const userId  = ctx.user?.id ?? DEV_FALLBACK_USER_ID;
    initI18n(locale);
    mount(userId, token ?? undefined);
  }).catch(() => {
    // Running outside Teams (browser dev mode) — use default locale + dev fallback
    initI18n('en');
    mount(DEV_FALLBACK_USER_ID, undefined);
  });
}).catch(() => {
  initI18n('en');
  mount(DEV_FALLBACK_USER_ID, undefined);
});

function mount(userId: string, authToken?: string): void {
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
          <App userId={userId} authToken={authToken} />
        </FluentProvider>
      </QueryClientProvider>
    </React.StrictMode>
  );
}
