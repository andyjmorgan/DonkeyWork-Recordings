import { useEffect, useRef, useState } from 'react';
import { Play, Pause, SkipBack, SkipForward, Loader2 } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { cn } from '@/lib/utils';
import type { TtsRecordingV1 } from '@/lib/api';

function fmt(seconds: number): string {
  if (!isFinite(seconds) || seconds <= 0) return '00:00';
  const s = Math.floor(seconds);
  const h = Math.floor(s / 3600);
  const m = Math.floor((s % 3600) / 60);
  const r = s % 60;
  return h > 0
    ? `${h.toString().padStart(2, '0')}:${m.toString().padStart(2, '0')}:${r.toString().padStart(2, '0')}`
    : `${m.toString().padStart(2, '0')}:${r.toString().padStart(2, '0')}`;
}

export function AudioPlayer({ recording, className }: { recording: TtsRecordingV1; className?: string }) {
  const audioRef = useRef<HTMLAudioElement | null>(null);
  const [isPlaying, setIsPlaying] = useState(false);
  const [position, setPosition] = useState(0);
  const [duration, setDuration] = useState(recording.durationSeconds || 0);

  useEffect(() => {
    setIsPlaying(false);
    setPosition(0);
    setDuration(recording.durationSeconds || 0);
  }, [recording.id, recording.durationSeconds]);

  const togglePlay = async () => {
    const a = audioRef.current;
    if (!a) return;
    if (isPlaying) {
      a.pause();
    } else {
      try {
        await a.play();
      } catch {
        // user gesture required / network issue — leave UI in paused state
      }
    }
  };

  const seek = (delta: number) => {
    const a = audioRef.current;
    if (!a) return;
    a.currentTime = Math.max(0, Math.min(a.duration || duration, a.currentTime + delta));
    setPosition(a.currentTime);
  };

  const onScrub = (event: React.ChangeEvent<HTMLInputElement>) => {
    const a = audioRef.current;
    if (!a) return;
    const pct = Number(event.target.value);
    a.currentTime = (pct / 100) * (a.duration || duration);
    setPosition(a.currentTime);
  };

  const isReady = recording.status === 'Ready' && recording.filePath;
  const pct = duration > 0 ? (position / duration) * 100 : 0;

  return (
    <div className={cn('rounded-2xl border border-border bg-card p-4 space-y-3', className)}>
      <audio
        ref={audioRef}
        src={isReady ? recording.filePath : undefined}
        preload="metadata"
        onPlay={() => setIsPlaying(true)}
        onPause={() => setIsPlaying(false)}
        onEnded={() => { setIsPlaying(false); setPosition(duration); }}
        onTimeUpdate={(e) => setPosition(e.currentTarget.currentTime)}
        onLoadedMetadata={(e) => {
          if (isFinite(e.currentTarget.duration) && e.currentTarget.duration > 0) {
            setDuration(e.currentTarget.duration);
          }
        }}
      />

      <div className="flex items-center gap-3">
        <Button onClick={() => seek(-15)} variant="outline" size="icon" disabled={!isReady}>
          <SkipBack className="h-4 w-4" />
        </Button>
        <Button
          onClick={togglePlay}
          size="icon"
          className="h-12 w-12 rounded-full"
          disabled={!isReady}
          title={isReady ? (isPlaying ? 'Pause' : 'Play') : recording.status}
        >
          {!isReady ? <Loader2 className="h-5 w-5 animate-spin" /> : isPlaying ? <Pause className="h-5 w-5" /> : <Play className="h-5 w-5 ml-0.5" />}
        </Button>
        <Button onClick={() => seek(15)} variant="outline" size="icon" disabled={!isReady}>
          <SkipForward className="h-4 w-4" />
        </Button>
        <div className="flex-1 min-w-0">
          <div className="text-sm font-medium truncate">{recording.chapterTitle || recording.name}</div>
          <div className="text-xs text-muted-foreground">
            {recording.status === 'Ready' ? `${fmt(position)} / ${fmt(duration)}` : recording.status}
          </div>
        </div>
      </div>

      <input
        type="range"
        min={0}
        max={100}
        value={pct}
        onChange={onScrub}
        disabled={!isReady}
        className="w-full accent-primary"
      />

      {(recording.transcript || recording.processedTranscript) && (
        <details className="border-t border-border pt-2">
          <summary className="cursor-pointer select-none text-xs font-medium text-muted-foreground">
            Transcript
          </summary>
          <div className="mt-2 space-y-3">
            {recording.transcript && (
              <div>
                <div className="mb-1 text-xs font-medium text-muted-foreground">Input text</div>
                <p className="whitespace-pre-wrap break-words text-sm">{recording.transcript}</p>
              </div>
            )}
            {recording.processedTranscript && (
              <div>
                <div className="mb-1 text-xs font-medium text-muted-foreground">Spoken text (processed)</div>
                <p className="whitespace-pre-wrap break-words text-sm">{recording.processedTranscript}</p>
              </div>
            )}
          </div>
        </details>
      )}
    </div>
  );
}
