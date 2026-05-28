import { useEffect } from 'react';
import { Navigate, useLocation } from 'react-router-dom';
import { useAuthStore } from '@/store/auth';

export function AuthGuard({ children }: { children: React.ReactNode }) {
  const isAuthenticated = useAuthStore((s) => s.isAuthenticated);
  const hasHydrated = useAuthStore((s) => s.hasHydrated);
  const isTokenExpired = useAuthStore((s) => s.isTokenExpired);
  const refreshTokens = useAuthStore((s) => s.refreshTokens);
  const logout = useAuthStore((s) => s.logout);
  const location = useLocation();

  useEffect(() => {
    if (!hasHydrated || !isAuthenticated) return;

    if (isTokenExpired()) {
      refreshTokens().then((result) => {
        if (!result.ok && result.reason === 'rejected') {
          logout();
        }
      });
    }
  }, [hasHydrated, isAuthenticated, isTokenExpired, refreshTokens, logout]);

  if (!hasHydrated) {
    return (
      <div className="flex min-h-screen items-center justify-center text-muted-foreground">
        Loading…
      </div>
    );
  }

  if (!isAuthenticated) {
    return <Navigate to="/login" state={{ from: location.pathname }} replace />;
  }

  return <>{children}</>;
}
