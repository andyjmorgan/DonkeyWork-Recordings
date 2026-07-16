# DonkeyWork Recordings

**Turn text you already have into a podcast you can subscribe to.**

DonkeyWork Recordings is a self-hosted service and **MCP server** that synthesises posted text into
podcast-style audio — daily briefings, document digests, release notes read aloud — and publishes it
as standard RSS feeds that any podcast app can subscribe to. You supply the words; it produces the
audio and the feed.

Hosted at **https://recordings.donkeywork.dev** (MCP at `…/mcp`).

## Why use this

- **Built for agents.** The primary client is an LLM agent talking to a first-class MCP server. Your
  agent can create channels, post recordings, watch them synthesise, and re-record — with no browser
  involved. A plain REST API mirrors every MCP tool for scripts and cron jobs.
- **Predictable, not magic.** There is no server-side LLM preprocessing. What you send is exactly
  what gets read, with a natural pause between paragraphs. The service synthesises and publishes; your
  agent does the editorial work.
- **It lands in a real podcast app.** Recordings publish as RSS feeds with full iTunes / Apple
  Podcasts metadata, so they show up in Apple Podcasts, Overcast, Pocket Casts, AntennaPod — anything
  that speaks RSS.
- **Self-hosted.** Run your own text-to-speech podcast pipeline instead of depending on a SaaS.

## Feature highlights

- **Natural-sounding voices.** Speech is synthesised with **Kokoro TTS** across multiple graded
  voices and BCP-47 languages. The default voice is **Heart** (`af_heart`); set a default per channel
  or override it per recording.
- **Channels that are podcast feeds.** A **channel** (called a *collection* in the API) is a named
  container that doubles as a podcast feed and holds an ordered list of recordings. It carries a
  default voice and language that new recordings inherit, plus its own **cover art** — override it per
  channel or fall back to the default.
- **Per-user RSS feeds, plus a master feed.** Every user gets one feed per channel and a master feed
  aggregating every recording across all channels, each with full iTunes metadata and cover image.
- **One-tap Apple Podcasts.** Channel and Feed Settings pages expose a `podcast://` deep link that
  opens Apple Podcasts straight to your feed and subscribes — no copy-paste.
- **Transcripts included.** Every recording carries a stored transcript, served as both plain text
  and **WebVTT**, and referenced from the feed so podcast apps can show captions.
- **Edit and re-record in place.** Fix a transcript and re-synthesise against the same recording id —
  the mp3 is overwritten at the same storage key, so the feed URL never changes and subscribers simply
  get the updated audio on the next refresh.
- **Headless or interactive auth.** The MCP endpoint is an OAuth 2.1 protected resource for
  interactive clients (Claude Desktop, Claude Code) *and* accepts **scoped API keys** for cron jobs
  and scripts. Either way, tools run as the same user.
- **OpenAI-compatible TTS endpoint.** Any OpenAI speech client works unmodified against
  `/openai/v1` (`GET /models`, `POST /audio/speech`) with a DonkeyWork API key as the bearer token —
  one model (`kokoro`), all six OpenAI response formats, and OpenAI voice names mapped to Kokoro
  voices. See the [REST guide](./docs/rest-api.md#openai-compatible-endpoint).

## Quick start

### Use it from an agent (MCP)

Point a compliant MCP client at the endpoint — for interactive clients that is all you need; the
client discovers Keycloak and runs the browser auth flow on first use:

```json
{
  "mcpServers": {
    "donkeywork-recordings": {
      "url": "https://recordings.donkeywork.dev/mcp"
    }
  }
}
```

For headless clients, create a scoped API key in the web app (**Profile → API Keys → New key**) and
send it as an `X-Api-Key` header. Then a typical agent flow is: `create_audio_collection` → split
your text into spoken paragraphs → `create_audio_recording` → poll `get_audio_recording` until
`Ready`. See the [MCP guide](./docs/mcp.md) for the full tool list and a worked example.

### Use it from a script (REST)

Everything the MCP server can do is available under `/api/v1`. Authenticate with an `X-Api-Key`:

```bash
export DWR=https://recordings.donkeywork.dev
export KEY=dk_your_key_here

# Create a channel
COLLECTION_ID=$(curl -s -X POST "$DWR/api/v1/collections" \
  -H "X-Api-Key: $KEY" -H "Content-Type: application/json" \
  -d '{"name":"Morning Briefing","defaultVoice":"af_heart","defaultLanguage":"en-US"}' \
  | jq -r '.id')

# Post a recording (paragraphs you split yourself)
RECORDING_ID=$(curl -s -X POST "$DWR/api/v1/recordings/generate" \
  -H "X-Api-Key: $KEY" -H "Content-Type: application/json" \
  -d "{\"collectionId\":\"$COLLECTION_ID\",\"name\":\"Briefing — 12 June\",
       \"paragraphs\":[\"Good morning. Here is your briefing.\",\"First up: the markets opened higher.\"]}" \
  | jq -r '.id')

# Poll until Ready, then filePath is the public mp3 URL
curl -s "$DWR/api/v1/recordings/$RECORDING_ID" -H "X-Api-Key: $KEY" \
  | jq '{status, progress, filePath, durationSeconds}'
```

See the [REST & API keys quickstart](./docs/rest-api.md) for the full surface.

### Subscribe in a podcast app

Copy a feed URL from the channel or Feed Settings page and add it to your podcast app, or tap the
**Apple Podcasts** button for one-tap subscribe. See [Subscribe in a podcast app](./docs/subscribe.md).

## Documentation

User-facing docs live in [`docs/`](./docs/): [overview](./docs/overview.md),
[how it works](./docs/how-it-works.md), the [MCP guide](./docs/mcp.md),
[REST & API keys](./docs/rest-api.md), and [subscribing in a podcast app](./docs/subscribe.md).

## How it works

The caller supplies the text already split into spoken paragraphs; synthesis runs asynchronously on a
background worker. A recording starts `Pending`, moves through `Generating` (with live progress), and
ends at `Ready` (with a public mp3 URL, duration, and transcript) or `Failed` — poll the recording
until it settles.

```
HTTP/MCP create (paragraphs[]) → insert Pending row + enqueue → background worker
  → SsmlPreprocessor (defensive: strip stray [PAUSE=…]/[EMPHASIS=…] tokens)
  → TtsChunker → KokoroTtsProvider per chunk
  → AudioConverter.ConcatWav → WavToMp3 → ffprobe duration
  → IStorageService.UploadAsync (x-amz-meta-* tagged)
  → https://s3.donkeywork.dev/recordings/{userId}/{recordingId}.mp3
  → recording row Status=Ready
```

The queue is an in-memory `Channel<T>` drained by a hosted `BackgroundService` for now — designed to
drop in a durable message queue later once the pipeline is proven end-to-end against real
Kokoro/SeaweedFS.

**Re-recording.** Editing a recording's transcript re-runs the same pipeline against the existing
recording id (`POST /api/v1/recordings/{id}/regenerate`, or the `regenerate_audio_recording` MCP
tool), so the mp3 is overwritten in place at the same `{userId}/{recordingId}.mp3` object key — the
feed URL never changes. Voice, language, channel and metadata are preserved; only the audio and
transcript change. The web edit dialog renders the whole transcript and splits it back into paragraphs
on blank lines before re-recording.

## Architecture & stack

This is a modular monolith mirroring `DonkeyWork-Agents`.

| | |
|---|---|
| Backend | .NET 10, EF Core (Postgres), Kokoro TTS, SeaweedFS (S3) |
| MCP | `ModelContextProtocol.AspNetCore` at `POST /mcp` (publicly `https://recordings.donkeywork.dev/mcp`, auth-gated via MultiAuth) |
| Auth | Keycloak (existing `Agents` realm, audience `donkeywork-recordings-api`); MultiAuth accepts a Keycloak JWT **or** a user API key, so programmatic/MCP clients can use a key. The MCP endpoint is also an OAuth 2.1 protected resource for interactive clients |
| Frontend | React 19, Vite 7, Tailwind 3, Zustand, react-router-dom v7 — theme + auth lifted from `DonkeyWork-Agents` |
| CI | GitHub Actions on self-hosted runners; images pushed to Nexus on main + semver tagged |
| Infra | Provisioned out-of-band by the k3s-agentling: office cluster namespace `donkeywork-recordings` (API + web behind ingress `recordings.donkeywork.dev`), attic SeaweedFS for the public-read `recordings` bucket served at `s3.donkeywork.dev` |

### Layout

```
DonkeyWork.Recordings.slnx
src/
  DonkeyWork.Recordings.Api/                       # ASP.NET Core host
  common/DonkeyWork.Recordings.Persistence/        # EF Core, RecordingsDbContext, migrations
  identity/DonkeyWork.Recordings.Identity.{Contracts,Core,Api}/
  storage/DonkeyWork.Recordings.Storage.{Contracts,Core,Api}/
  audio/DonkeyWork.Recordings.Audio.{Contracts,Core,Api}/   # feature core (named Audio to avoid host name collision); MCP tools live in Audio.Api/McpTools/AudioTools.cs
  mcp/DonkeyWork.Recordings.Mcp.{Contracts,Core,Api}/       # hosting glue only — Mcp.Api maps POST /mcp; the tools themselves are in the Audio feature module above
  frontend/                                        # pnpm workspace
    apps/web/                                      # Vite SPA
test/
  audio/DonkeyWork.Recordings.Audio.Core.Tests/    # unit
  integration/DonkeyWork.Recordings.Integration.Tests/  # WebApplicationFactory + Testcontainers Postgres
  smoke/DonkeyWork.Recordings.Smoke.Tests/         # opt-in: Category=LiveSmoke against real Kokoro
  e2e/DonkeyWork.Recordings.E2E.Tests/             # opt-in .NET skeleton (Category=E2E); the live browser e2e is the Playwright suite in src/frontend/apps/web/e2e/
docker-compose.dev.yml                             # Postgres for local dev
Dockerfile                                         # full multi-stage backend build (for local)
Dockerfile.runtime                                 # runtime-only backend (CI publishes ./publish, image consumes it)
src/frontend/Dockerfile                            # node 22 pnpm build → nginx alpine
_nuget.config                                      # Nexus proxy template — `cp _nuget.config nuget.config` before restore
```

## Local dev

```bash
# 1. Postgres
docker compose -f docker-compose.dev.yml up -d

# 2. Port-forward Kokoro TTS from the office cluster
kubectl --context=office port-forward -n kokoro-tts svc/kokoro-tts 8000:8000 &

# 3. Run migrations + start the backend
dotnet ef database update \
  --project src/common/DonkeyWork.Recordings.Persistence \
  --startup-project src/DonkeyWork.Recordings.Api
dotnet run --project src/DonkeyWork.Recordings.Api

# 4. Start the SPA
cd src/frontend && pnpm install && pnpm dev
# → http://localhost:5199
```

`src/DonkeyWork.Recordings.Api/appsettings.Development.json` is gitignored — drop a local copy with
Postgres connection string, attic S3 creds (admin key from the agentling for dev), and Keycloak
config. See the parent `appsettings.json` for the shape.

## CI

- **PR → `main`**: `pr-build-test.yml` — full backend + frontend build + CI-tier tests.
  No images pushed.
- **Push to `main`**: `docker-build.yml` — same build + test, then pushes
  `{registry}/donkeywork-recordings/api:{tag}` and `…/web:{tag}` to Nexus,
  and creates a `v{semver}` git tag for the release.
- **PR touching `src/frontend/**`** (or `workflow_dispatch`): `e2e.yml` — runs the
  Playwright browser suite (`src/frontend/apps/web/e2e/`, mobile-layout checks) against a real
  Keycloak login on the `Agents` realm.

Self-hosted runners (`[self-hosted, Linux, X64]`) provisioned by the k3s-agentling.

Builds are change-filtered (`dorny/paths-filter`): backend and frontend jobs only run when their
respective paths change.
