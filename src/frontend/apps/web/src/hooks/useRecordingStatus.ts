import { useEffect, useState } from 'react';
import { recordings, type TtsRecordingV1 } from '@/lib/api';

const POLL_INTERVAL_MS = 3000;

export function useRecordingStatus(initial: TtsRecordingV1 | null): TtsRecordingV1 | null {
  const [recording, setRecording] = useState<TtsRecordingV1 | null>(initial);

  // Re-sync when the row identity changes, and also when its status changes upstream —
  // e.g. an edit dialog re-records and flips it back to Pending, which must restart polling.
  useEffect(() => {
    setRecording(initial);
  }, [initial?.id, initial?.status]);

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
