import { type ReactNode, useEffect, useRef, useState } from 'react';
import { Play, Pause, SkipBack, SkipForward, Loader2 } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Tabs, TabsList, TabsTrigger, TabsContent } from '@/components/ui/tabs';
import { cn } from '@/lib/utils';
import type { TtsRecordingV1 } from '@/lib/api';

const SPEEDS = [0.75, 1, 1.25, 1.5, 1.75, 2];

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

function TranscriptTabs({ inputText, processedText }: { inputText?: string | null; processedText?: string | null }) {
  const tabs = [
    inputText ? { key: 'input', label: 'Input text', text: inputText } : null,
    processedText ? { key: 'processed', label: 'Spoken text (processed)', text: processedText } : null,
  ].filter((t): t is { key: string; label: string; text: string } => t !== null);

  if (tabs.length === 0) return null;

  // A single source needs no tab strip — just show it.
  if (tabs.length === 1) {
    return <p className="mt-2 whitespace-pre-wrap break-words text-sm">{tabs[0].text}</p>;
  }

  return (
    <Tabs defaultValue={tabs[0].key} className="mt-2">
      <TabsList className="justify-start">
        {tabs.map((t) => (
          <TabsTrigger key={t.key} value={t.key}>{t.label}</TabsTrigger>
        ))}
      </TabsList>
      {tabs.map((t) => (
        <TabsContent key={t.key} value={t.key}>
          <p className="whitespace-pre-wrap break-words text-sm">{t.text}</p>
        </TabsContent>
      ))}
    </Tabs>
  );
}

export function AudioPlayer({ recording, className, actions }: { recording: TtsRecordingV1; className?: string; actions?: ReactNode }) {
  const audioRef = useRef<HTMLAudioElement | null>(null);
  const [isPlaying, setIsPlaying] = useState(false);
  const [position, setPosition] = useState(0);
  const [duration, setDuration] = useState(recording.durationSeconds || 0);
  const [speed, setSpeed] = useState(1);

  useEffect(() => {
    setIsPlaying(false);
    setPosition(0);
    setDuration(recording.durationSeconds || 0);
  }, [recording.id, recording.durationSeconds]);

  useEffect(() => {
    if (audioRef.current) audioRef.current.playbackRate = speed;
  }, [speed]);

  const cycleSpeed = () => setSpeed((s) => SPEEDS[(SPEEDS.indexOf(s) + 1) % SPEEDS.length]);

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
    <div data-testid="audio-player" className={cn('rounded-2xl border border-border bg-card p-4 space-y-4', className)}>
      <audio
        ref={audioRef}
        src={isReady ? recording.filePath : undefined}
        preload="metadata"
        onPlay={() => setIsPlaying(true)}
        onPause={() => setIsPlaying(false)}
        onEnded={() => { setIsPlaying(false); setPosition(duration); }}
        onTimeUpdate={(e) => setPosition(e.currentTarget.currentTime)}
        onLoadedMetadata={(e) => {
          e.currentTarget.playbackRate = speed;
          if (isFinite(e.currentTarget.duration) && e.currentTarget.duration > 0) {
            setDuration(e.currentTarget.duration);
          }
        }}
      />

      <div className="flex items-start justify-between gap-2">
        <div className="min-w-0">
          <div className="truncate text-sm font-medium">{recording.chapterTitle || recording.name}</div>
          <div className="text-xs text-muted-foreground tabular-nums">
            {recording.status === 'Ready'
              ? `${fmt(position)} / ${fmt(duration)}`
              : (recording.statusDetail ?? recording.status)}
          </div>
        </div>
        {actions && <div className="-mr-1 -mt-1 shrink-0">{actions}</div>}
      </div>

      <input
        type="range"
        min={0}
        max={100}
        value={isReady ? pct : Math.round((recording.progress ?? 0) * 100)}
        onChange={onScrub}
        disabled={!isReady}
        className="w-full accent-primary"
      />

      <div className="relative flex items-center justify-center gap-2">
        <Button onClick={() => seek(-15)} variant="outline" size="icon" className="h-9 w-9" disabled={!isReady} title="Back 15s">
          <SkipBack className="h-4 w-4" />
        </Button>
        <Button
          onClick={togglePlay}
          size="icon"
          className="h-11 w-11 rounded-full"
          disabled={!isReady}
          title={isReady ? (isPlaying ? 'Pause' : 'Play') : recording.status}
        >
          {!isReady ? <Loader2 className="h-5 w-5 animate-spin" /> : isPlaying ? <Pause className="h-5 w-5" /> : <Play className="h-5 w-5 ml-0.5" />}
        </Button>
        <Button onClick={() => seek(15)} variant="outline" size="icon" className="h-9 w-9" disabled={!isReady} title="Forward 15s">
          <SkipForward className="h-4 w-4" />
        </Button>
        <div className="absolute inset-y-0 right-0 flex items-center">
          <Button
            onClick={cycleSpeed}
            variant="outline"
            size="sm"
            disabled={!isReady}
            className="shrink-0 px-2 tabular-nums"
            title="Playback speed"
          >
            {speed}×
          </Button>
        </div>
      </div>

      {(recording.transcript || recording.processedTranscript) && (
        <details className="border-t border-border pt-3">
          <summary className="cursor-pointer select-none text-xs font-medium text-muted-foreground">
            Transcript
          </summary>
          <TranscriptTabs
            inputText={recording.transcript}
            processedText={recording.processedTranscript}
          />
        </details>
      )}
    </div>
  );
}
