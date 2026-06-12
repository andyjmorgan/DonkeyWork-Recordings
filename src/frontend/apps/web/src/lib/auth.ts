// Sign-in is a hand-off to the backend, which kicks off the OAuth code flow and
// redirects to Keycloak. Keycloak owns the login screen — including the social
// IdP buttons — so the SPA has no dedicated login page of its own (mirrors the
// Vault portal). Any "log in" action, expired-session bounce, or hit on a
// guarded route while signed out routes through here.
export function login(): void {
  window.location.assign('/api/v1/auth/login');
}
