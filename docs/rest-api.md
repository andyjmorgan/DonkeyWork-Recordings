# REST & API keys quickstart

Everything the MCP server can do is also available over a plain REST API under `/api/v1`. This is
handy for scripts, cron jobs, and any client that doesn't speak MCP.

Base URL (hosted): `https://recordings.donkeywork.dev`

## API keys

Programmatic clients authenticate with a **user API key** sent as an `X-Api-Key` header.

### Create a key

In the web app, go to **Profile → API Keys → New key**, give it a name, and pick a **scope**:

| Scope | Allows |
|-------|--------|
| `RestAndMcp` | Everything — REST (`/api/*`) and the MCP endpoint (`POST /mcp`). |
| `McpOnly` | The MCP endpoint only. |
| `RestOnly` | The REST API only (`/api/*`). |

The secret (`dk_…`) is shown **once** at creation — copy it then. Scope is enforced on every request;
a mis-scoped call returns `401`. Revoking a key takes effect immediately.

### Use a key

```bash
export DWR=https://recordings.donkeywork.dev
export KEY=dk_your_key_here
# send this header on every request:  -H "X-Api-Key: $KEY"
```

## Endpoints

### Voices

```
GET /api/v1/voices
```

### Channels (collections)

```
GET    /api/v1/collections                 # list (offset, limit)
POST   /api/v1/collections                 # create
GET    /api/v1/collections/{id}            # get one
PUT    /api/v1/collections/{id}            # update
DELETE /api/v1/collections/{id}            # delete (recordings become unfiled)
GET    /api/v1/collections/{id}/recordings # list recordings in a channel
```

### Recordings

```
GET    /api/v1/recordings                  # list (offset, limit, unfiledOnly)
POST   /api/v1/recordings/generate         # create — returns 202 Accepted, Status=Pending
GET    /api/v1/recordings/{id}             # get one (poll Status here)
POST   /api/v1/recordings/{id}/regenerate  # re-record from an edited transcript — 202 Accepted
PUT    /api/v1/recordings/{id}             # update metadata
DELETE /api/v1/recordings/{id}             # delete
PUT    /api/v1/recordings/{id}/collection  # move to another channel
GET    /api/v1/recordings/{id}/events      # Server-Sent Events: live generation lifecycle
```

### Progressive playback (SSE + chunks)

While a recording is `Generating`, each synthesized segment is published as an ephemeral WAV clip
so clients can start playback before the final mp3 exists. Two ways to consume them:

- **SSE** — `GET /api/v1/recordings/{id}/events` (same `X-Api-Key` auth) streams:
  - `chunk-ready` — `{"index": 0, "url": "…/chunks/0.wav", "playableUpTo": 0}`
  - `progress` — `{"progress": 0.42, "statusDetail": "Generating audio — segment 3 of 9"}`
  - `ready` — `{"url": "…final mp3 url…"}` (stream then closes)
  - `failed` — `{"error": "…"}` (stream then closes)

  The stream replays current state on connect (already-ready chunks first), then follows live. A
  `: keep-alive` comment is written every ~15s.
- **Polling** — `GET /api/v1/recordings/{id}` carries `chunks[]` (`index`, `url`, `sizeBytes`,
  `durationSeconds`) and `playableUpTo`.

`playableUpTo` is the highest index N such that chunks `0..N` are all available — only fetch/play
up to it (chunks can complete out of order). Chunk URLs are public like the final mp3's
`filePath`. Chunks are ephemeral: they are deleted a few minutes after the recording settles, so
switch to the final mp3 on `ready`.

### Feed settings & feeds

```
GET /api/v1/feed-settings                  # master feed metadata
GET /feeds/{userId}/all.xml                # master RSS feed
GET /feeds/{userId}/{collectionId}.xml     # per-channel RSS feed
```

## Worked example

Create a channel, post a recording, and poll until it's ready.

```bash
# 1. Create a channel
COLLECTION=$(curl -s -X POST "$DWR/api/v1/collections" \
  -H "X-Api-Key: $KEY" -H "Content-Type: application/json" \
  -d '{
        "name": "Morning Briefing",
        "description": "My daily digest, read aloud.",
        "defaultVoice": "af_heart",
        "defaultLanguage": "en-US"
      }')
COLLECTION_ID=$(echo "$COLLECTION" | jq -r '.id')

# 2. Create a recording (paragraphs you split yourself)
RECORDING=$(curl -s -X POST "$DWR/api/v1/recordings/generate" \
  -H "X-Api-Key: $KEY" -H "Content-Type: application/json" \
  -d "{
        \"collectionId\": \"$COLLECTION_ID\",
        \"name\": \"Briefing — 12 June\",
        \"paragraphs\": [
          \"Good morning. Here is your briefing for Thursday the twelfth of June.\",
          \"First up: the markets opened higher across Europe.\",
          \"And finally, the weather: clear skies through the afternoon.\"
        ]
      }")
RECORDING_ID=$(echo "$RECORDING" | jq -r '.id')

# 3. Poll until Ready
curl -s "$DWR/api/v1/recordings/$RECORDING_ID" -H "X-Api-Key: $KEY" \
  | jq '{status, progress, statusDetail, filePath, durationSeconds}'
```

When `status` is `Ready`, `filePath` is the public mp3 URL and the recording is live in the channel
feed. To fix the transcript later, POST the edited paragraphs to
`/api/v1/recordings/$RECORDING_ID/regenerate` — the mp3 is overwritten in place and the feed URL never
changes.
