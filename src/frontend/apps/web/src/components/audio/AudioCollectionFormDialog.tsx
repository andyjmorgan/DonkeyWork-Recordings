import { useEffect, useMemo, useRef, useState } from 'react';
import { Loader2, Volume2 } from 'lucide-react';
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Textarea } from '@/components/ui/textarea';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import { collections, voices, type AudioCollectionV1, type VoicesResponse } from '@/lib/api';
import { gradeRank } from '@/lib/voiceGrades';
import { toast } from 'sonner';

interface Props {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  editing?: AudioCollectionV1 | null;
  onSaved: (collection: AudioCollectionV1) => void;
}

const INHERIT_VALUE = '__inherit__';

export function AudioCollectionFormDialog({ open, onOpenChange, editing, onSaved }: Props) {
  const [name, setName] = useState('');
  const [description, setDescription] = useState('');
  const [coverImagePath, setCoverImagePath] = useState('');
  const [defaultVoice, setDefaultVoice] = useState('');
  const [defaultLanguage, setDefaultLanguage] = useState('en-US');
  const [submitting, setSubmitting] = useState(false);

  const [voiceData, setVoiceData] = useState<VoicesResponse | null>(null);
  const [voicesError, setVoicesError] = useState(false);
  const [testing, setTesting] = useState(false);
  const audioRef = useRef<HTMLAudioElement | null>(null);
  const audioUrlRef = useRef<string | null>(null);

  useEffect(() => {
    if (open) {
      setName(editing?.name ?? '');
      setDescription(editing?.description ?? '');
      setCoverImagePath(editing?.coverImagePath ?? '');
      setDefaultVoice(editing?.defaultVoice ?? '');
      setDefaultLanguage(editing?.defaultLanguage ?? 'en-US');
    }
  }, [open, editing]);

  useEffect(() => {
    if (!open || voiceData || voicesError) return;
    voices.list()
      .then((v) => setVoiceData(v))
      .catch(() => setVoicesError(true));
  }, [open, voiceData, voicesError]);

  useEffect(() => () => {
    if (audioUrlRef.current) URL.revokeObjectURL(audioUrlRef.current);
  }, []);

  const allVoices = voiceData?.voices ?? [];

  const languages = useMemo(() => {
    const set = new Set<string>();
    allVoices.forEach((v) => set.add(v.language));
    if (defaultLanguage) set.add(defaultLanguage);
    return [...set].sort();
  }, [allVoices, defaultLanguage]);

  // Hide emotion variants, scope to the picked language, and order best-grade first.
  const filteredVoices = useMemo(
    () => allVoices
      .filter((v) => !v.emotion && (!defaultLanguage || v.language === defaultLanguage))
      .sort((a, b) => gradeRank(a.rating) - gradeRank(b.rating) || a.name.localeCompare(b.name)),
    [allVoices, defaultLanguage],
  );

  const selectedSampleUrl = defaultVoice
    ? allVoices.find((v) => v.id === defaultVoice)?.sampleUrl
    : undefined;

  const playSample = (url: string) => {
    if (audioUrlRef.current) URL.revokeObjectURL(audioUrlRef.current);
    audioUrlRef.current = null;
    const audio = audioRef.current ?? new Audio();
    audioRef.current = audio;
    audio.src = url;
    audio.play().catch((e) => toast.error(`Playback blocked: ${e.message ?? e}`));
  };

  const handleLanguageChange = (value: string) => {
    setDefaultLanguage(value);
    if (defaultVoice && !allVoices.some((v) => v.id === defaultVoice && v.language === value)) {
      setDefaultVoice('');
    }
  };

  const handleTest = async () => {
    setTesting(true);
    try {
      const blob = await voices.preview({
        voice: defaultVoice || voiceData?.defaultVoice || undefined,
        language: defaultLanguage || 'en-US',
      });
      if (audioUrlRef.current) URL.revokeObjectURL(audioUrlRef.current);
      const url = URL.createObjectURL(blob);
      audioUrlRef.current = url;
      const audio = audioRef.current ?? new Audio();
      audioRef.current = audio;
      audio.src = url;
      // Fire-and-forget: awaiting play() can hang indefinitely if autoplay
      // is blocked, which would leave the button stuck on "Synthesising…".
      audio.play().catch((e) => toast.error(`Playback blocked: ${e.message ?? e}`));
    } catch (err) {
      toast.error(err instanceof Error ? err.message : 'Preview failed');
    } finally {
      setTesting(false);
    }
  };

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
        // Always send (even when blank) so clearing the field resets the channel
        // to the default cover; the feed falls back to the default when blank.
        coverImagePath: coverImagePath.trim(),
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

  const voicesReady = voiceData !== null || voicesError;

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="sm:max-w-2xl">
        <form onSubmit={handleSubmit}>
          <DialogHeader>
            <DialogTitle>{editing ? 'Edit channel' : 'New channel'}</DialogTitle>
            <DialogDescription>
              Channels group recordings into a podcast feed. The default voice is used for every recording in this channel.
            </DialogDescription>
          </DialogHeader>

          <div className="grid gap-4 py-4">
            <div className="space-y-2">
              <Label htmlFor="name">Name</Label>
              <Input id="name" value={name} onChange={(e) => setName(e.target.value)} placeholder="Daily News" autoFocus />
            </div>
            <div className="space-y-2">
              <Label htmlFor="description">Description</Label>
              <Textarea id="description" value={description} onChange={(e) => setDescription(e.target.value)} rows={5} placeholder="What this channel is about." />
            </div>
            <div className="space-y-2">
              <Label htmlFor="coverImagePath">Cover image URL</Label>
              <Input id="coverImagePath" type="url" value={coverImagePath} onChange={(e) => setCoverImagePath(e.target.value)} placeholder="https://…" />
              <p className="text-xs text-muted-foreground">
                Used as the podcast artwork for this channel. Leave blank to use the DonkeyWork default cover.
              </p>
            </div>
            <div className="grid grid-cols-2 gap-3">
              <div className="space-y-2">
                <Label htmlFor="defaultVoice">Default voice</Label>
                <div className="flex gap-2">
                  <Select
                    value={defaultVoice || INHERIT_VALUE}
                    onValueChange={(v) => setDefaultVoice(v === INHERIT_VALUE ? '' : v)}
                    disabled={!voicesReady}
                  >
                    <SelectTrigger id="defaultVoice" className="flex-1">
                      <SelectValue placeholder={voiceData ? 'Default (Heart)' : 'Loading…'} />
                    </SelectTrigger>
                    <SelectContent>
                      <SelectItem value={INHERIT_VALUE}>Default (Heart)</SelectItem>
                      {filteredVoices.map((v) => (
                        <SelectItem key={v.id} value={v.id}>{v.name}{v.rating ? ` · ${v.rating}` : ''}</SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                  <Button
                    type="button"
                    variant="outline"
                    size="icon"
                    disabled={!selectedSampleUrl}
                    onClick={() => selectedSampleUrl && playSample(selectedSampleUrl)}
                    title="Play sample"
                  >
                    <Volume2 className="h-4 w-4" />
                  </Button>
                </div>
              </div>
              <div className="space-y-2">
                <Label htmlFor="defaultLanguage">Default language</Label>
                <Select value={defaultLanguage || 'en-US'} onValueChange={handleLanguageChange} disabled={!voicesReady}>
                  <SelectTrigger id="defaultLanguage">
                    <SelectValue placeholder={voiceData ? 'Pick a language' : 'Loading…'} />
                  </SelectTrigger>
                  <SelectContent>
                    {languages.map((lang) => (
                      <SelectItem key={lang} value={lang}>{lang}</SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>
            </div>
            <div>
              <Button
                type="button"
                variant="outline"
                size="sm"
                onClick={handleTest}
                disabled={testing || !voicesReady}
              >
                {testing ? <Loader2 className="h-4 w-4 mr-2 animate-spin" /> : <Volume2 className="h-4 w-4 mr-2" />}
                {testing ? 'Synthesising…' : 'Test voice'}
              </Button>
              <p className="mt-2 text-xs text-muted-foreground">
                Synthesises "testing, one, two, three" with the selected voice.
              </p>
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
