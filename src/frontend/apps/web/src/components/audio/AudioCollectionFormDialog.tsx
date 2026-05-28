import { useEffect, useState } from 'react';
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Textarea } from '@/components/ui/textarea';
import { collections, type AudioCollectionV1 } from '@/lib/api';
import { toast } from 'sonner';

interface Props {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  editing?: AudioCollectionV1 | null;
  onSaved: (collection: AudioCollectionV1) => void;
}

export function AudioCollectionFormDialog({ open, onOpenChange, editing, onSaved }: Props) {
  const [name, setName] = useState('');
  const [description, setDescription] = useState('');
  const [tone, setTone] = useState('');
  const [defaultVoice, setDefaultVoice] = useState('');
  const [defaultLanguage, setDefaultLanguage] = useState('en-US');
  const [submitting, setSubmitting] = useState(false);

  useEffect(() => {
    if (open) {
      setName(editing?.name ?? '');
      setDescription(editing?.description ?? '');
      setTone(editing?.tone ?? '');
      setDefaultVoice(editing?.defaultVoice ?? '');
      setDefaultLanguage(editing?.defaultLanguage ?? 'en-US');
    }
  }, [open, editing]);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!name.trim()) {
      toast.error('Name is required');
      return;
    }
    setSubmitting(true);
    try {
      const payload = {
        name: name.trim(),
        description: description.trim(),
        tone: tone.trim() || undefined,
        defaultVoice: defaultVoice.trim() || undefined,
        defaultLanguage: defaultLanguage.trim() || undefined,
      };
      const result = editing
        ? await collections.update(editing.id, payload)
        : await collections.create(payload);
      toast.success(editing ? 'Channel updated' : 'Channel created');
      onSaved(result);
      onOpenChange(false);
    } catch (err) {
      toast.error(err instanceof Error ? err.message : 'Save failed');
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="sm:max-w-lg">
        <form onSubmit={handleSubmit}>
          <DialogHeader>
            <DialogTitle>{editing ? 'Edit channel' : 'New channel'}</DialogTitle>
            <DialogDescription>
              Channels group recordings into a podcast feed. Tone is passed to gpt-oss for every recording in this channel.
            </DialogDescription>
          </DialogHeader>

          <div className="grid gap-4 py-4">
            <div className="space-y-2">
              <Label htmlFor="name">Name</Label>
              <Input id="name" value={name} onChange={(e) => setName(e.target.value)} placeholder="Daily News" autoFocus />
            </div>
            <div className="space-y-2">
              <Label htmlFor="description">Description</Label>
              <Textarea id="description" value={description} onChange={(e) => setDescription(e.target.value)} rows={2} placeholder="What this channel is about." />
            </div>
            <div className="space-y-2">
              <Label htmlFor="tone">Tone (free-text)</Label>
              <Textarea
                id="tone"
                value={tone}
                onChange={(e) => setTone(e.target.value)}
                rows={3}
                placeholder="e.g. 'serious news anchor with measured pace and clear enunciation'"
              />
              <p className="text-xs text-muted-foreground">
                Threaded into every recording's preprocessor prompt. Leave blank for a neutral conversational tone.
              </p>
            </div>
            <div className="grid grid-cols-2 gap-3">
              <div className="space-y-2">
                <Label htmlFor="defaultVoice">Default voice</Label>
                <Input id="defaultVoice" value={defaultVoice} onChange={(e) => setDefaultVoice(e.target.value)} placeholder="Magpie-Multilingual.EN-US.Aria" />
              </div>
              <div className="space-y-2">
                <Label htmlFor="defaultLanguage">Default language</Label>
                <Input id="defaultLanguage" value={defaultLanguage} onChange={(e) => setDefaultLanguage(e.target.value)} placeholder="en-US" />
              </div>
            </div>
          </div>

          <DialogFooter>
            <Button type="button" variant="outline" onClick={() => onOpenChange(false)} disabled={submitting}>Cancel</Button>
            <Button type="submit" disabled={submitting}>
              {submitting ? 'Saving…' : editing ? 'Save' : 'Create channel'}
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  );
}
