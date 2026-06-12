import { useEffect, useMemo, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useAuthStore } from '@/store/auth';

function parseJwt(token: string): Record<string, unknown> | null {
  try {
    const base64Url = token.split('.')[1];
    if (!base64Url) return null;
    const base64 = base64Url.replace(/-/g, '+').replace(/_/g, '/');
    const json = decodeURIComponent(
      atob(base64)
        .split('')
        .map((c) => '%' + ('00' + c.charCodeAt(0).toString(16)).slice(-2))
        .join(''),
    );
    return JSON.parse(json);
  } catch {
    return null;
  }
}

export function LoginCallbackPage() {
  const navigate = useNavigate();
  const setTokens = useAuthStore((s) => s.setTokens);
  const setUser = useAuthStore((s) => s.setUser);

  const initial = useMemo(() => {
    const params = new URLSearchParams(window.location.hash.replace(/^#/, ''));
    const errorParam = params.get('error');
    if (errorParam) {
      const desc = params.get('error_description');
      return { error: desc || errorParam, params: null };
    }
    return { error: null, params };
  }, []);

  const [error, setError] = useState<string | null>(initial.error);

  useEffect(() => {
    if (initial.error || !initial.params) return;

    const accessToken = initial.params.get('access_token');
    const refreshToken = initial.params.get('refresh_token');
    const idToken = initial.params.get('id_token');
    const expiresIn = initial.params.get('expires_in');

    if (!accessToken) {
      setError('No access token received.');
      return;
    }

    setTokens(accessToken, refreshToken, parseInt(expiresIn || '300', 10), idToken);

    const payload = parseJwt(accessToken);
    if (payload) {
      setUser({
        id: String(payload.sub ?? ''),
        email: typeof payload.email === 'string' ? payload.email : undefined,
        name: typeof payload.name === 'string' ? payload.name : undefined,
        username: typeof payload.preferred_username === 'string' ? payload.preferred_username : undefined,
      });
    }

    window.history.replaceState(null, '', window.location.pathname);
    navigate('/channels', { replace: true });
  }, [navigate, setTokens, setUser, initial]);

  if (error) {
    return (
      <div className="flex min-h-screen flex-col items-center justify-center bg-background p-4">
        <div className="w-full max-w-sm space-y-4 text-center">
          <h1 className="text-2xl font-bold text-destructive">Login failed</h1>
          <p className="text-muted-foreground">{error}</p>
          <a href="/login" className="inline-block text-sm text-primary hover:underline">
            Try again
          </a>
        </div>
      </div>
    );
  }

  return (
    <div className="flex min-h-screen flex-col items-center justify-center bg-background p-4">
      <div className="w-full max-w-sm space-y-4 text-center">
        <h1 className="text-2xl font-bold">Signing you in…</h1>
        <p className="text-muted-foreground">Hang tight.</p>
      </div>
    </div>
  );
}
