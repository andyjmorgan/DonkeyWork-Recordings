import { useEffect } from 'react';
import { Navigate, useLocation } from 'react-router-dom';
import { useAuthStore } from '@/store/auth';

export function AuthGuard({ children }: { children: React.ReactNode }) {
  const isAuthenticated = useAuthStore((s) => s.isAuthenticated);
  const isTokenExpired = useAuthStore((s) => s.isTokenExpired);
  const refreshTokens = useAuthStore((s) => s.refreshTokens);
  const logout = useAuthStore((s) => s.logout);
  const location = useLocation();

  useEffect(() => {
    if (!isAuthenticated) return;
    if (isTokenExpired()) {
      refreshTokens().then((result) => {
        if (!result.ok && result.reason === 'rejected') {
          logout();
        }
      });
    }
  }, [isAuthenticated, isTokenExpired, refreshTokens, logout]);

  if (!isAuthenticated) {
    return <Navigate to="/login" state={{ from: location.pathname }} replace />;
  }

  return <>{children}</>;
}
