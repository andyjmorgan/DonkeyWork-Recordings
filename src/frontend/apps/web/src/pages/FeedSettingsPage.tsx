import { useEffect, useState } from 'react';
import { Rss, Copy, Check, Save, Podcast } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Textarea } from '@/components/ui/textarea';
import { Switch } from '@/components/ui/switch';
import { feedSettings, type FeedSettingsV1, type UpdateFeedSettingsRequest } from '@/lib/api';
import { toast } from 'sonner';

export function FeedSettingsPage() {
  const [loaded, setLoaded] = useState<FeedSettingsV1 | null>(null);
  const [draft, setDraft] = useState<UpdateFeedSettingsRequest>({});
  const [submitting, setSubmitting] = useState(false);
  const [copied, setCopied] = useState(false);

  useEffect(() => {
    feedSettings.get()
      .then((s) => { setLoaded(s); setDraft(s); })
      .catch((err) => toast.error(err instanceof Error ? err.message : 'Failed to load feed settings'));
  }, []);

  const handleSave = async (e: React.FormEvent) => {
    e.preventDefault();
    setSubmitting(true);
    try {
      const saved = await feedSettings.update(draft);
      setLoaded(saved);
      setDraft(saved);
      toast.success('Feed settings saved');
    } catch (err) {
      toast.error(err instanceof Error ? err.message : 'Save failed');
    } finally {
      setSubmitting(false);
    }
  };

  const copyFeed = () => {
    if (!loaded) return;
    navigator.clipboard.writeText(loaded.masterFeedUrl);
    setCopied(true);
    toast.success('Master feed URL copied');
    setTimeout(() => setCopied(false), 2000);
  };

  if (!loaded) return <div className="p-8 text-muted-foreground">Loading…</div>;

  return (
    <div className="p-4 sm:p-8 mx-auto max-w-2xl space-y-8">
      <header className="flex items-center gap-4">
        <div className="inline-flex h-12 w-12 items-center justify-center rounded-xl bg-primary/10 text-primary">
          <Rss className="h-6 w-6" />
        </div>
        <div>
          <h1 className="text-2xl font-semibold">Feed Settings</h1>
          <p className="text-sm text-muted-foreground">Master podcast feed metadata.</p>
        </div>
      </header>

      <section className="rounded-2xl border border-border bg-card p-6 space-y-3">
        <h2 className="text-sm font-semibold uppercase tracking-wide text-muted-foreground">Master Feed URL</h2>
        <div className="font-mono text-xs break-all p-3 rounded-lg border border-border bg-muted/50">
          {loaded.masterFeedUrl}
        </div>
        <div className="flex flex-wrap gap-2">
          <Button onClick={copyFeed} variant="outline" size="sm">
            {copied ? <><Check className="h-4 w-4 mr-2" />Copied</> : <><Copy className="h-4 w-4 mr-2" />Copy URL</>}
          </Button>
          <Button asChild variant="outline" size="sm">
            <a href={loaded.masterFeedUrl.replace(/^https?:\/\//, 'podcast://')}>
              <Podcast className="h-4 w-4 mr-2 text-[#9933CC]" />Apple Podcasts
            </a>
          </Button>
        </div>
      </section>

      <form onSubmit={handleSave} className="rounded-2xl border border-border bg-card p-6 space-y-5">
        <h2 className="text-sm font-semibold uppercase tracking-wide text-muted-foreground">Channel Metadata</h2>

        <div className="space-y-2">
          <Label htmlFor="title">Title</Label>
          <Input id="title" value={draft.title ?? ''} onChange={(e) => setDraft({ ...draft, title: e.target.value })} />
        </div>

        <div className="space-y-2">
          <Label htmlFor="description">Description</Label>
          <Textarea id="description" value={draft.description ?? ''} onChange={(e) => setDraft({ ...draft, description: e.target.value })} rows={3} />
        </div>

        <div className="grid grid-cols-2 gap-3">
          <div className="space-y-2">
            <Label htmlFor="author">Author</Label>
            <Input id="author" value={draft.author ?? ''} onChange={(e) => setDraft({ ...draft, author: e.target.value })} />
          </div>
          <div className="space-y-2">
            <Label htmlFor="authorEmail">Author email</Label>
            <Input id="authorEmail" type="email" value={draft.authorEmail ?? ''} onChange={(e) => setDraft({ ...draft, authorEmail: e.target.value })} />
          </div>
        </div>

        <div className="grid grid-cols-2 gap-3">
          <div className="space-y-2">
            <Label htmlFor="language">Language</Label>
            <Input id="language" value={draft.language ?? ''} onChange={(e) => setDraft({ ...draft, language: e.target.value })} placeholder="en-US" />
          </div>
          <div className="space-y-2">
            <Label htmlFor="category">iTunes category</Label>
            <Input id="category" value={draft.itunesCategory ?? ''} onChange={(e) => setDraft({ ...draft, itunesCategory: e.target.value })} placeholder="Technology" />
          </div>
        </div>

        <div className="space-y-2">
          <Label htmlFor="link">Homepage link</Label>
          <Input id="link" type="url" value={draft.link ?? ''} onChange={(e) => setDraft({ ...draft, link: e.target.value })} placeholder="https://…" />
        </div>

        <div className="space-y-2">
          <Label htmlFor="cover">Cover image URL</Label>
          <Input id="cover" type="url" value={draft.coverImagePath ?? ''} onChange={(e) => setDraft({ ...draft, coverImagePath: e.target.value })} placeholder="https://…" />
        </div>

        <div className="flex items-center justify-between rounded-lg border border-border p-3">
          <Label htmlFor="explicit" className="m-0">Explicit content</Label>
          <Switch id="explicit" checked={draft.isExplicit ?? false} onCheckedChange={(v) => setDraft({ ...draft, isExplicit: v })} />
        </div>

        <div className="flex justify-end">
          <Button type="submit" disabled={submitting}>
            <Save className="h-4 w-4 mr-2" />{submitting ? 'Saving…' : 'Save'}
          </Button>
        </div>
      </form>
    </div>
  );
}
