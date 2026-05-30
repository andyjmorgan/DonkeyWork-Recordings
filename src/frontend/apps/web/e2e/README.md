# E2E tests (Playwright)

Mobile-viewport layout checks that run against a locally built+previewed copy of
the web app. Auth is real: `auth.setup.ts` fetches a token from Keycloak via the
Direct Access Grants flow (no login UI) and seeds it into the SPA's `localStorage`
session, saved as Playwright `storageState` and reused by every test.

## Run locally

```bash
cd src/frontend
pnpm install
pnpm --filter @donkeywork-recordings/web exec playwright install chromium

export E2E_TOKEN_URL="https://auth.donkeywork.dev/realms/Agents/protocol/openid-connect/token"
export E2E_CLIENT_ID="recordings-e2e"
export E2E_USERNAME="e2e-recordings"
export E2E_PASSWORD="<the e2e test user password>"   # CI secret E2E_PASSWORD

pnpm --filter @donkeywork-recordings/web e2e
```

By default Playwright builds the app and serves it on `http://localhost:4173`. To
test an already-running deployment instead, set `PLAYWRIGHT_BASE_URL` (the local
build/preview server is then skipped).

## Auth model

The SPA persists its session in `localStorage["donkeywork-recordings-auth"]` (a
zustand-persist blob) and sends `Authorization: Bearer <accessToken>`. `auth.setup.ts`
mirrors that blob from a real token, so the app boots authenticated. The token is
issued for the public `recordings-e2e` Keycloak client (Direct Access Grants), which
carries the `recordings-audience` scope so the recordings API accepts it.

The data shown in layout tests is mocked via Playwright route interception, so the
checks are deterministic and don't depend on real recordings or the backend.
