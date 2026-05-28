import { useEffect } from 'react';
import { useAuthStore } from '@/store/auth';

const CHECK_INTERVAL_MS = 60_000;

export function useTokenRefresh() {
  const isAuthenticated = useAuthStore((s) => s.isAuthenticated);

  useEffect(() => {
    if (!isAuthenticated) return;

    const tick = () => {
      const { shouldRefreshToken, refreshTokens } = useAuthStore.getState();
      if (shouldRefreshToken()) {
        refreshTokens().catch(() => undefined);
      }
    };

    tick();
    const interval = window.setInterval(tick, CHECK_INTERVAL_MS);

    const onFocus = () => tick();
    window.addEventListener('focus', onFocus);

    return () => {
      window.clearInterval(interval);
      window.removeEventListener('focus', onFocus);
    };
  }, [isAuthenticated]);
}
