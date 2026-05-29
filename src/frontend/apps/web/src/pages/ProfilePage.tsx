import { useEffect, useState } from 'react';
import { UserCircle, Copy, Check, RefreshCw } from 'lucide-react';
import { useAuthStore } from '@/store/auth';
import { feedSettings, type FeedSettingsV1 } from '@/lib/api';
import { Button } from '@/components/ui/button';
import { toast } from 'sonner';

export function ProfilePage() {
  const user = useAuthStore((s) => s.user);
  const [settings, setSettings] = useState<FeedSettingsV1 | null>(null);
  const [loading, setLoading] = useState(true);
  const [copied, setCopied] = useState(false);

  useEffect(() => {
    let cancelled = false;
    feedSettings.get()
      .then((s) => { if (!cancelled) { setSettings(s); setLoading(false); } })
      .catch(() => { if (!cancelled) setLoading(false); });
    return () => { cancelled = true; };
  }, []);

  const handleCopy = () => {
    if (!settings) return;
    navigator.clipboard.writeText(settings.masterFeedUrl);
    setCopied(true);
    toast.success('Master feed URL copied');
    setTimeout(() => setCopied(false), 2000);
  };

  return (
    <div className="p-8 mx-auto max-w-2xl space-y-8">
      <header className="flex items-center gap-4">
        <div className="inline-flex h-12 w-12 items-center justify-center rounded-xl bg-primary/10 text-primary">
          <UserCircle className="h-6 w-6" />
        </div>
        <div>
          <h1 className="text-2xl font-semibold">Profile</h1>
          <p className="text-sm text-muted-foreground">Account + master feed URL.</p>
        </div>
      </header>

      <section className="rounded-2xl border border-border bg-card p-6 space-y-4">
        <h2 className="text-sm font-semibold uppercase tracking-wide text-muted-foreground">Identity</h2>
        <dl className="grid grid-cols-3 gap-y-2 text-sm">
          <dt className="text-muted-foreground">Name</dt>
          <dd className="col-span-2">{user?.name ?? '—'}</dd>
          <dt className="text-muted-foreground">Username</dt>
          <dd className="col-span-2">{user?.username ?? '—'}</dd>
          <dt className="text-muted-foreground">Email</dt>
          <dd className="col-span-2">{user?.email ?? '—'}</dd>
          <dt className="text-muted-foreground">User ID</dt>
          <dd className="col-span-2 font-mono text-xs">{user?.id ?? '—'}</dd>
        </dl>
      </section>

      <section className="rounded-2xl border border-border bg-card p-6 space-y-4">
        <h2 className="text-sm font-semibold uppercase tracking-wide text-muted-foreground">Master Feed</h2>
        {loading && <div className="text-sm text-muted-foreground inline-flex items-center gap-2"><RefreshCw className="h-3 w-3 animate-spin" />Loading…</div>}
        {settings && (
          <div className="space-y-3">
            <div className="font-mono text-xs break-all p-3 rounded-lg border border-border bg-muted/50">
              {settings.masterFeedUrl}
            </div>
            <Button onClick={handleCopy} variant="outline" size="sm">
              {copied ? <><Check className="h-4 w-4 mr-2" />Copied</> : <><Copy className="h-4 w-4 mr-2" />Copy URL</>}
            </Button>
            <p className="text-xs text-muted-foreground">
              Subscribe in Overcast, Apple Podcasts, or any RSS-aware podcast app.
            </p>
          </div>
        )}
      </section>
    </div>
  );
}
