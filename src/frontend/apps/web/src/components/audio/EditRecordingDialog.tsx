import { useEffect, useState } from 'react';
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Textarea } from '@/components/ui/textarea';
import { recordings, type TtsRecordingV1 } from '@/lib/api';
import { toast } from 'sonner';

interface Props {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  recording: TtsRecordingV1;
  onSaved: (recording: TtsRecordingV1) => void;
}

export function EditRecordingDialog({ open, onOpenChange, recording, onSaved }: Props) {
  const [name, setName] = useState('');
  const [description, setDescription] = useState('');
  const [chapterTitle, setChapterTitle] = useState('');
  const [submitting, setSubmitting] = useState(false);

  useEffect(() => {
    if (!open) return;
    setName(recording.name ?? '');
    setDescription(recording.description ?? '');
    setChapterTitle(recording.chapterTitle ?? '');
  }, [open, recording]);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!name.trim()) {
      toast.error('Name is required');
      return;
    }
    setSubmitting(true);
    try {
      const saved = await recordings.update(recording.id, {
        name: name.trim(),
        description: description.trim(),
        chapterTitle: chapterTitle.trim(),
      });
      toast.success('Recording updated');
      onSaved(saved);
      onOpenChange(false);
    } catch (err) {
      toast.error(err instanceof Error ? err.message : 'Update failed');
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="sm:max-w-md">
        <form onSubmit={handleSubmit}>
          <DialogHeader>
            <DialogTitle>Edit recording</DialogTitle>
            <DialogDescription>Update the recording's details. This does not re-synthesise audio.</DialogDescription>
          </DialogHeader>

          <div className="grid gap-4 py-4">
            <div className="space-y-2">
              <Label htmlFor="recName">Name</Label>
              <Input id="recName" value={name} onChange={(e) => setName(e.target.value)} autoFocus />
            </div>
            <div className="space-y-2">
              <Label htmlFor="recChapter">Chapter title (optional)</Label>
              <Input id="recChapter" value={chapterTitle} onChange={(e) => setChapterTitle(e.target.value)} placeholder="Shown in the channel in place of the name" />
            </div>
            <div className="space-y-2">
              <Label htmlFor="recDesc">Description</Label>
              <Textarea id="recDesc" value={description} onChange={(e) => setDescription(e.target.value)} rows={3} placeholder="Show-notes for this episode." />
            </div>
          </div>

          <DialogFooter>
            <Button type="button" variant="outline" onClick={() => onOpenChange(false)} disabled={submitting}>Cancel</Button>
            <Button type="submit" disabled={submitting}>{submitting ? 'Saving…' : 'Save'}</Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  );
}
