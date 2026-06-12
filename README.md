# DonkeyWork-Recordings

Self-hosted site + **MCP server** that turns posted text into podcast-style audio recordings
(caller-supplied paragraphs → Kokoro TTS synthesis → ffmpeg stitch → SeaweedFS). Publishes per-user
RSS feeds for podcast apps. Modular monolith mirroring `DonkeyWork-Agents`.

The caller (an MCP/REST client — typically an LLM) supplies the text already split into spoken
paragraphs; there is no server-side LLM preprocessing.

Hosted at **https://recordings.donkeywork.dev** (MCP at `…/mcp`).

## What it does

- **MCP-first** — an agent drives the whole thing over `POST /mcp`: create channels, post recordings,
  poll status, re-record. REST under `/api/v1` mirrors it.
- **Kokoro TTS** with multiple graded voices; default voice **Heart** (`af_heart`), overridable per
  channel or per recording.
- **Channels** (collections) hold ordered **recordings**; each channel is its own podcast feed.
- **Edit transcript & re-record in place** — the mp3 is overwritten at the same object key, so the
  feed URL never changes.
- **Per-user RSS feeds + a master feed** with full iTunes / Apple Podcasts metadata, including a
  one-tap `podcast://` Apple Podcasts link.
- **Scoped API keys** (`RestAndMcp` / `McpOnly` / `RestOnly`) so agents authenticate headlessly;
  Keycloak JWT or API key via MultiAuth.

## Documentation

User-facing docs live in [`docs/`](./docs/): [overview](./docs/overview.md),
[how it works](./docs/how-it-works.md), the [MCP guide](./docs/mcp.md),
[REST & API keys](./docs/rest-api.md), and [subscribing in a podcast app](./docs/subscribe.md).

## Pipeline

```
HTTP/MCP create (paragraphs[]) → insert Pending row + enqueue → background worker
  → SsmlPreprocessor (defensive: strip stray [PAUSE=…]/[EMPHASIS=…] tokens)
  → TtsChunker → KokoroTtsProvider per chunk
  → AudioConverter.ConcatWav → WavToMp3 → ffprobe duration
  → IStorageService.UploadAsync (x-amz-meta-* tagged)
  → https://s3.donkeywork.dev/recordings/{userId}/{recordingId}.mp3
  → recording row Status=Ready
```

In-memory `Channel<T>` + `BackgroundService` for now — designed to drop in a durable message queue
later once the pipeline is proven end-to-end against real Kokoro/SeaweedFS.

**Re-recording.** Editing a recording's transcript re-runs the same pipeline against the existing
recording id (`POST /api/v1/recordings/{id}/regenerate`, or the `regenerate_audio_recording` MCP
tool), so the mp3 is overwritten in place at the same `{userId}/{recordingId}.mp3` object key — the
feed URL never changes. Voice, language, channel and metadata are preserved; only the audio and
transcript change. The web edit dialog renders the whole transcript and splits it back into
paragraphs on blank lines before re-recording.

## Stack

| | |
|---|---|
| Backend | .NET 10, EF Core (Postgres), Kokoro TTS, SeaweedFS (S3) |
| MCP | `ModelContextProtocol.AspNetCore` at `POST /mcp` (publicly `https://recordings.donkeywork.dev/mcp`, auth-gated via MultiAuth) |
| Auth | Keycloak (existing `Agents` realm, audience `donkeywork-recordings-api`); MultiAuth accepts a Keycloak JWT **or** a user API key, so programmatic/MCP clients can use a key |
| Frontend | React 19, Vite 7, Tailwind 3, Zustand, react-router-dom v7 — theme + auth lifted from `DonkeyWork-Agents` |
| CI | GitHub Actions on self-hosted runners; images pushed to Nexus on main + semver tagged |
| Infra | Provisioned out-of-band by the k3s-agentling: office cluster namespace `donkeywork-recordings` (API + web behind ingress `recordings.donkeywork.dev`), attic SeaweedFS for the public-read `recordings` bucket served at `s3.donkeywork.dev` |

## Layout

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
