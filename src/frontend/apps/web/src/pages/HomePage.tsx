import { Mic2, LogOut } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { useAuthStore } from '@/store/auth';

export function HomePage() {
  const user = useAuthStore((s) => s.user);
  const logout = useAuthStore((s) => s.logout);

  const handleLogout = () => {
    const idToken = useAuthStore.getState().idToken;
    logout();
    const hint = idToken ? `?id_token_hint=${encodeURIComponent(idToken)}` : '';
    window.location.assign(`/api/v1/auth/logout${hint}`);
  };

  return (
    <main className="min-h-screen p-4 sm:p-8">
      <div className="mx-auto max-w-3xl space-y-8">
        <header className="flex flex-wrap items-center justify-between gap-3">
          <div className="flex items-center gap-3">
            <div className="inline-flex h-10 w-10 items-center justify-center rounded-xl bg-primary/10 text-primary">
              <Mic2 className="h-5 w-5" />
            </div>
            <div>
              <h1 className="text-xl font-semibold">DonkeyWork Recordings</h1>
              <p className="text-xs text-muted-foreground">
                Signed in as {user?.name ?? user?.username ?? user?.email ?? 'unknown user'}
              </p>
            </div>
          </div>
          <Button variant="outline" size="sm" onClick={handleLogout}>
            <LogOut className="h-4 w-4" />
            Log out
          </Button>
        </header>

        <section className="rounded-2xl border border-border bg-card p-8 text-center text-card-foreground">
          <p className="text-muted-foreground">
            Frontend round 2 — auth flow wired. Channels, recordings dialog, and feed settings land in round 3 and later.
          </p>
        </section>
      </div>
    </main>
  );
}
