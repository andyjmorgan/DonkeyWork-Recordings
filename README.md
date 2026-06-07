# DonkeyWork-Recordings

Site + MCP server that turns posted text into podcast-style audio recordings
(caller-supplied paragraphs → Kokoro TTS synthesis → ffmpeg stitch → SeaweedFS). Publishes per-user
RSS feeds for podcast apps. Modular monolith mirroring `DonkeyWork-Agents`.

The caller (an MCP/REST client — typically an LLM) supplies the text already split into spoken
paragraphs; there is no server-side LLM preprocessing.

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

In-memory `Channel<T>` + `BackgroundService` for now (drop-in swap for Wolverine+NATS once we've
proven the pipeline end-to-end against real Kokoro/SeaweedFS).

## Stack

| | |
|---|---|
| Backend | .NET 10, EF Core (Postgres), Kokoro TTS, SeaweedFS (S3) |
| MCP | `ModelContextProtocol.AspNetCore` at `POST /` (auth-gated via MultiAuth) |
| Auth | Keycloak (existing `Agents` realm, audience `donkeywork-recordings-api`) |
| Frontend | React 19, Vite 7, Tailwind 3, Zustand, react-router-dom v7 — theme + auth lifted from `DonkeyWork-Agents` |
| CI | GitHub Actions on self-hosted runners; images pushed to Nexus on main + semver tagged |
| Infra | Provisioned out-of-band by the k3s-agentling (office cluster for the API + web, attic SeaweedFS for the public-read `recordings` bucket) |

## Layout

```
DonkeyWork.Recordings.slnx
src/
  DonkeyWork.Recordings.Api/                       # ASP.NET Core host
  common/DonkeyWork.Recordings.Persistence/        # EF Core, RecordingsDbContext, migrations
  identity/DonkeyWork.Recordings.Identity.{Contracts,Core,Api}/
  storage/DonkeyWork.Recordings.Storage.{Contracts,Core,Api}/
  audio/DonkeyWork.Recordings.Audio.{Contracts,Core,Api}/   # feature core (named Audio to avoid host name collision)
  mcp/DonkeyWork.Recordings.Mcp.{Contracts,Core,Api}/
  frontend/                                        # pnpm workspace
    apps/web/                                      # Vite SPA
test/
  audio/DonkeyWork.Recordings.Audio.Core.Tests/    # unit
  integration/DonkeyWork.Recordings.Integration.Tests/  # WebApplicationFactory + Testcontainers Postgres
  smoke/DonkeyWork.Recordings.Smoke.Tests/         # opt-in: Category=LiveSmoke against real Kokoro
  e2e/DonkeyWork.Recordings.E2E.Tests/             # opt-in: Category=E2E (Playwright placeholder)
docs/research/                                     # design notes + agentling answers
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

Self-hosted runners (`[self-hosted, Linux, X64]`) provisioned by the k3s-agentling.

## Design docs

- `docs/research/architecture-reference.md` — parent (`DonkeyWork-Agents`) patterns we mirror.
- `docs/research/proposed-design.md` — current spec (locked decisions + open infra items).
- `docs/research/agentling-and-spark-tts.md` — Magpie + spark infra facts.
- `docs/research/tts-pipeline.md` — extraction reference + provider swap point.
- `docs/implementation-plan.md` — phased plan + a 2026-05-28 revisions log noting where the
  earlier text was superseded.
