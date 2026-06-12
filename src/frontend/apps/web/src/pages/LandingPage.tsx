import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import {
  ArrowRight,
  BookOpen,
  Boxes,
  Check,
  Copy,
  Github,
  KeyRound,
  Menu,
  Mic,
  Podcast,
  RefreshCw,
  Rss,
  Sparkles,
  Terminal,
  Waves,
} from 'lucide-react';
import { Button } from '@/components/ui/button';
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu';
import { ThemeToggle } from '@/components/layout/ThemeToggle';
import { useAuthStore } from '@/store/auth';

const REPO = 'https://github.com/andyjmorgan/DonkeyWork-Recordings';
const DOCS = 'https://github.com/andyjmorgan/DonkeyWork-Recordings/tree/main/docs';
const MCP_ENDPOINT = 'https://recordings.donkeywork.dev/mcp';

function CopyChip({ value }: { value: string }) {
  const [copied, setCopied] = useState(false);
  const copy = () => {
    navigator.clipboard.writeText(value);
    setCopied(true);
    setTimeout(() => setCopied(false), 2000);
  };
  return (
    <Button
      variant="ghost"
      size="icon"
      onClick={copy}
      aria-label="Copy MCP endpoint"
      title={copied ? 'Copied' : 'Copy'}
    >
      {copied ? <Check className="size-4 text-success" /> : <Copy className="size-4" />}
    </Button>
  );
}

const features = [
  {
    icon: Terminal,
    title: 'MCP-first, agent-native',
    body: 'A first-class MCP server at POST /mcp. Your LLM agent creates channels, posts recordings, polls status and re-records — no browser, no clicks. REST is there too when you want it.',
  },
  {
    icon: Mic,
    title: 'Kokoro TTS, many voices',
    body: 'Natural speech synthesised with Kokoro TTS. Pick from a roster of graded voices, set a default per channel, or override per recording.',
  },
  {
    icon: Sparkles,
    title: 'You bring the paragraphs',
    body: 'The caller supplies text already split into spoken paragraphs — there is no server-side LLM rewriting. What you send is what gets read, with a natural pause between each paragraph.',
  },
  {
    icon: RefreshCw,
    title: 'Edit & re-record in place',
    body: 'Fix a transcript and re-synthesise against the same recording. The mp3 is overwritten at the same object key, so the feed URL never changes and subscribers just get the new audio.',
  },
  {
    icon: Rss,
    title: 'Per-user RSS + master feed',
    body: 'Every user gets podcast feeds with full iTunes / Apple Podcasts metadata — a master feed across everything plus one feed per channel.',
  },
  {
    icon: KeyRound,
    title: 'Scoped API keys',
    body: 'Mint X-Api-Key keys scoped to REST + MCP, MCP-only or REST-only so agents authenticate headlessly. Backed by Keycloak JWT or API key via MultiAuth.',
  },
];

const pipeline = [
  { label: 'Paragraphs', sub: 'caller-supplied' },
  { label: 'Kokoro TTS', sub: 'per chunk' },
  { label: 'ffmpeg', sub: 'stitch to mp3' },
  { label: 'SeaweedFS', sub: 'S3 object store' },
  { label: 'RSS feed', sub: 'stable URL' },
];

export function LandingPage() {
  const navigate = useNavigate();
  const authed = useAuthStore((s) => s.isAuthenticated);
  const enter = () => navigate(authed ? '/channels' : '/login');
  const enterLabel = authed ? 'Open app' : 'Log in';

  return (
    <div className="min-h-screen bg-background text-foreground">
      {/* top bar */}
      <header className="sticky top-0 z-30 border-b border-border/60 bg-background/80 backdrop-blur">
        <div className="mx-auto flex max-w-6xl items-center justify-between px-4 py-3 sm:px-6">
          <div className="flex items-center gap-2 font-semibold">
            <img src="/donkeywork.png" alt="DonkeyWork" className="h-8 w-8" />
            <span>
              DonkeyWork <span className="text-accent">Recordings</span>
            </span>
          </div>
          <div className="flex items-center gap-1 sm:gap-2">
            <div className="hidden items-center gap-1 sm:flex">
              <Button variant="ghost" asChild>
                <a href={DOCS} target="_blank" rel="noreferrer">
                  <BookOpen className="size-4" /> Docs
                </a>
              </Button>
              <Button variant="ghost" size="icon" asChild aria-label="GitHub">
                <a href={REPO} target="_blank" rel="noreferrer">
                  <Github />
                </a>
              </Button>
            </div>
            <ThemeToggle />
            <Button onClick={enter} className="hidden sm:inline-flex">
              {enterLabel}
            </Button>
            <DropdownMenu>
              <DropdownMenuTrigger asChild className="sm:hidden">
                <Button variant="ghost" size="icon" aria-label="Menu">
                  <Menu />
                </Button>
              </DropdownMenuTrigger>
              <DropdownMenuContent align="end">
                <DropdownMenuItem onSelect={enter}>{enterLabel}</DropdownMenuItem>
                <DropdownMenuItem asChild>
                  <a href={DOCS} target="_blank" rel="noreferrer">
                    <BookOpen className="size-4" /> Docs
                  </a>
                </DropdownMenuItem>
                <DropdownMenuItem asChild>
                  <a href={REPO} target="_blank" rel="noreferrer">
                    <Github className="size-4" /> GitHub
                  </a>
                </DropdownMenuItem>
              </DropdownMenuContent>
            </DropdownMenu>
          </div>
        </div>
      </header>

      {/* hero */}
      <section className="mx-auto max-w-4xl px-4 pb-10 pt-12 text-center sm:px-6 sm:pt-20">
        <div className="mx-auto mb-5 inline-flex items-center gap-2 rounded-full border border-border bg-card px-3 py-1 text-xs text-muted-foreground">
          <Waves className="size-3.5 text-accent" /> Self-hosted text-to-podcast for agents
        </div>
        <h1 className="text-4xl font-semibold tracking-tight sm:text-6xl">
          Turn text into a podcast
          <br />
          <span className="bg-gradient-to-r from-accent to-primary bg-clip-text text-transparent">
            your agent can publish
          </span>
        </h1>
        <p className="mx-auto mt-5 max-w-2xl text-balance text-base text-muted-foreground sm:text-lg">
          Post paragraphs over MCP or REST; DonkeyWork Recordings synthesises them with Kokoro TTS,
          stitches an mp3, stores it, and publishes per-user RSS feeds your podcast app subscribes to.
          The caller brings the words — there is no server-side rewriting.
        </p>

        {/* MCP endpoint — front and center, copyable like an install chip */}
        <div className="mx-auto mt-8 flex max-w-2xl items-center gap-2 rounded-2xl border border-border bg-card p-2 pl-4 text-left shadow-sm">
          <span className="select-none font-mono text-accent">MCP</span>
          <code className="min-w-0 flex-1 truncate font-mono text-sm">{MCP_ENDPOINT}</code>
          <CopyChip value={MCP_ENDPOINT} />
        </div>
        <p className="mt-2 text-xs text-muted-foreground">
          Point your agent here and authenticate with a scoped{' '}
          <code className="text-accent">X-Api-Key</code>. No browser login required.
        </p>

        <div className="mt-8 flex flex-wrap items-center justify-center gap-3">
          <Button size="lg" onClick={enter}>
            {authed ? 'Open the app' : 'Log in'} <ArrowRight className="size-4" />
          </Button>
          <Button size="lg" variant="outline" asChild>
            <a href={DOCS} target="_blank" rel="noreferrer">
              <BookOpen className="size-4" /> Read the docs
            </a>
          </Button>
          <Button size="lg" variant="outline" asChild>
            <a href={REPO} target="_blank" rel="noreferrer">
              <Github className="size-4" /> View on GitHub
            </a>
          </Button>
        </div>
      </section>

      {/* pipeline strip */}
      <section className="mx-auto max-w-5xl px-4 pb-16 sm:px-6">
        <div className="rounded-2xl border border-border bg-card p-5 shadow-sm">
          <div className="mb-4 text-center text-sm font-medium text-muted-foreground">
            The pipeline, end to end
          </div>
          <div className="flex flex-wrap items-center justify-center gap-2 sm:gap-3">
            {pipeline.map((step, i) => (
              <div key={step.label} className="flex items-center gap-2 sm:gap-3">
                <div className="rounded-xl border border-border bg-background px-3 py-2 text-center">
                  <div className="text-sm font-medium">{step.label}</div>
                  <div className="text-[11px] text-muted-foreground">{step.sub}</div>
                </div>
                {i < pipeline.length - 1 && (
                  <ArrowRight className="size-4 shrink-0 text-muted-foreground/60" />
                )}
              </div>
            ))}
          </div>
        </div>
      </section>

      {/* features */}
      <section className="mx-auto max-w-6xl px-4 pb-20 sm:px-6">
        <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
          {features.map((f) => (
            <div
              key={f.title}
              className="rounded-2xl border border-border bg-card p-5 shadow-sm transition-all duration-200 hover:border-accent/50"
            >
              <div className="flex size-10 items-center justify-center rounded-xl bg-accent/10 text-accent">
                <f.icon className="size-5" />
              </div>
              <h3 className="mt-4 font-medium">{f.title}</h3>
              <p className="mt-1.5 text-sm text-muted-foreground">{f.body}</p>
            </div>
          ))}
        </div>
      </section>

      {/* subscribe / Apple Podcasts one-tap — headline feature */}
      <section className="mx-auto max-w-6xl px-4 pb-20 sm:px-6">
        <div className="grid items-center gap-6 rounded-2xl border border-border bg-card p-6 shadow-sm sm:p-8 lg:grid-cols-2">
          <div>
            <div className="inline-flex size-11 items-center justify-center rounded-xl bg-[#9933CC]/10">
              <Podcast className="size-6 text-[#9933CC]" />
            </div>
            <h2 className="mt-4 text-2xl font-semibold tracking-tight sm:text-3xl">
              Add it to your phone in one tap
            </h2>
            <p className="mt-3 text-sm text-muted-foreground sm:text-base">
              Every channel and your master feed expose a one-tap Apple Podcasts link. The{' '}
              <code className="text-accent">podcast://</code> deep-link opens your feed straight into
              Apple Podcasts on iOS — no copy-pasting a feed URL into an "add by URL" box. Drop any feed
              into Overcast, Pocket Casts or anything that speaks RSS just the same.
            </p>
            <div className="mt-5 flex flex-wrap gap-3">
              <Button onClick={enter}>
                <Rss className="size-4" /> Get your feed
              </Button>
              <Button variant="outline" asChild>
                <a
                  href={`${DOCS}/subscribe.md`}
                  target="_blank"
                  rel="noreferrer"
                >
                  How subscribing works
                </a>
              </Button>
            </div>
          </div>
          <div className="rounded-xl border border-border bg-background p-5">
            <div className="flex items-center gap-2 text-xs font-medium uppercase tracking-wide text-muted-foreground">
              <Boxes className="size-4" /> Channels hold ordered recordings
            </div>
            <ul className="mt-4 space-y-3 text-sm">
              {[
                'Group recordings into channels — each is its own podcast feed.',
                'Master feed aggregates every recording across all your channels.',
                'iTunes metadata: title, author, category, cover art, explicit flag.',
                'Re-record a transcript and subscribers get the fresh audio at the same URL.',
              ].map((line) => (
                <li key={line} className="flex gap-2 text-muted-foreground">
                  <Check className="mt-0.5 size-4 shrink-0 text-accent" />
                  <span>{line}</span>
                </li>
              ))}
            </ul>
          </div>
        </div>
      </section>

      <footer className="border-t border-border">
        <div className="mx-auto flex max-w-6xl flex-col items-center justify-between gap-2 px-4 py-6 text-xs text-muted-foreground sm:flex-row sm:px-6">
          <div className="text-center sm:text-left">
            <div>DonkeyWork Recordings — self-hosted text-to-podcast for agents.</div>
            <div className="mt-1 text-[11px] text-muted-foreground/75">
              .NET 10 · Kokoro TTS · React 19. Fueled by caffeine and token-spending addictions.
            </div>
          </div>
          <div className="flex items-center gap-4">
            <a
              href={DOCS}
              target="_blank"
              rel="noreferrer"
              className="inline-flex items-center gap-1 hover:text-foreground"
            >
              <BookOpen className="size-3.5" /> Documentation
            </a>
            <a
              href={REPO}
              target="_blank"
              rel="noreferrer"
              className="inline-flex items-center gap-1 hover:text-foreground"
            >
              <Github className="size-3.5" /> andyjmorgan/DonkeyWork-Recordings
            </a>
          </div>
        </div>
      </footer>
    </div>
  );
}
