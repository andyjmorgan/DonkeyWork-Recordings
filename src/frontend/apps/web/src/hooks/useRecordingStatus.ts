import { useEffect, useState } from 'react';
import { recordings, type TtsRecordingV1 } from '@/lib/api';

const POLL_INTERVAL_MS = 3000;

export function useRecordingStatus(initial: TtsRecordingV1 | null): TtsRecordingV1 | null {
  const [recording, setRecording] = useState<TtsRecordingV1 | null>(initial);

  // Re-seed from the prop when it's a genuinely newer external update — a different row,
  // or a more recent `updatedAt` (e.g. an edit dialog re-records and flips it back to
  // Pending, which must restart polling). Guarding on the timestamp is what stops a render
  // loop: this hook also pushes its polled value back up to the parent list, so without the
  // "strictly newer" check the parent's stale write-back would be re-adopted here and the two
  // would trade places every render, flickering status/length. Older or equal props are ignored.
  useEffect(() => {
    setRecording((cur) => {
      if (!initial || !cur || cur.id !== initial.id) return initial;
      const curAt = Date.parse(cur.updatedAt ?? '');
      const nextAt = Date.parse(initial.updatedAt ?? '');
      return Number.isFinite(nextAt) && (!Number.isFinite(curAt) || nextAt > curAt) ? initial : cur;
    });
  }, [initial]);

  useEffect(() => {
    if (!recording) return;
    if (recording.status === 'Ready' || recording.status === 'Failed') return;

    let cancelled = false;
    const interval = window.setInterval(async () => {
      try {
        const fresh = await recordings.get(recording.id);
        if (cancelled) return;
        setRecording(fresh);
        if (fresh.status === 'Ready' || fresh.status === 'Failed') {
          window.clearInterval(interval);
        }
      } catch {
        // network blip — keep polling
      }
    }, POLL_INTERVAL_MS);

    return () => {
      cancelled = true;
      window.clearInterval(interval);
    };
  }, [recording?.id, recording?.status]);

  return recording;
}
