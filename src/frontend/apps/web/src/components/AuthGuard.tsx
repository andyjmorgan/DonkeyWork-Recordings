import { useEffect } from 'react';
import { login } from '@/lib/auth';
import { useAuthStore } from '@/store/auth';

export function AuthGuard({ children }: { children: React.ReactNode }) {
  const isAuthenticated = useAuthStore((s) => s.isAuthenticated);
  const isTokenExpired = useAuthStore((s) => s.isTokenExpired);
  const refreshTokens = useAuthStore((s) => s.refreshTokens);
  const logout = useAuthStore((s) => s.logout);

  useEffect(() => {
    // Signed out on a guarded route — hand straight off to Keycloak.
    if (!isAuthenticated) {
      login();
      return;
    }
    if (isTokenExpired()) {
      refreshTokens().then((result) => {
        if (!result.ok && result.reason === 'rejected') {
          logout();
        }
      });
    }
  }, [isAuthenticated, isTokenExpired, refreshTokens, logout]);

  if (!isAuthenticated) {
    return null; // redirecting to Keycloak
  }

  return <>{children}</>;
}
