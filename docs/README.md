# DonkeyWork Recordings — Documentation

DonkeyWork Recordings is a self-hosted service and **MCP server** that turns posted text into
podcast-style audio. A client — typically an LLM agent — supplies text already split into spoken
paragraphs; the server synthesises speech with **Kokoro TTS**, stitches the chunks into an mp3 with
ffmpeg, stores it in SeaweedFS (S3), and publishes **per-user RSS feeds** that any podcast app can
subscribe to.

The hosted instance lives at **https://recordings.donkeywork.dev** with the MCP endpoint at
**https://recordings.donkeywork.dev/mcp**.

## Documentation map

| Page | What it covers |
|------|----------------|
| [Overview](./overview.md) | What the project is, who it's for, the core concepts (channels, recordings, feeds). |
| [How it works](./how-it-works.md) | The synthesis pipeline end to end and how Kokoro TTS fits in. |
| [MCP guide](./mcp.md) | The MCP endpoint, authenticating with an API key, the full tool list, and a worked example. |
| [REST & API keys quickstart](./rest-api.md) | The REST surface, minting scoped API keys, and curl examples. |
| [Subscribe in a podcast app](./subscribe.md) | RSS feeds, the master feed, and one-tap Apple Podcasts. |

## Core concepts in one breath

- **Channel** (a.k.a. *collection*) — a named container that is its own podcast feed and holds an
  ordered list of recordings. A channel carries a default voice and language.
- **Recording** — one synthesised audio episode inside a channel.
- **Feed** — the RSS your podcast app subscribes to. There's one per channel plus a **master feed**
  aggregating every recording across all your channels.
- **API key** — an `X-Api-Key` secret, scoped to REST + MCP / MCP-only / REST-only, that lets agents
  authenticate without a browser login.

## Tech at a glance

- **Backend:** .NET 10, EF Core (Postgres).
- **TTS:** Kokoro.
- **Storage:** SeaweedFS (S3-compatible), public-read bucket served at `s3.donkeywork.dev`.
- **MCP:** `ModelContextProtocol.AspNetCore`, mapped at `POST /mcp`.
- **Auth:** Keycloak JWT **or** API key (MultiAuth).
- **Frontend:** React 19 + Vite + Tailwind SPA.
