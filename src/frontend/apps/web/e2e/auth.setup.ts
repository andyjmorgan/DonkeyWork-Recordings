import { test as setup } from '@playwright/test';
import { mkdirSync, writeFileSync } from 'node:fs';
import { dirname } from 'node:path';

// Reused authenticated browser state, seeded from a programmatic Keycloak token
// (Direct Access Grants) — no login UI. The SPA persists its session in
// localStorage under "donkeywork-recordings-auth" (a zustand-persist blob), so
// we write a valid token there and every test starts already signed in.
const AUTH_FILE = 'e2e/.auth/state.json';

function requireEnv(name: string): string {
  const value = process.env[name];
  if (!value) throw new Error(`Missing required env var ${name} (see e2e/README.md)`);
  return value;
}

setup('authenticate', async ({ request, baseURL }) => {
  const tokenUrl = requireEnv('E2E_TOKEN_URL');
  const clientId = process.env.E2E_CLIENT_ID ?? 'recordings-e2e';
  const username = requireEnv('E2E_USERNAME');
  const password = requireEnv('E2E_PASSWORD');

  const response = await request.post(tokenUrl, {
    form: {
      grant_type: 'password',
      client_id: clientId,
      username,
      password,
      scope: 'openid',
    },
  });

  if (!response.ok()) {
    throw new Error(`Keycloak token request failed: ${response.status()} ${await response.text()}`);
  }

  const token = (await response.json()) as {
    access_token: string;
    refresh_token?: string;
    id_token?: string;
    expires_in: number;
  };

  const now = Date.now();
  const persisted = {
    state: {
      accessToken: token.access_token,
      refreshToken: token.refresh_token ?? null,
      idToken: token.id_token ?? null,
      expiresAt: now + token.expires_in * 1000,
      tokenIssuedAt: now,
      user: { id: 'e2e', username, name: 'E2E', email: `${username}@donkeywork.dev` },
      isAuthenticated: true,
    },
    version: 0,
  };

  const origin = new URL(baseURL ?? 'http://localhost:4173').origin;
  const storageState = {
    cookies: [],
    origins: [
      {
        origin,
        localStorage: [{ name: 'donkeywork-recordings-auth', value: JSON.stringify(persisted) }],
      },
    ],
  };

  mkdirSync(dirname(AUTH_FILE), { recursive: true });
  writeFileSync(AUTH_FILE, JSON.stringify(storageState, null, 2));
});
