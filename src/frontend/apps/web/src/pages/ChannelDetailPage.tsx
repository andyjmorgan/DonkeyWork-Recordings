import { useEffect, useState } from 'react';
import { Link, useParams } from 'react-router-dom';
import { ArrowLeft, Plus, Copy, Check, MoreVertical, Trash2, FolderInput } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { DropdownMenu, DropdownMenuContent, DropdownMenuItem, DropdownMenuTrigger } from '@/components/ui/dropdown-menu';
import { collections, recordings, type AudioCollectionV1, type TtsRecordingV1 } from '@/lib/api';
import { AudioPlayer } from '@/components/audio/AudioPlayer';
import { NewRecordingDialog } from '@/components/audio/NewRecordingDialog';
import { MoveRecordingDialog } from '@/components/audio/MoveRecordingDialog';
import { useRecordingStatus } from '@/hooks/useRecordingStatus';
import { useAuthStore } from '@/store/auth';
import { toast } from 'sonner';

export function ChannelDetailPage() {
  const { id } = useParams<{ id: string }>();
  const [collection, setCollection] = useState<AudioCollectionV1 | null>(null);
  const [items, setItems] = useState<TtsRecordingV1[]>([]);
  const [loading, setLoading] = useState(true);
  const [newOpen, setNewOpen] = useState(false);
  const [moving, setMoving] = useState<TtsRecordingV1 | null>(null);
  const [copiedFeed, setCopiedFeed] = useState(false);
  const userId = useAuthStore((s) => s.user?.id);

  const load = async () => {
    if (!id) return;
    setLoading(true);
    try {
      const [coll, list] = await Promise.all([
        collections.get(id),
        collections.listRecordings(id, 0, 500),
      ]);
      setCollection(coll);
      setItems(list.items);
    } catch (err) {
      toast.error(err instanceof Error ? err.message : 'Failed to load channel');
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => { load(); }, [id]);

  const handleCreated = (created: TtsRecordingV1) => {
    setItems((prev) => [created, ...prev]);
  };

  const handleDelete = async (r: TtsRecordingV1) => {
    if (!confirm(`Delete recording "${r.name}"?`)) return;
    try {
      await recordings.delete(r.id);
      setItems((prev) => prev.filter((x) => x.id !== r.id));
      toast.success('Recording deleted');
    } catch (err) {
      toast.error(err instanceof Error ? err.message : 'Delete failed');
    }
  };

  const handleMoved = (moved: TtsRecordingV1) => {
    if (moved.collectionId !== id) {
      setItems((prev) => prev.filter((x) => x.id !== moved.id));
    } else {
      setItems((prev) => prev.map((x) => x.id === moved.id ? moved : x));
    }
  };

  const copyFeed = () => {
    if (!collection || !userId) return;
    const url = `${window.location.origin}/feeds/${userId}/${collection.id}.xml`;
    navigator.clipboard.writeText(url);
    setCopiedFeed(true);
    toast.success('Channel feed URL copied');
    setTimeout(() => setCopiedFeed(false), 2000);
  };

  if (loading) return <div className="p-8 text-muted-foreground">Loading…</div>;
  if (!collection) return <div className="p-8 text-muted-foreground">Channel not found.</div>;

  return (
    <div className="p-4 sm:p-8 mx-auto max-w-4xl space-y-6">
      <Link to="/channels" className="inline-flex items-center text-sm text-muted-foreground hover:text-foreground">
        <ArrowLeft className="h-4 w-4 mr-1" />All channels
      </Link>

      <header className="flex flex-col gap-3 sm:flex-row sm:items-start sm:justify-between sm:gap-4">
        <div className="space-y-1 min-w-0">
          <h1 className="text-2xl font-semibold truncate">{collection.name}</h1>
          <p className="text-sm text-muted-foreground">{collection.description || 'No description'}</p>
          {collection.tone && (
            <p className="text-xs text-muted-foreground italic">Tone: {collection.tone}</p>
          )}
        </div>
        <div className="flex gap-2 shrink-0 flex-wrap">
          <Button onClick={copyFeed} variant="outline" size="sm">
            {copiedFeed ? <><Check className="h-4 w-4 mr-2" />Copied</> : <><Copy className="h-4 w-4 mr-2" />Copy feed URL</>}
          </Button>
          <Button onClick={() => setNewOpen(true)} size="sm">
            <Plus className="h-4 w-4 mr-2" />New recording
          </Button>
        </div>
      </header>

      <section className="space-y-3">
        {items.length === 0 && (
          <div className="rounded-2xl border border-dashed border-border p-12 text-center space-y-3">
            <p className="text-muted-foreground">No recordings in this channel yet.</p>
            <Button onClick={() => setNewOpen(true)} variant="outline">
              Create the first recording
            </Button>
          </div>
        )}

        {items.map((r) => (
          <RecordingRow
            key={r.id}
            recording={r}
            onUpdate={(fresh) => setItems((prev) => prev.map((x) => x.id === fresh.id ? fresh : x))}
            onMove={() => setMoving(r)}
            onDelete={() => handleDelete(r)}
          />
        ))}
      </section>

      <NewRecordingDialog
        open={newOpen}
        onOpenChange={setNewOpen}
        collectionId={collection.id}
        defaultTtsModel={collection.defaultTtsModel}
        defaultVoice={collection.defaultVoice}
        defaultLanguage={collection.defaultLanguage}
        onCreated={handleCreated}
      />

      {moving && (
        <MoveRecordingDialog
          open={!!moving}
          onOpenChange={(open) => !open && setMoving(null)}
          recording={moving}
          onMoved={handleMoved}
        />
      )}
    </div>
  );
}

function RecordingRow({
  recording,
  onUpdate,
  onMove,
  onDelete,
}: {
  recording: TtsRecordingV1;
  onUpdate: (r: TtsRecordingV1) => void;
  onMove: () => void;
  onDelete: () => void;
}) {
  const live = useRecordingStatus(recording);
  useEffect(() => { if (live && live !== recording) onUpdate(live); }, [live, recording, onUpdate]);

  if (!live) return null;

  return (
    <div className="space-y-2">
      <AudioPlayer
        recording={live}
        actions={
          <DropdownMenu>
            <DropdownMenuTrigger asChild>
              <Button variant="ghost" size="icon" className="h-8 w-8 text-muted-foreground hover:text-foreground">
                <MoreVertical className="h-4 w-4" />
              </Button>
            </DropdownMenuTrigger>
            <DropdownMenuContent align="end">
              <DropdownMenuItem onClick={onMove}>
                <FolderInput className="h-4 w-4 mr-2" />Move…
              </DropdownMenuItem>
              <DropdownMenuItem onClick={onDelete} className="text-destructive">
                <Trash2 className="h-4 w-4 mr-2" />Delete
              </DropdownMenuItem>
            </DropdownMenuContent>
          </DropdownMenu>
        }
      />
      {live.status === 'Failed' && live.errorMessage && (
        <div className="text-xs text-destructive px-4">Error: {live.errorMessage}</div>
      )}
    </div>
  );
}
