import { clsx, type ClassValue } from 'clsx';
import { twMerge } from 'tailwind-merge';

export function cn(...inputs: ClassValue[]): string {
  return twMerge(clsx(inputs));
}

// Re-recording overwrites the mp3 at the same URL, so any consumer of a recording's filePath
// (player src, download link) must append the last-updated token or the browser serves stale audio.
export function withCacheBust(url: string, version: string | number | null | undefined): string {
  return `${url}${url.includes('?') ? '&' : '?'}v=${encodeURIComponent(version ?? '')}`;
}
