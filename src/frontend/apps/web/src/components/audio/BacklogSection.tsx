import { useEffect, useState } from 'react';
import { ChevronDown, ChevronRight, ExternalLink, MoreVertical, Pencil, Plus, Trash2, XCircle } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { DropdownMenu, DropdownMenuContent, DropdownMenuItem, DropdownMenuTrigger } from '@/components/ui/dropdown-menu';
import { backlog, type BacklogItemV1 } from '@/lib/api';
import { BacklogItemFormDialog } from '@/components/audio/BacklogItemFormDialog';
import { cn } from '@/lib/utils';
import { toast } from 'sonner';

interface Props {
  collectionId: string;
  onPendingCountChange?: (count: number) => void;
}

// Pending items FIFO (oldest first — the order the next episode will cover them);
// history newest-first.
function byOldestFirst(a: BacklogItemV1, b: BacklogItemV1): number {
  return Date.parse(a.createdAt) - Date.parse(b.createdAt);
}

export function BacklogSection({ collectionId, onPendingCountChange }: Props) {
  const [items, setItems] = useState<BacklogItemV1[]>([]);
  const [loading, setLoading] = useState(true);
  const [showHistory, setShowHistory] = useState(false);
  const [dialogOpen, setDialogOpen] = useState(false);
  const [editing, setEditing] = useState<BacklogItemV1 | null>(null);

  const pending = items.filter((i) => i.status === 'Pending').sort(byOldestFirst);
  const history = items
    .filter((i) => i.status !== 'Pending')
    .sort((a, b) => byOldestFirst(b, a));

  useEffect(() => {
    onPendingCountChange?.(pending.length);
  }, [pending.length, onPendingCountChange]);

  const load = async () => {
    setLoading(true);
    try {
      const list = await backlog.list(collectionId, 'all');
      setItems(list.items);
    } catch (err) {
      toast.error(err instanceof Error ? err.message : 'Failed to load backlog');
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => { load(); }, [collectionId]);

  const handleSaved = (saved: BacklogItemV1) => {
    setItems((prev) => {
      const exists = prev.some((x) => x.id === saved.id);
      return exists ? prev.map((x) => (x.id === saved.id ? saved : x)) : [...prev, saved];
    });
  };

  const handleDismiss = async (item: BacklogItemV1) => {
    try {
      const dismissed = await backlog.dismiss(item.id);
      setItems((prev) => prev.map((x) => (x.id === dismissed.id ? dismissed : x)));
      toast.success('Item dismissed');
    } catch (err) {
      toast.error(err instanceof Error ? err.message : 'Dismiss failed');
    }
  };

  const handleDelete = async (item: BacklogItemV1) => {
    if (!confirm(`Delete backlog item "${item.title}"?`)) return;
    try {
      await backlog.delete(item.id);
      setItems((prev) => prev.filter((x) => x.id !== item.id));
      toast.success('Item deleted');
    } catch (err) {
      toast.error(err instanceof Error ? err.message : 'Delete failed');
    }
  };

  if (loading) return <div className="p-8 text-muted-foreground">Loading…</div>;

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between gap-4">
        <p className="text-sm text-muted-foreground min-w-0">
          Pending items are worked into the next generated episode, oldest first.
        </p>
        <Button size="sm" className="shrink-0" onClick={() => { setEditing(null); setDialogOpen(true); }}>
          <Plus className="size-4 mr-1" />Add item
        </Button>
      </div>

      {pending.length === 0 && (
        <div className="rounded-2xl border border-dashed border-border p-12 text-center space-y-2">
          <p className="text-muted-foreground">No pending backlog items.</p>
          <p className="text-sm text-muted-foreground">
            Items added here are picked up by your scheduled episode generator when it writes the next script.
          </p>
        </div>
      )}

      <div className="space-y-3">
        {pending.map((item) => (
          <BacklogRow
            key={item.id}
            item={item}
            onEdit={() => { setEditing(item); setDialogOpen(true); }}
            onDismiss={() => handleDismiss(item)}
            onDelete={() => handleDelete(item)}
          />
        ))}
      </div>

      {history.length > 0 && (
        <div className="space-y-3">
          <button
            type="button"
            onClick={() => setShowHistory((v) => !v)}
            className="inline-flex items-center gap-1 text-sm font-medium text-muted-foreground hover:text-foreground transition-all duration-200"
          >
            {showHistory ? <ChevronDown className="size-4" /> : <ChevronRight className="size-4" />}
            History ({history.length})
          </button>
          {showHistory && history.map((item) => (
            <BacklogRow key={item.id} item={item} onDelete={() => handleDelete(item)} />
          ))}
        </div>
      )}

      <BacklogItemFormDialog
        open={dialogOpen}
        onOpenChange={setDialogOpen}
        collectionId={collectionId}
        item={editing}
        onSaved={handleSaved}
      />
    </div>
  );
}

function BacklogRow({
  item,
  onEdit,
  onDismiss,
  onDelete,
}: {
  item: BacklogItemV1;
  onEdit?: () => void;
  onDismiss?: () => void;
  onDelete: () => void;
}) {
  const isPending = item.status === 'Pending';

  return (
    <div className={cn('rounded-2xl border border-border bg-card p-4 space-y-2', !isPending && 'opacity-70')}>
      <div className="flex items-start justify-between gap-3">
        <div className="min-w-0 space-y-1">
          <div className="flex items-center gap-2 min-w-0">
            <span className="font-medium truncate">{item.title}</span>
            {item.status === 'Consumed' && <Badge variant="success">Consumed</Badge>}
            {item.status === 'Dismissed' && <Badge variant="muted">Dismissed</Badge>}
          </div>
          {item.content && (
            <p className="text-sm text-muted-foreground line-clamp-2">{item.content}</p>
          )}
          {item.notes && (
            <p className="text-xs text-muted-foreground italic line-clamp-1">Note: {item.notes}</p>
          )}
          <div className="flex flex-wrap items-center gap-x-3 gap-y-1 text-xs text-muted-foreground">
            <span>Added {new Date(item.createdAt).toLocaleDateString()}</span>
            {item.sourceUrl && (
              <a
                href={item.sourceUrl}
                target="_blank"
                rel="noreferrer"
                className="inline-flex items-center gap-1 hover:text-foreground truncate max-w-60"
              >
                <ExternalLink className="size-3 shrink-0" />
                <span className="truncate">{item.sourceUrl.replace(/^https?:\/\//, '')}</span>
              </a>
            )}
            {item.status === 'Consumed' && (
              <span>
                Used {item.consumedAt ? new Date(item.consumedAt).toLocaleDateString() : ''}
                {item.consumedByRecordingName ? ` in “${item.consumedByRecordingName}”` : ''}
              </span>
            )}
          </div>
        </div>

        <DropdownMenu>
          <DropdownMenuTrigger asChild>
            <Button variant="ghost" size="icon" className="h-8 w-8 shrink-0 text-muted-foreground hover:text-foreground">
              <MoreVertical className="size-4" />
            </Button>
          </DropdownMenuTrigger>
          <DropdownMenuContent align="end">
            {onEdit && (
              <DropdownMenuItem onClick={onEdit}>
                <Pencil className="size-4 mr-2" />Edit…
              </DropdownMenuItem>
            )}
            {onDismiss && (
              <DropdownMenuItem onClick={onDismiss}>
                <XCircle className="size-4 mr-2" />Dismiss
              </DropdownMenuItem>
            )}
            <DropdownMenuItem onClick={onDelete} className="text-destructive">
              <Trash2 className="size-4 mr-2" />Delete
            </DropdownMenuItem>
          </DropdownMenuContent>
        </DropdownMenu>
      </div>
    </div>
  );
}
