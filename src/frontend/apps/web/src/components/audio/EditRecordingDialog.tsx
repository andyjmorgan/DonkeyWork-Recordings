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

// Split an edited transcript into spoken paragraphs on blank lines, matching how the
// transcript is stored (paragraphs joined by a blank line). Single newlines stay within
// a paragraph. Returns trimmed, non-empty paragraphs.
function splitParagraphs(text: string): string[] {
  return text
    .split(/\n\s*\n/)
    .map((p) => p.trim())
    .filter(Boolean);
}

export function EditRecordingDialog({ open, onOpenChange, recording, onSaved }: Props) {
  const [name, setName] = useState('');
  const [description, setDescription] = useState('');
  const [chapterTitle, setChapterTitle] = useState('');
  const [transcript, setTranscript] = useState('');
  const [submitting, setSubmitting] = useState(false);

  useEffect(() => {
    if (!open) return;
    setName(recording.name ?? '');
    setDescription(recording.description ?? '');
    setChapterTitle(recording.chapterTitle ?? '');
    setTranscript(recording.transcript ?? '');
  }, [open, recording]);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!name.trim()) {
      toast.error('Name is required');
      return;
    }

    const paragraphs = splitParagraphs(transcript);
    // Re-record only when the transcript actually changed — compare the normalised
    // (re-split, re-joined) form so whitespace-only edits don't trigger a synthesis.
    const transcriptChanged = paragraphs.join('\n\n') !== splitParagraphs(recording.transcript ?? '').join('\n\n');

    if (transcriptChanged && paragraphs.length === 0) {
      toast.error('Transcript cannot be empty');
      return;
    }

    setSubmitting(true);
    try {
      // Metadata patch first (name/chapter/description), then re-record if the text changed.
      let saved = await recordings.update(recording.id, {
        name: name.trim(),
        description: description.trim(),
        chapterTitle: chapterTitle.trim(),
      });

      if (transcriptChanged) {
        saved = await recordings.regenerate(recording.id, { paragraphs });
        toast.success('Re-recording audio…');
      } else {
        toast.success('Recording updated');
      }

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
      <DialogContent className="sm:max-w-2xl">
        <form onSubmit={handleSubmit}>
          <DialogHeader>
            <DialogTitle>Edit recording</DialogTitle>
            <DialogDescription>
              Update the details, or edit the transcript to re-record the audio. Saving an edited
              transcript re-synthesises the episode and replaces the existing file.
            </DialogDescription>
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
              <Textarea id="recDesc" value={description} onChange={(e) => setDescription(e.target.value)} rows={2} placeholder="Show-notes for this episode." />
            </div>
            <div className="space-y-2">
              <Label htmlFor="recTranscript">Transcript</Label>
              <Textarea
                id="recTranscript"
                value={transcript}
                onChange={(e) => setTranscript(e.target.value)}
                rows={12}
                className="font-mono text-xs leading-relaxed"
                placeholder="The spoken text. Separate paragraphs with a blank line."
              />
              <p className="text-xs text-muted-foreground">
                Separate spoken paragraphs with a blank line. Editing this and saving will re-record the audio.
              </p>
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
