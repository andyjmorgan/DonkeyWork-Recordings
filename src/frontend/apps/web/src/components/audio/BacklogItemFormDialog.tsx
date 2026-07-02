import { useEffect, useState } from 'react';
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Textarea } from '@/components/ui/textarea';
import { backlog, type BacklogItemV1 } from '@/lib/api';
import { toast } from 'sonner';

interface Props {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  collectionId: string;
  /** Item to edit; omit to create a new one. */
  item?: BacklogItemV1 | null;
  onSaved: (item: BacklogItemV1) => void;
}

export function BacklogItemFormDialog({ open, onOpenChange, collectionId, item, onSaved }: Props) {
  const [title, setTitle] = useState('');
  const [content, setContent] = useState('');
  const [sourceUrl, setSourceUrl] = useState('');
  const [notes, setNotes] = useState('');
  const [submitting, setSubmitting] = useState(false);

  useEffect(() => {
    if (!open) return;
    setTitle(item?.title ?? '');
    setContent(item?.content ?? '');
    setSourceUrl(item?.sourceUrl ?? '');
    setNotes(item?.notes ?? '');
  }, [open, item]);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!title.trim()) {
      toast.error('Title is required');
      return;
    }

    setSubmitting(true);
    try {
      const payload = {
        title: title.trim(),
        content: content.trim(),
        sourceUrl: sourceUrl.trim(),
        notes: notes.trim(),
      };
      const saved = item
        ? await backlog.update(item.id, payload)
        : await backlog.create(collectionId, payload);
      toast.success(item ? 'Backlog item updated' : 'Added to backlog');
      onSaved(saved);
      onOpenChange(false);
    } catch (err) {
      toast.error(err instanceof Error ? err.message : 'Save failed');
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="sm:max-w-2xl">
        <form onSubmit={handleSubmit}>
          <DialogHeader>
            <DialogTitle>{item ? 'Edit backlog item' : 'Add backlog item'}</DialogTitle>
            <DialogDescription>
              Queue a story, link, or note for this channel. Pending items are picked up the next
              time an episode is generated for the channel.
            </DialogDescription>
          </DialogHeader>

          <div className="grid gap-4 py-4">
            <div className="space-y-2">
              <Label htmlFor="backlogTitle">Title</Label>
              <Input id="backlogTitle" value={title} onChange={(e) => setTitle(e.target.value)} autoFocus placeholder="Short headline for the item" />
            </div>
            <div className="space-y-2">
              <Label htmlFor="backlogContent">Content</Label>
              <Textarea
                id="backlogContent"
                value={content}
                onChange={(e) => setContent(e.target.value)}
                rows={8}
                placeholder="The material to work into the episode — paste text, bullet points, or a summary."
              />
            </div>
            <div className="space-y-2">
              <Label htmlFor="backlogSourceUrl">Source URL (optional)</Label>
              <Input id="backlogSourceUrl" type="url" value={sourceUrl} onChange={(e) => setSourceUrl(e.target.value)} placeholder="https://…" />
            </div>
            <div className="space-y-2">
              <Label htmlFor="backlogNotes">Notes (optional)</Label>
              <Textarea
                id="backlogNotes"
                value={notes}
                onChange={(e) => setNotes(e.target.value)}
                rows={2}
                placeholder="Editorial guidance, e.g. 'mention briefly at the end'."
              />
            </div>
          </div>

          <DialogFooter>
            <Button type="button" variant="outline" onClick={() => onOpenChange(false)} disabled={submitting}>Cancel</Button>
            <Button type="submit" disabled={submitting}>{submitting ? 'Saving…' : item ? 'Save' : 'Add item'}</Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  );
}
