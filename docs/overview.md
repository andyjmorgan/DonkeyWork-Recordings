# Overview

DonkeyWork Recordings turns **text you already have** into a **podcast you can subscribe to**. It is
built for automation: the primary client is an LLM agent talking to the **MCP server**, though a
plain REST API is available for everything too.

## Who it's for

- **Agent builders** who want their agent to produce listenable audio — a daily briefing, a digest of
  long documents, release notes read aloud — and have it land in a real podcast app.
- **Self-hosters** who'd rather run their own text-to-speech podcast pipeline than depend on a SaaS.

## The model: bring your own paragraphs

There is **no server-side LLM preprocessing**. The caller is responsible for splitting the source
text into natural, breath-paused **paragraphs** (typically 1–4 sentences each) and stripping anything
that wouldn't sound right read aloud — URLs, markdown, code blocks, tables. What you send is exactly
what gets read, with a short pause between each paragraph.

This keeps the service simple and predictable: it synthesises and publishes; your agent does the
editorial work.

## Core concepts

### Channels (collections)

A **channel** is a named container that doubles as a podcast feed. It holds an ordered list of
recordings and carries a **default voice** and **default language** that new recordings inherit. In
the API and MCP tools a channel is called a *collection*.

### Recordings

A **recording** is one synthesised episode. You create it by posting an ordered array of paragraphs
plus a name and the channel it belongs to. Synthesis runs in the background, so a recording starts in
`Pending`, moves through `Generating` (with live progress), and ends at `Ready` (or `Failed`). When
`Ready`, it has a public mp3 URL, a duration, and a stored transcript.

You can **edit a transcript and re-record in place** — the mp3 is overwritten at the same storage key,
so the feed URL never changes and subscribers simply receive the new audio.

### Voices

Speech is synthesised with **Kokoro TTS**. Multiple voices are available, each with a quality grade;
the default voice is **Heart** (`af_heart`). Set a default per channel or override per recording.

### Feeds

Every user gets RSS feeds with full iTunes / Apple Podcasts metadata:

- a **master feed** aggregating every recording across all your channels, and
- a **per-channel feed**, one for each channel.

See [Subscribe in a podcast app](./subscribe.md) for how to add a feed — including the **one-tap
Apple Podcasts** deep-link.

### Authentication

Requests are authenticated with either a **Keycloak JWT** (browser login) or a **user API key**
(`X-Api-Key`). The dual scheme — *MultiAuth* — means an agent can use a key while a person uses the
web app. API keys are **scoped** so you can hand an agent exactly the surface it needs. See the
[MCP guide](./mcp.md) and [REST quickstart](./rest-api.md).
