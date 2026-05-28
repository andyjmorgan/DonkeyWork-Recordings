import { useEffect, useState } from 'react';
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog';
import { Button } from '@/components/ui/button';
import { Label } from '@/components/ui/label';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { collections, recordings, type AudioCollectionV1, type TtsRecordingV1 } from '@/lib/api';
import { toast } from 'sonner';

interface Props {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  recording: TtsRecordingV1;
  onMoved: (recording: TtsRecordingV1) => void;
}

const UNFILED = '__unfiled__';

export function MoveRecordingDialog({ open, onOpenChange, recording, onMoved }: Props) {
  const [target, setTarget] = useState<string>(recording.collectionId ?? UNFILED);
  const [allCollections, setAllCollections] = useState<AudioCollectionV1[]>([]);
  const [submitting, setSubmitting] = useState(false);

  useEffect(() => {
    if (!open) return;
    setTarget(recording.collectionId ?? UNFILED);
    collections.list(0, 200)
      .then((r) => setAllCollections(r.items))
      .catch(() => toast.error('Could not load channels'));
  }, [open, recording.collectionId]);

  const handleSubmit = async () => {
    setSubmitting(true);
    try {
      const moved = await recordings.move(recording.id, {
        collectionId: target === UNFILED ? null : target,
      });
      toast.success('Moved');
      onMoved(moved);
      onOpenChange(false);
    } catch (err) {
      toast.error(err instanceof Error ? err.message : 'Move failed');
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="sm:max-w-md">
        <DialogHeader>
          <DialogTitle>Move recording</DialogTitle>
          <DialogDescription>
            Reassign “{recording.chapterTitle || recording.name}” to another channel, or unfile it.
          </DialogDescription>
        </DialogHeader>

        <div className="grid gap-3 py-4">
          <Label htmlFor="target">Target channel</Label>
          <Select value={target} onValueChange={setTarget}>
            <SelectTrigger id="target"><SelectValue /></SelectTrigger>
            <SelectContent>
              <SelectItem value={UNFILED}>(Unfiled)</SelectItem>
              {allCollections.map((c) => (
                <SelectItem key={c.id} value={c.id}>{c.name}</SelectItem>
              ))}
            </SelectContent>
          </Select>
        </div>

        <DialogFooter>
          <Button type="button" variant="outline" onClick={() => onOpenChange(false)} disabled={submitting}>Cancel</Button>
          <Button type="button" onClick={handleSubmit} disabled={submitting}>
            {submitting ? 'Moving…' : 'Move'}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
