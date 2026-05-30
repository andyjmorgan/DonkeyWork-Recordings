import { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { Plus, Radio, Pencil, Trash2 } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { collections, type AudioCollectionV1 } from '@/lib/api';
import { AudioCollectionFormDialog } from '@/components/audio/AudioCollectionFormDialog';
import { toast } from 'sonner';

export function ChannelsListPage() {
  const [items, setItems] = useState<AudioCollectionV1[]>([]);
  const [loading, setLoading] = useState(true);
  const [dialogOpen, setDialogOpen] = useState(false);
  const [editing, setEditing] = useState<AudioCollectionV1 | null>(null);

  const load = async () => {
    setLoading(true);
    try {
      const r = await collections.list(0, 200);
      setItems(r.items);
    } catch (err) {
      toast.error(err instanceof Error ? err.message : 'Failed to load channels');
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => { load(); }, []);

  const handleSaved = (saved: AudioCollectionV1) => {
    setItems((prev) => {
      const idx = prev.findIndex((c) => c.id === saved.id);
      if (idx === -1) return [saved, ...prev];
      const next = [...prev];
      next[idx] = saved;
      return next;
    });
  };

  const handleDelete = async (c: AudioCollectionV1) => {
    if (!confirm(`Delete channel "${c.name}"? Recordings in this channel will be unfiled, not deleted.`)) return;
    try {
      await collections.delete(c.id);
      setItems((prev) => prev.filter((x) => x.id !== c.id));
      toast.success('Channel deleted');
    } catch (err) {
      toast.error(err instanceof Error ? err.message : 'Delete failed');
    }
  };

  return (
    <div className="p-4 sm:p-8 mx-auto max-w-5xl space-y-6">
      <header className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
        <div>
          <h1 className="text-2xl font-semibold">Channels</h1>
          <p className="text-sm text-muted-foreground">Each channel is a podcast feed.</p>
        </div>
        <Button className="w-full sm:w-auto" onClick={() => { setEditing(null); setDialogOpen(true); }}>
          <Plus className="h-4 w-4 mr-2" />New channel
        </Button>
      </header>

      {loading && <div className="text-muted-foreground">Loading…</div>}
      {!loading && items.length === 0 && (
        <div className="rounded-2xl border border-dashed border-border p-12 text-center space-y-3">
          <Radio className="h-10 w-10 mx-auto text-muted-foreground" />
          <p className="text-muted-foreground">No channels yet.</p>
          <Button onClick={() => { setEditing(null); setDialogOpen(true); }} variant="outline">
            Create your first channel
          </Button>
        </div>
      )}

      {!loading && items.length > 0 && (
        <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-3">
          {items.map((c) => (
            <div key={c.id} className="rounded-2xl border border-border bg-card p-5 space-y-3 group">
              <Link to={`/channels/${c.id}`} className="block space-y-2 hover:opacity-90">
                <div className="flex items-start justify-between gap-2">
                  <h3 className="font-semibold leading-tight">{c.name}</h3>
                  <span className="text-xs text-muted-foreground shrink-0">{c.recordingCount} ep</span>
                </div>
                <p className="text-sm text-muted-foreground line-clamp-2">{c.description || 'No description'}</p>
                {c.tone && (
                  <div className="text-xs text-muted-foreground italic line-clamp-1">Tone: {c.tone}</div>
                )}
              </Link>
              <div className="flex justify-end gap-1 opacity-0 group-hover:opacity-100 transition">
                <Button variant="ghost" size="icon" onClick={() => { setEditing(c); setDialogOpen(true); }}>
                  <Pencil className="h-4 w-4" />
                </Button>
                <Button variant="ghost" size="icon" onClick={() => handleDelete(c)}>
                  <Trash2 className="h-4 w-4" />
                </Button>
              </div>
            </div>
          ))}
        </div>
      )}

      <AudioCollectionFormDialog
        open={dialogOpen}
        onOpenChange={setDialogOpen}
        editing={editing}
        onSaved={handleSaved}
      />
    </div>
  );
}
