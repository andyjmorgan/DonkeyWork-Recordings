import { useState } from 'react';
import { UserCircle, Copy, Check } from 'lucide-react';
import { useAuthStore } from '@/store/auth';
import { Button } from '@/components/ui/button';
import { ApiKeysPanel } from '@/components/ApiKeysPanel';
import { toast } from 'sonner';

const MCP_URL = 'https://recordings.donkeywork.dev/mcp';

export function ProfilePage() {
  const user = useAuthStore((s) => s.user);
  const [copied, setCopied] = useState(false);

  const handleCopy = () => {
    navigator.clipboard.writeText(MCP_URL);
    setCopied(true);
    toast.success('MCP server URL copied');
    setTimeout(() => setCopied(false), 2000);
  };

  return (
    <div className="p-4 sm:p-8 mx-auto max-w-2xl space-y-8">
      <header className="flex items-center gap-4">
        <div className="inline-flex h-12 w-12 items-center justify-center rounded-xl bg-primary/10 text-primary">
          <UserCircle className="h-6 w-6" />
        </div>
        <div>
          <h1 className="text-2xl font-semibold">Profile</h1>
          <p className="text-sm text-muted-foreground">Account, API keys, and MCP setup.</p>
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

      <ApiKeysPanel />

      <section className="rounded-2xl border border-border bg-card p-6 space-y-4">
        <h2 className="text-sm font-semibold uppercase tracking-wide text-muted-foreground">MCP Server</h2>
        <p className="text-xs text-muted-foreground">
          Add this server to an MCP client (Claude, etc.) to create channels and recordings. Authenticate with an
          {' '}<code className="font-mono text-[11px]">X-Api-Key: dk_…</code> header using a key from above (MCP scope).
        </p>
        <div className="space-y-3">
          <div className="font-mono text-xs break-all p-3 rounded-lg border border-border bg-muted/50">
            {MCP_URL}
          </div>
          <Button onClick={handleCopy} variant="outline" size="sm">
            {copied ? <><Check className="h-4 w-4 mr-2" />Copied</> : <><Copy className="h-4 w-4 mr-2" />Copy URL</>}
          </Button>
        </div>
      </section>
    </div>
  );
}
