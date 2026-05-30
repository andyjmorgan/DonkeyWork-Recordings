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
import { collections, voices, type AudioCollectionV1, type TtsModelV1 } from '@/lib/api';
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
  const [tone, setTone] = useState('');
  const [defaultTtsModel, setDefaultTtsModel] = useState('');
  const [defaultVoice, setDefaultVoice] = useState('');
  const [defaultLanguage, setDefaultLanguage] = useState('en-US');
  const [submitting, setSubmitting] = useState(false);

  const [models, setModels] = useState<TtsModelV1[] | null>(null);
  const [modelsError, setModelsError] = useState(false);
  const [testing, setTesting] = useState(false);
  const audioRef = useRef<HTMLAudioElement | null>(null);
  const audioUrlRef = useRef<string | null>(null);

  useEffect(() => {
    if (open) {
      setName(editing?.name ?? '');
      setDescription(editing?.description ?? '');
      setTone(editing?.tone ?? '');
      setDefaultTtsModel(editing?.defaultTtsModel ?? '');
      setDefaultVoice(editing?.defaultVoice ?? '');
      setDefaultLanguage(editing?.defaultLanguage ?? 'en-US');
    }
  }, [open, editing]);

  useEffect(() => {
    if (!open || models || modelsError) return;
    voices.models()
      .then((m) => setModels(m))
      .catch(() => setModelsError(true));
  }, [open, models, modelsError]);

  useEffect(() => () => {
    if (audioUrlRef.current) URL.revokeObjectURL(audioUrlRef.current);
  }, []);

  // "Inherit" resolves to the system default model for the purposes of the voice picker.
  const effectiveModelKey = defaultTtsModel || models?.find((m) => m.isDefault)?.key;
  const selectedModel = models?.find((m) => m.key === effectiveModelKey) ?? null;
  const supportsVoice = selectedModel?.supportsVoiceSelection ?? false;

  const languages = useMemo(() => {
    const set = new Set<string>();
    (selectedModel?.voices ?? []).forEach((v) => set.add(v.language));
    if (defaultLanguage) set.add(defaultLanguage);
    return [...set].sort();
  }, [selectedModel, defaultLanguage]);

  // Hide emotion variants, scope to the picked language, and order best-grade first.
  const filteredVoices = useMemo(
    () => (selectedModel?.voices ?? [])
      .filter((v) => !v.emotion && (!defaultLanguage || v.language === defaultLanguage))
      .sort((a, b) => gradeRank(a.rating) - gradeRank(b.rating) || a.name.localeCompare(b.name)),
    [selectedModel, defaultLanguage],
  );

  const selectedSampleUrl = defaultVoice
    ? selectedModel?.voices.find((v) => v.id === defaultVoice)?.sampleUrl
    : undefined;

  const playSample = (url: string) => {
    if (audioUrlRef.current) URL.revokeObjectURL(audioUrlRef.current);
    audioUrlRef.current = null;
    const audio = audioRef.current ?? new Audio();
    audioRef.current = audio;
    audio.src = url;
    audio.play().catch((e) => toast.error(`Playback blocked: ${e.message ?? e}`));
  };

  const handleModelChange = (value: string) => {
    setDefaultTtsModel(value === INHERIT_VALUE ? '' : value);
    setDefaultVoice('');
  };

  const handleLanguageChange = (value: string) => {
    setDefaultLanguage(value);
    if (defaultVoice && selectedModel && !selectedModel.voices.some((v) => v.id === defaultVoice && v.language === value)) {
      setDefaultVoice('');
    }
  };

  const handleTest = async () => {
    if (supportsVoice && !defaultVoice) {
      toast.error('Pick a voice to preview.');
      return;
    }
    setTesting(true);
    try {
      const blob = await voices.preview({
        model: effectiveModelKey,
        voice: defaultVoice || selectedModel?.defaultVoice || '',
        language: defaultLanguage || 'en-US',
        tone: tone.trim() || undefined,
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
        tone: tone.trim() || undefined,
        defaultTtsModel: defaultTtsModel || undefined,
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

  const modelsReady = models !== null || modelsError;

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
                <Label htmlFor="defaultModel">Default model</Label>
                <Select
                  value={defaultTtsModel || INHERIT_VALUE}
                  onValueChange={handleModelChange}
                  disabled={!modelsReady}
                >
                  <SelectTrigger id="defaultModel">
                    <SelectValue placeholder={models ? 'Inherit system default' : 'Loading…'} />
                  </SelectTrigger>
                  <SelectContent>
                    <SelectItem value={INHERIT_VALUE}>Inherit system default</SelectItem>
                    {(models ?? []).map((m) => (
                      <SelectItem key={m.key} value={m.key}>{m.displayName}</SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>
              <div className="space-y-2">
                <Label htmlFor="defaultLanguage">Default language</Label>
                <Select value={defaultLanguage || 'en-US'} onValueChange={handleLanguageChange} disabled={!modelsReady}>
                  <SelectTrigger id="defaultLanguage">
                    <SelectValue placeholder={models ? 'Pick a language' : 'Loading…'} />
                  </SelectTrigger>
                  <SelectContent>
                    {languages.map((lang) => (
                      <SelectItem key={lang} value={lang}>{lang}</SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>
            </div>
            {supportsVoice && (
              <div className="space-y-2">
                <Label htmlFor="defaultVoice">Default voice</Label>
                <div className="flex gap-2">
                  <Select
                    value={defaultVoice || INHERIT_VALUE}
                    onValueChange={(v) => setDefaultVoice(v === INHERIT_VALUE ? '' : v)}
                    disabled={!modelsReady}
                  >
                    <SelectTrigger id="defaultVoice" className="flex-1">
                      <SelectValue placeholder={models ? 'Inherit model default' : 'Loading…'} />
                    </SelectTrigger>
                    <SelectContent>
                      <SelectItem value={INHERIT_VALUE}>Inherit model default</SelectItem>
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
            )}
            <div>
              <Button
                type="button"
                variant="outline"
                size="sm"
                onClick={handleTest}
                disabled={testing || !modelsReady || (supportsVoice && !defaultVoice)}
              >
                {testing ? <Loader2 className="h-4 w-4 mr-2 animate-spin" /> : <Volume2 className="h-4 w-4 mr-2" />}
                {testing ? 'Synthesising…' : 'Test voice + tone'}
              </Button>
              <p className="mt-2 text-xs text-muted-foreground">
                Synthesises "testing, one, two, three" with the current model/voice and pipes it through gpt-oss using the tone above.
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
