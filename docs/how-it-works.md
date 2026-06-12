# How it works

This page walks the synthesis pipeline from the moment a recording is created to the moment it shows
up in a podcast app.

## The pipeline

```
create (paragraphs[])  →  insert a Pending recording row + enqueue the job
                       →  background worker picks it up
                       →  defensive SSML cleanup (strip stray [PAUSE=…]/[EMPHASIS=…] tokens)
                       →  chunk the paragraphs to fit the TTS request size
                       →  Kokoro TTS synthesises each chunk to audio
                       →  ffmpeg concatenates the chunks and encodes to mp3
                       →  ffprobe measures the duration
                       →  upload the mp3 to SeaweedFS (S3) at {userId}/{recordingId}.mp3
                       →  recording row Status = Ready
```

The public mp3 ends up at a stable URL:

```
https://s3.donkeywork.dev/recordings/{userId}/{recordingId}.mp3
```

### Why it's asynchronous

Synthesis takes time, so `create` returns immediately with a `Pending` recording and a job id (the
recording id). The work happens on a background worker; you **poll** the recording until its status is
`Ready` or `Failed`.

Today the queue is an in-memory `Channel<T>` drained by a hosted `BackgroundService` — simple and
self-contained, no external broker required.

### Status lifecycle

| Status | Meaning |
|--------|---------|
| `Pending` | Accepted and queued; synthesis hasn't started yet. |
| `Generating` | In progress. `Progress` (0–1) and `StatusDetail` (e.g. *"Generating audio — segment 3 of 9"*) report live state. |
| `Ready` | Done. `FilePath` (public mp3 URL), `DurationSeconds`, and the transcript are populated. |
| `Failed` | Synthesis failed. |

## Kokoro TTS

Speech is synthesised with **Kokoro TTS**. Each chunk of paragraphs is sent to Kokoro with the chosen
**voice** (a voice id such as `af_heart`) and **language** (BCP-47, e.g. `en-US`). Voice and language
default to the channel's settings and can be overridden per recording.

Voices are graded for quality; list them via the `list_voices` MCP tool or the `GET /api/v1/voices`
endpoint, and use a voice's `Id` when creating a channel or recording. The default voice is **Heart**
(`af_heart`).

## Re-recording in place

Editing a recording's transcript re-runs the same pipeline against the **existing recording id**
(`POST /api/v1/recordings/{id}/regenerate`, or the `regenerate_audio_recording` MCP tool). The mp3 is
overwritten at the same `{userId}/{recordingId}.mp3` object key, so:

- the feed item's media URL is unchanged,
- voice, language, channel and metadata are preserved, and
- only the audio and transcript change.

Subscribers simply get the updated audio the next time they refresh the feed.

## Storage and feeds

The mp3 lives in a public-read SeaweedFS bucket (S3-compatible). The RSS feeds — per channel and the
master feed — reference these mp3 URLs and carry full iTunes metadata. See
[Subscribe in a podcast app](./subscribe.md).
