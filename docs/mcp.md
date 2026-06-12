# MCP guide

DonkeyWork Recordings ships a first-class **MCP (Model Context Protocol) server** so an agent can
create channels, post recordings, watch them synthesise, and re-record — entirely over MCP, with no
browser involved.

## Endpoint

```
POST https://recordings.donkeywork.dev/mcp
```

The MCP server is mounted at `POST /mcp` and is auth-gated. It supports two ways to authenticate —
**OAuth** for interactive clients and a **scoped API key** for headless ones.

## Authentication

### OAuth (interactive clients — recommended)

The endpoint is an OAuth 2.1 **protected resource**: it advertises
`/.well-known/oauth-protected-resource`, which points compliant MCP clients (Claude Desktop, Claude
Code, …) at the Keycloak authorization server. Just give the client the URL — nothing else:

```json
{
  "mcpServers": {
    "donkeywork-recordings": {
      "url": "https://recordings.donkeywork.dev/mcp"
    }
  }
}
```

On first use the client receives a `401` whose `WWW-Authenticate` header points at the resource
metadata, discovers Keycloak from it, and runs the browser authorization-code flow (scopes
`openid profile email offline_access recordings-audience`). It then calls `/mcp` with an
`Authorization: Bearer …` token, refreshed automatically via `offline_access` — no secret to copy or
store.

### Scoped API key (headless clients)

For cron jobs, scripts, or any non-interactive client, authenticate with a **scoped API key** sent as
an `X-Api-Key` header instead. Create a key from the web app (**Profile → API Keys → New key**) and
choose a scope that includes MCP:

- **REST + MCP** — full access (`RestAndMcp`).
- **MCP only** — the MCP endpoint only (`McpOnly`).

Keys are shown unmasked exactly once at creation — copy the secret then. The scope is enforced on
every request; a mis-scoped call returns `401`.

```json
{
  "mcpServers": {
    "donkeywork-recordings": {
      "url": "https://recordings.donkeywork.dev/mcp",
      "headers": { "X-Api-Key": "dk_your_key_here" }
    }
  }
}
```

Both methods resolve to the same user; tools run as that identity either way.

## Tools

All tools operate as the authenticated user — the OAuth subject, or the owner of the API key.

### Recordings

| Tool | Purpose |
|------|---------|
| `create_audio_recording` | Submit ordered spoken **paragraphs** to synthesise a recording in a channel. `collectionId` is **required**. Returns the recording immediately with `Status=Pending`; the id is the job id. Optional: `voice`, `language`, `sequenceNumber`, `chapterTitle`, `description`. |
| `get_audio_recording` | Get one recording by id. Returns `Status`, live `Progress` / `StatusDetail` while generating, `FilePath` (mp3 URL) when ready, `DurationSeconds`, transcript, etc. *(read-only)* |
| `list_audio_recordings` | List recordings. Scope to a channel with `collectionId`, or pass `unfiledOnly=true` for recordings in no channel; omit both for every recording, newest first. Supports `offset` / `limit`. *(read-only)* |
| `update_audio_recording` | Edit a recording's metadata (`name`, `description`, `chapterTitle`). Does **not** re-synthesise audio. |
| `regenerate_audio_recording` | Re-synthesise a recording from an **edited transcript** (ordered paragraphs), replacing the mp3 in place. Voice, language, channel and metadata are preserved. Returns `Status=Pending`. |
| `move_audio_recording` | Move a recording to a different channel (`collectionId` required). Optional `sequenceNumber`, `chapterTitle`. |
| `delete_audio_recording` | Permanently delete a recording and its mp3. |

### Channels (collections)

| Tool | Purpose |
|------|---------|
| `create_audio_collection` | Create a channel. Optional `description`, `defaultVoice`, `defaultLanguage`. |
| `get_audio_collection` | Get one channel by id. *(read-only)* |
| `list_audio_collections` | List your channels, with `offset` / `limit`. *(read-only)* |
| `update_audio_collection` | Patch a channel (`name`, `description`, `defaultVoice`, `defaultLanguage`). |
| `delete_audio_collection` | Delete a channel. Its recordings become **unfiled** (not deleted). |

### Voices

| Tool | Purpose |
|------|---------|
| `list_voices` | List the available Kokoro voices. Use a voice's `Id` as `voice` / `defaultVoice`. Default is **Heart** (`af_heart`). *(read-only)* |

## Worked example: a channel with one recording

A typical agent flow:

1. **Pick a voice** (optional — the channel default is Heart).

   ```
   list_voices()
   → { "DefaultVoice": "af_heart", "Voices": [ { "Id": "af_heart", ... }, ... ] }
   ```

2. **Create a channel.**

   ```
   create_audio_collection(
     name = "Morning Briefing",
     description = "My daily news digest, read aloud.",
     defaultVoice = "af_heart",
     defaultLanguage = "en-US"
   )
   → { "Id": "8f1c…", "Name": "Morning Briefing", ... }
   ```

3. **Create a recording** in that channel. Split your text into spoken paragraphs first.

   ```
   create_audio_recording(
     collectionId = "8f1c…",
     name = "Briefing — 12 June",
     paragraphs = [
       "Good morning. Here is your briefing for Thursday the twelfth of June.",
       "First up: the markets opened higher across Europe, led by energy and financials.",
       "And finally, the weather: clear skies through the afternoon, turning cool after sunset."
     ]
   )
   → { "Id": "a23b…", "Status": "Pending", ... }
   ```

4. **Poll until ready.**

   ```
   get_audio_recording(recordingId = "a23b…")
   → { "Status": "Generating", "Progress": 0.33, "StatusDetail": "Generating audio — segment 1 of 3" }
   …
   get_audio_recording(recordingId = "a23b…")
   → { "Status": "Ready", "FilePath": "https://s3.donkeywork.dev/recordings/{userId}/a23b….mp3",
       "DurationSeconds": 41.2 }
   ```

5. **Need a fix?** Re-record from an edited transcript — same recording id, same feed URL.

   ```
   regenerate_audio_recording(
     recordingId = "a23b…",
     paragraphs = [ "Good morning. Here is your corrected briefing for Thursday the twelfth of June.", … ]
   )
   → { "Status": "Pending", ... }   // poll again until Ready
   ```

The recording now appears in the **Morning Briefing** channel feed and the master feed. See
[Subscribe in a podcast app](./subscribe.md) to add it to a podcast app.
