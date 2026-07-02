import { useEffect, useLayoutEffect, useRef, useState } from 'react';
import { Link, useParams } from 'react-router-dom';
import { ArrowLeft, Copy, Check, MoreVertical, Trash2, FolderInput, Podcast, Pencil, Download } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { DropdownMenu, DropdownMenuContent, DropdownMenuItem, DropdownMenuTrigger } from '@/components/ui/dropdown-menu';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { backlog, collections, recordings, type AudioCollectionV1, type TtsRecordingV1 } from '@/lib/api';
import { AudioPlayer } from '@/components/audio/AudioPlayer';
import { BacklogSection } from '@/components/audio/BacklogSection';
import { MoveRecordingDialog } from '@/components/audio/MoveRecordingDialog';
import { EditRecordingDialog } from '@/components/audio/EditRecordingDialog';
import { useRecordingStatus } from '@/hooks/useRecordingStatus';
import { useAuthStore } from '@/store/auth';
import { cn, withCacheBust } from '@/lib/utils';
import { toast } from 'sonner';

// Newest first: most recently created at the top, oldest at the bottom.
// Falls back to sequenceNumber (assigned max+1 on create, so higher = newer)
// when timestamps tie or are missing.
function byNewestFirst(a: TtsRecordingV1, b: TtsRecordingV1): number {
  const at = Date.parse(a.createdAt);
  const bt = Date.parse(b.createdAt);
  if (Number.isFinite(at) && Number.isFinite(bt) && at !== bt) return bt - at;
  return (b.sequenceNumber ?? 0) - (a.sequenceNumber ?? 0);
}

function CollapsibleDescription({ text }: { text?: string | null }) {
  const [expanded, setExpanded] = useState(false);
  const [overflows, setOverflows] = useState(false);
  const ref = useRef<HTMLParagraphElement>(null);

  // Detect whether the clamped text is actually truncated so we only show the
  // toggle when there's something hidden. Re-measure when the text changes.
  useLayoutEffect(() => {
    const el = ref.current;
    if (!el) return;
    setOverflows(el.scrollHeight > el.clientHeight + 1);
  }, [text]);

  return (
    <div className="space-y-1">
      <p
        ref={ref}
        className={cn('text-sm text-muted-foreground', !expanded && 'line-clamp-2')}
      >
        {text || 'No description'}
      </p>
      {(overflows || expanded) && (
        <button
          type="button"
          onClick={() => setExpanded((v) => !v)}
          className="text-xs font-medium text-muted-foreground hover:text-foreground"
        >
          {expanded ? 'Show less' : 'Show more'}
        </button>
      )}
    </div>
  );
}

export function ChannelDetailPage() {
  const { id } = useParams<{ id: string }>();
  const [collection, setCollection] = useState<AudioCollectionV1 | null>(null);
  const [items, setItems] = useState<TtsRecordingV1[]>([]);
  const [loading, setLoading] = useState(true);
  const [moving, setMoving] = useState<TtsRecordingV1 | null>(null);
  const [editing, setEditing] = useState<TtsRecordingV1 | null>(null);
  const [copiedFeed, setCopiedFeed] = useState(false);
  const [pendingBacklogCount, setPendingBacklogCount] = useState(0);
  const userId = useAuthStore((s) => s.user?.id);

  const load = async () => {
    if (!id) return;
    setLoading(true);
    try {
      const [coll, list, pendingBacklog] = await Promise.all([
        collections.get(id),
        collections.listRecordings(id, 0, 500),
        backlog.list(id, 'Pending', 0, 1),
      ]);
      setCollection(coll);
      setItems([...list.items].sort(byNewestFirst));
      setPendingBacklogCount(pendingBacklog.totalCount);
    } catch (err) {
      toast.error(err instanceof Error ? err.message : 'Failed to load channel');
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => { load(); }, [id]);

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

  const handleEdited = (updated: TtsRecordingV1) => {
    setItems((prev) => prev.map((x) => x.id === updated.id ? updated : x));
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

  const feedUrl = userId ? `${window.location.origin}/feeds/${userId}/${collection.id}.xml` : '';
  // The podcast:// scheme nudges iOS to open the feed in Apple Podcasts (which has no in-app "add by URL").
  const podcastUrl = feedUrl.replace(/^https?:\/\//, 'podcast://');

  return (
    <div className="p-4 sm:p-8 mx-auto max-w-4xl space-y-6">
      <Link to="/channels" className="inline-flex items-center text-sm text-muted-foreground hover:text-foreground">
        <ArrowLeft className="h-4 w-4 mr-1" />All channels
      </Link>

      <header className="space-y-2">
        <div className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between sm:gap-4">
          <h1 className="text-2xl font-semibold truncate min-w-0">{collection.name}</h1>
          <div className="flex items-center gap-2 shrink-0">
            <Button onClick={copyFeed} variant="outline" size="icon" title={copiedFeed ? 'Copied' : 'Copy feed URL'}>
              {copiedFeed ? <Check className="h-4 w-4" /> : <Copy className="h-4 w-4" />}
            </Button>
            {podcastUrl && (
              <Button asChild variant="outline" size="icon" title="Add to Apple Podcasts">
                <a href={podcastUrl}>
                  <Podcast className="h-4 w-4 text-[#9933CC]" />
                </a>
              </Button>
            )}
          </div>
        </div>
        {/* Description spans the full width below the title row, rather than being
            squeezed into the title's column next to the action buttons. */}
        <CollapsibleDescription text={collection.description} />
      </header>

      <Tabs defaultValue="episodes">
        <TabsList className="justify-start">
          <TabsTrigger value="episodes">Episodes</TabsTrigger>
          <TabsTrigger value="backlog">
            Backlog
            {pendingBacklogCount > 0 && (
              <span className="ml-2 rounded-full bg-accent/20 px-2 py-0.5 text-xs text-accent">
                {pendingBacklogCount}
              </span>
            )}
          </TabsTrigger>
        </TabsList>

        <TabsContent value="episodes" className="mt-4">
          <section className="space-y-3">
            {items.length === 0 && (
              <div className="rounded-2xl border border-dashed border-border p-12 text-center space-y-2">
                <p className="text-muted-foreground">No recordings in this channel yet.</p>
                <p className="text-sm text-muted-foreground">
                  Recordings are created through the MCP server or REST API — see your Profile for setup.
                </p>
              </div>
            )}

            {items.map((r) => (
              <RecordingRow
                key={r.id}
                recording={r}
                onUpdate={(fresh) => setItems((prev) => prev.map((x) => x.id === fresh.id ? fresh : x))}
                onEdit={() => setEditing(r)}
                onMove={() => setMoving(r)}
                onDelete={() => handleDelete(r)}
              />
            ))}
          </section>
        </TabsContent>

        <TabsContent value="backlog" className="mt-4">
          {id && <BacklogSection collectionId={id} onPendingCountChange={setPendingBacklogCount} />}
        </TabsContent>
      </Tabs>

      {editing && (
        <EditRecordingDialog
          open={!!editing}
          onOpenChange={(open) => !open && setEditing(null)}
          recording={editing}
          onSaved={handleEdited}
        />
      )}

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
  onEdit,
  onMove,
  onDelete,
}: {
  recording: TtsRecordingV1;
  onUpdate: (r: TtsRecordingV1) => void;
  onEdit: () => void;
  onMove: () => void;
  onDelete: () => void;
}) {
  const live = useRecordingStatus(recording);
  useEffect(() => { if (live && live !== recording) onUpdate(live); }, [live, recording, onUpdate]);

  if (!live) return null;

  const downloadName = `${live.chapterTitle || live.name || 'recording'}.mp3`;

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
              <DropdownMenuItem onClick={onEdit}>
                <Pencil className="h-4 w-4 mr-2" />Edit…
              </DropdownMenuItem>
              {live.status === 'Ready' && live.filePath && (
                <DropdownMenuItem asChild>
                  <a href={withCacheBust(live.filePath, live.updatedAt ?? live.durationSeconds)} download={downloadName}>
                    <Download className="h-4 w-4 mr-2" />Download
                  </a>
                </DropdownMenuItem>
              )}
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
