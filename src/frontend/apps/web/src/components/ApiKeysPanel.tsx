import { useEffect, useState } from 'react';
import { Check, Copy, KeyRound, Loader2, Plus, Trash2 } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import { apiKeys, type ApiKeyItemV1, type ApiKeyScope, type CreateApiKeyResponseV1 } from '@/lib/api';
import { toast } from 'sonner';

const SCOPE_LABELS: Record<ApiKeyScope, string> = {
  RestAndMcp: 'REST + MCP',
  McpOnly: 'MCP only',
  RestOnly: 'REST only',
};

function formatLastUsed(iso?: string | null): string {
  if (!iso) return 'Never used';
  const used = new Date(iso);
  const diffMs = Date.now() - used.getTime();
  const minutes = Math.floor(diffMs / 60_000);
  if (minutes < 1) return 'Used just now';
  if (minutes < 60) return `Used ${minutes}m ago`;
  const hours = Math.floor(minutes / 60);
  if (hours < 24) return `Used ${hours}h ago`;
  const days = Math.floor(hours / 24);
  if (days < 30) return `Used ${days}d ago`;
  return `Used ${used.toLocaleDateString()}`;
}

export function ApiKeysPanel() {
  const [keys, setKeys] = useState<ApiKeyItemV1[] | null>(null);
  const [loadError, setLoadError] = useState(false);
  const [showCreate, setShowCreate] = useState(false);
  const [newKey, setNewKey] = useState<CreateApiKeyResponseV1 | null>(null);
  const [copied, setCopied] = useState(false);

  useEffect(() => {
    apiKeys.list()
      .then(setKeys)
      .catch(() => setLoadError(true));
  }, []);

  const refresh = () => {
    apiKeys.list().then(setKeys).catch(() => setLoadError(true));
  };

  const handleDelete = async (id: string, name: string) => {
    if (!window.confirm(`Revoke API key "${name}"? Any client using it will stop working immediately.`)) {
      return;
    }
    try {
      await apiKeys.delete(id);
      toast.success('API key revoked');
      refresh();
    } catch (err) {
      toast.error(err instanceof Error ? err.message : 'Revoke failed');
    }
  };

  const copyNewKey = () => {
    if (!newKey) return;
    navigator.clipboard.writeText(newKey.key);
    setCopied(true);
    toast.success('API key copied');
    setTimeout(() => setCopied(false), 2000);
  };

  return (
    <section className="rounded-2xl border border-border bg-card p-6 space-y-4">
      <div className="flex items-center justify-between">
        <h2 className="text-sm font-semibold uppercase tracking-wide text-muted-foreground">API Keys</h2>
        <Button size="sm" variant="outline" onClick={() => setShowCreate(true)}>
          <Plus className="h-4 w-4 mr-2" />New key
        </Button>
      </div>

      <p className="text-xs text-muted-foreground">
        Pass <code className="font-mono text-[11px]">X-Api-Key: dk_…</code> on any REST or MCP call to authenticate as your user. Keys are shown unmasked exactly once at creation.
      </p>

      {loadError && <p className="text-sm text-destructive">Failed to load API keys.</p>}
      {!loadError && keys === null && (
        <div className="text-sm text-muted-foreground inline-flex items-center gap-2">
          <Loader2 className="h-3 w-3 animate-spin" />Loading…
        </div>
      )}
      {keys !== null && keys.length === 0 && (
        <p className="text-sm text-muted-foreground">No API keys yet.</p>
      )}
      {keys !== null && keys.length > 0 && (
        <ul className="divide-y divide-border">
          {keys.map((k) => (
            <li key={k.id} className="flex items-center justify-between gap-4 py-3">
              <div className="min-w-0 flex-1">
                <div className="flex items-center gap-2 text-sm font-medium">
                  <KeyRound className="h-4 w-4 text-muted-foreground shrink-0" />
                  <span className="truncate">{k.name}</span>
                  <span className="rounded-full border border-border px-2 py-0.5 text-[10px] font-medium uppercase tracking-wide text-muted-foreground">
                    {SCOPE_LABELS[k.scope]}
                  </span>
                </div>
                <div className="mt-1 flex flex-wrap items-center gap-2 text-xs text-muted-foreground">
                  <span className="font-mono">{k.maskedKey}</span>
                  <span>·</span>
                  <span>Created {new Date(k.createdAt).toLocaleDateString()}</span>
                  <span>·</span>
                  <span>{formatLastUsed(k.lastUsedAt)}</span>
                </div>
                {k.description && (
                  <p className="mt-1 text-xs text-muted-foreground truncate">{k.description}</p>
                )}
              </div>
              <Button
                variant="ghost"
                size="icon"
                className="h-8 w-8 text-muted-foreground hover:text-destructive"
                onClick={() => handleDelete(k.id, k.name)}
                aria-label={`Revoke ${k.name}`}
              >
                <Trash2 className="h-4 w-4" />
              </Button>
            </li>
          ))}
        </ul>
      )}

      <CreateKeyDialog
        open={showCreate}
        onOpenChange={(open) => {
          setShowCreate(open);
          if (!open) {
            setNewKey(null);
            setCopied(false);
          }
        }}
        onCreated={(key) => {
          setNewKey(key);
          refresh();
        }}
        createdKey={newKey}
        copied={copied}
        onCopy={copyNewKey}
      />
    </section>
  );
}

interface CreateKeyDialogProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  onCreated: (key: CreateApiKeyResponseV1) => void;
  createdKey: CreateApiKeyResponseV1 | null;
  copied: boolean;
  onCopy: () => void;
}

function CreateKeyDialog({ open, onOpenChange, onCreated, createdKey, copied, onCopy }: CreateKeyDialogProps) {
  const [name, setName] = useState('');
  const [description, setDescription] = useState('');
  const [scope, setScope] = useState<ApiKeyScope>('RestAndMcp');
  const [submitting, setSubmitting] = useState(false);

  useEffect(() => {
    if (open && !createdKey) {
      setName('');
      setDescription('');
      setScope('RestAndMcp');
    }
  }, [open, createdKey]);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!name.trim()) {
      toast.error('Name is required');
      return;
    }
    setSubmitting(true);
    try {
      const created = await apiKeys.create({
        name: name.trim(),
        description: description.trim() || undefined,
        scope,
      });
      onCreated(created);
    } catch (err) {
      toast.error(err instanceof Error ? err.message : 'Create failed');
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="sm:max-w-md">
        {!createdKey ? (
          <form onSubmit={handleSubmit}>
            <DialogHeader>
              <DialogTitle>New API key</DialogTitle>
              <DialogDescription>Give the key a memorable name. The secret is shown once after creation.</DialogDescription>
            </DialogHeader>
            <div className="grid gap-4 py-4">
              <div className="space-y-2">
                <Label htmlFor="apiKeyName">Name</Label>
                <Input id="apiKeyName" value={name} onChange={(e) => setName(e.target.value)} placeholder="laptop-cli" autoFocus />
              </div>
              <div className="space-y-2">
                <Label htmlFor="apiKeyDesc">Description (optional)</Label>
                <Input id="apiKeyDesc" value={description} onChange={(e) => setDescription(e.target.value)} placeholder="Where this key will be used" />
              </div>
              <div className="space-y-2">
                <Label htmlFor="apiKeyScope">Allowed surface</Label>
                <Select value={scope} onValueChange={(v) => setScope(v as ApiKeyScope)}>
                  <SelectTrigger id="apiKeyScope">
                    <SelectValue />
                  </SelectTrigger>
                  <SelectContent>
                    <SelectItem value="RestAndMcp">REST + MCP (everything)</SelectItem>
                    <SelectItem value="McpOnly">MCP only (POST /)</SelectItem>
                    <SelectItem value="RestOnly">REST only (/api/*)</SelectItem>
                  </SelectContent>
                </Select>
                <p className="text-xs text-muted-foreground">
                  Scope is enforced on every request — a mis-scoped call returns 401.
                </p>
              </div>
            </div>
            <DialogFooter>
              <Button type="button" variant="outline" onClick={() => onOpenChange(false)} disabled={submitting}>Cancel</Button>
              <Button type="submit" disabled={submitting}>{submitting ? 'Creating…' : 'Create key'}</Button>
            </DialogFooter>
          </form>
        ) : (
          <div>
            <DialogHeader>
              <DialogTitle>API key created</DialogTitle>
              <DialogDescription>Copy the secret now — you won't be able to see it again.</DialogDescription>
            </DialogHeader>
            <div className="py-4 space-y-3">
              <div className="font-mono text-xs break-all p-3 rounded-lg border border-border bg-muted/50">
                {createdKey.key}
              </div>
              <Button onClick={onCopy} variant="outline" size="sm">
                {copied ? <><Check className="h-4 w-4 mr-2" />Copied</> : <><Copy className="h-4 w-4 mr-2" />Copy key</>}
              </Button>
            </div>
            <DialogFooter>
              <Button type="button" onClick={() => onOpenChange(false)}>Done</Button>
            </DialogFooter>
          </div>
        )}
      </DialogContent>
    </Dialog>
  );
}
