import { useEffect, useMemo, useState } from 'react';
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Textarea } from '@/components/ui/textarea';
import { Select, SelectContent, SelectGroup, SelectItem, SelectLabel, SelectTrigger, SelectValue } from '@/components/ui/select';
import { recordings, voices, type TtsModelV1, type TtsRecordingV1 } from '@/lib/api';
import { toast } from 'sonner';

interface Props {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  collectionId?: string;
  defaultTtsModel?: string;
  defaultVoice?: string;
  defaultLanguage?: string;
  onCreated: (recording: TtsRecordingV1) => void;
}

const INHERIT = '__inherit__';

export function NewRecordingDialog({ open, onOpenChange, collectionId, defaultTtsModel, defaultVoice, defaultLanguage, onCreated }: Props) {
  const [name, setName] = useState('');
  const [text, setText] = useState('');
  const [ttsModel, setTtsModel] = useState<string>(INHERIT);
  const [voice, setVoice] = useState<string>(INHERIT);
  const [language, setLanguage] = useState<string>(defaultLanguage ?? 'en-US');
  const [models, setModels] = useState<TtsModelV1[]>([]);
  const [modelsLoading, setModelsLoading] = useState(false);
  const [submitting, setSubmitting] = useState(false);

  useEffect(() => {
    if (!open) return;
    setName('');
    setText('');
    setTtsModel(INHERIT);
    setVoice(INHERIT);
    setLanguage(defaultLanguage ?? 'en-US');
  }, [open, defaultLanguage]);

  useEffect(() => {
    if (!open || models.length > 0) return;
    setModelsLoading(true);
    voices.models()
      .then((m) => setModels(m))
      .catch(() => toast.error('Could not load TTS models'))
      .finally(() => setModelsLoading(false));
  }, [open, models.length]);

  // When "inherit" is selected, the effective model is the channel default, else the system default.
  const effectiveModelKey = ttsModel !== INHERIT ? ttsModel : (defaultTtsModel ?? models.find((m) => m.isDefault)?.key);
  const selectedModel = models.find((m) => m.key === effectiveModelKey);
  const supportsVoice = selectedModel?.supportsVoiceSelection ?? false;

  const grouped = useMemo(() => {
    const byLang = new Map<string, TtsModelV1['voices']>();
    for (const v of selectedModel?.voices ?? []) {
      if (!byLang.has(v.language)) byLang.set(v.language, []);
      byLang.get(v.language)!.push(v);
    }
    return Array.from(byLang.entries()).sort(([a], [b]) => a.localeCompare(b));
  }, [selectedModel]);

  const handleModelChange = (value: string) => {
    setTtsModel(value);
    setVoice(INHERIT);
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!name.trim() || !text.trim()) {
      toast.error('Name and text are required');
      return;
    }
    setSubmitting(true);
    try {
      const created = await recordings.generate({
        text: text.trim(),
        name: name.trim(),
        collectionId,
        ttsModel: ttsModel === INHERIT ? undefined : ttsModel,
        voice: voice === INHERIT ? undefined : voice,
        language: language.trim() || undefined,
      });
      toast.success(`Queued “${created.name}”`);
      onCreated(created);
      onOpenChange(false);
    } catch (err) {
      toast.error(err instanceof Error ? err.message : 'Submit failed');
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="sm:max-w-xl">
        <form onSubmit={handleSubmit}>
          <DialogHeader>
            <DialogTitle>New recording</DialogTitle>
            <DialogDescription>
              Submitted text goes through gpt-oss (channel tone applied) then the selected TTS model. Status updates by polling once submitted.
            </DialogDescription>
          </DialogHeader>

          <div className="grid gap-4 py-4">
            <div className="space-y-2">
              <Label htmlFor="name">Title</Label>
              <Input id="name" value={name} onChange={(e) => setName(e.target.value)} placeholder="Episode 17 — what I learned this week" autoFocus />
            </div>

            <div className="space-y-2">
              <Label htmlFor="text">Text</Label>
              <Textarea id="text" value={text} onChange={(e) => setText(e.target.value)} rows={10} placeholder="Paste the script, post, or notes here. Markdown is fine — the preprocessor strips it." />
              <p className="text-xs text-muted-foreground">{text.length.toLocaleString()} characters</p>
            </div>

            <div className="grid grid-cols-2 gap-3">
              <div className="space-y-2">
                <Label htmlFor="model">Model</Label>
                <Select value={ttsModel} onValueChange={handleModelChange}>
                  <SelectTrigger id="model">
                    <SelectValue placeholder={modelsLoading ? 'Loading…' : 'Pick a model'} />
                  </SelectTrigger>
                  <SelectContent>
                    <SelectItem value={INHERIT}>
                      {defaultTtsModel ? `Inherit channel default (${defaultTtsModel})` : 'Inherit channel / system default'}
                    </SelectItem>
                    {models.map((m) => (
                      <SelectItem key={m.key} value={m.key}>{m.displayName}</SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>
              <div className="space-y-2">
                <Label htmlFor="language">Language</Label>
                <Input id="language" value={language} onChange={(e) => setLanguage(e.target.value)} placeholder="en-US" />
              </div>
            </div>

            {supportsVoice && (
              <div className="space-y-2">
                <Label htmlFor="voice">Voice</Label>
                <Select value={voice} onValueChange={setVoice}>
                  <SelectTrigger id="voice">
                    <SelectValue placeholder="Pick a voice" />
                  </SelectTrigger>
                  <SelectContent>
                    <SelectItem value={INHERIT}>
                      {defaultVoice ? `Inherit channel default (${defaultVoice})` : 'Inherit channel / model default'}
                    </SelectItem>
                    {grouped.map(([lang, items]) => (
                      <SelectGroup key={lang}>
                        <SelectLabel>{lang}</SelectLabel>
                        {items.map((v) => (
                          <SelectItem key={v.id} value={v.id}>
                            {v.name}{v.emotion ? ` · ${v.emotion}` : ''}
                          </SelectItem>
                        ))}
                      </SelectGroup>
                    ))}
                  </SelectContent>
                </Select>
              </div>
            )}
          </div>

          <DialogFooter>
            <Button type="button" variant="outline" onClick={() => onOpenChange(false)} disabled={submitting}>Cancel</Button>
            <Button type="submit" disabled={submitting}>
              {submitting ? 'Submitting…' : 'Create recording'}
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  );
}
