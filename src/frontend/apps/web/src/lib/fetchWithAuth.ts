import { useAuthStore } from '@/store/auth';

export async function fetchWithAuth(
  url: string,
  options: RequestInit = {},
  retryOnUnauthorized = true,
): Promise<Response> {
  const { logout, refreshTokens, shouldRefreshToken, isTokenExpired } = useAuthStore.getState();

  if (isTokenExpired() && retryOnUnauthorized) {
    const result = await refreshTokens();
    if (!result.ok) {
      if (result.reason === 'rejected') {
        logout();
        window.location.assign('/login');
        throw new Error('Session expired');
      }
      throw new Error('Token refresh failed (network)');
    }
  } else if (shouldRefreshToken() && retryOnUnauthorized) {
    refreshTokens().catch(() => undefined);
  }

  const currentToken = useAuthStore.getState().accessToken;

  const response = await fetch(url, {
    ...options,
    headers: {
      ...options.headers,
      Authorization: `Bearer ${currentToken}`,
    },
  });

  if (response.status === 401 && retryOnUnauthorized) {
    const result = await refreshTokens();

    if (result.ok) {
      return fetchWithAuth(url, options, false);
    }

    if (result.reason === 'rejected') {
      logout();
      window.location.assign('/login');
      throw new Error('Session expired');
    }

    return response;
  }

  return response;
}
