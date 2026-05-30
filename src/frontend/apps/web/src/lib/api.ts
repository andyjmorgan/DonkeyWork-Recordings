import { fetchWithAuth } from '@/lib/fetchWithAuth';

async function json<T>(url: string, init?: RequestInit): Promise<T> {
  const res = await fetchWithAuth(url, init);
  if (!res.ok) {
    const body = await res.text();
    throw new ApiError(res.status, body || res.statusText);
  }
  if (res.status === 204) return undefined as T;
  return (await res.json()) as T;
}

export class ApiError extends Error {
  constructor(public status: number, message: string) {
    super(message);
    this.name = 'ApiError';
  }
}

export interface TtsRecordingV1 {
  id: string;
  name: string;
  description: string;
  filePath: string;
  transcript: string;
  processedTranscript: string;
  ttsModel: string;
  contentType: string;
  sizeBytes: number;
  durationSeconds: number;
  voice?: string;
  language?: string;
  collectionId?: string;
  sequenceNumber?: number;
  chapterTitle?: string;
  status: 'Pending' | 'Generating' | 'Ready' | 'Failed';
  progress: number;
  errorMessage?: string;
  createdAt: string;
  updatedAt?: string;
}

export interface AudioCollectionV1 {
  id: string;
  name: string;
  description: string;
  coverImagePath?: string;
  defaultTtsModel?: string;
  defaultVoice?: string;
  defaultLanguage?: string;
  tone?: string;
  author?: string;
  authorEmail?: string;
  itunesCategory?: string;
  isExplicit: boolean;
  link?: string;
  recordingCount: number;
  createdAt: string;
  updatedAt?: string;
}

export interface ListResponse<T> { items: T[]; totalCount: number }

export interface TtsVoice { id: string; language: string; name: string; emotion?: string; rating?: string; sampleUrl?: string }

export interface TtsModelV1 {
  key: string;
  displayName: string;
  supportsVoiceSelection: boolean;
  defaultVoice: string;
  isDefault: boolean;
  voices: TtsVoice[];
}

export interface StartAudioGenerationRequest {
  text: string;
  name: string;
  collectionId: string;
  description?: string;
  ttsModel?: string;
  voice?: string;
  language?: string;
  sequenceNumber?: number;
  chapterTitle?: string;
}

export interface CreateAudioCollectionRequest {
  name: string;
  description?: string;
  coverImagePath?: string;
  defaultTtsModel?: string;
  defaultVoice?: string;
  defaultLanguage?: string;
  tone?: string;
  author?: string;
  authorEmail?: string;
  itunesCategory?: string;
  isExplicit?: boolean;
  link?: string;
}

export type UpdateAudioCollectionRequest = Partial<CreateAudioCollectionRequest>;

export interface MoveRecordingRequest {
  collectionId: string;
  sequenceNumber?: number;
  chapterTitle?: string;
}

export interface FeedSettingsV1 {
  title: string;
  description: string;
  author?: string;
  authorEmail?: string;
  language: string;
  coverImagePath?: string;
  link?: string;
  isExplicit: boolean;
  itunesCategory?: string;
  masterFeedUrl: string;
}

export type UpdateFeedSettingsRequest = Partial<Omit<FeedSettingsV1, 'masterFeedUrl'>>;

export const recordings = {
  list: (offset = 0, limit = 50, unfiledOnly = false) =>
    json<ListResponse<TtsRecordingV1>>(`/api/v1/recordings?offset=${offset}&limit=${limit}&unfiledOnly=${unfiledOnly}`),
  get: (id: string) => json<TtsRecordingV1>(`/api/v1/recordings/${id}`),
  generate: (req: StartAudioGenerationRequest) =>
    json<TtsRecordingV1>('/api/v1/recordings/generate', {
      method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(req),
    }),
  delete: (id: string) => json<void>(`/api/v1/recordings/${id}`, { method: 'DELETE' }),
  move: (id: string, req: MoveRecordingRequest) =>
    json<TtsRecordingV1>(`/api/v1/recordings/${id}/collection`, {
      method: 'PUT', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(req),
    }),
};

export const collections = {
  list: (offset = 0, limit = 50) =>
    json<ListResponse<AudioCollectionV1>>(`/api/v1/collections?offset=${offset}&limit=${limit}`),
  get: (id: string) => json<AudioCollectionV1>(`/api/v1/collections/${id}`),
  create: (req: CreateAudioCollectionRequest) =>
    json<AudioCollectionV1>('/api/v1/collections', {
      method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(req),
    }),
  update: (id: string, req: UpdateAudioCollectionRequest) =>
    json<AudioCollectionV1>(`/api/v1/collections/${id}`, {
      method: 'PUT', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(req),
    }),
  delete: (id: string) => json<void>(`/api/v1/collections/${id}`, { method: 'DELETE' }),
  listRecordings: (id: string, offset = 0, limit = 100) =>
    json<ListResponse<TtsRecordingV1>>(`/api/v1/collections/${id}/recordings?offset=${offset}&limit=${limit}`),
};

export const voices = {
  models: () => json<TtsModelV1[]>('/api/v1/voices/models'),
  preview: async (req: { model?: string; voice: string; language: string; tone?: string }): Promise<Blob> => {
    const res = await fetchWithAuth('/api/v1/voices/preview', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(req),
    });
    if (!res.ok) throw new ApiError(res.status, (await res.text()) || res.statusText);
    return res.blob();
  },
};

export type ApiKeyScope = 'RestAndMcp' | 'McpOnly' | 'RestOnly';

export interface ApiKeyItemV1 {
  id: string;
  name: string;
  description?: string | null;
  maskedKey: string;
  createdAt: string;
  lastUsedAt?: string | null;
  scope: ApiKeyScope;
}

export interface CreateApiKeyResponseV1 {
  id: string;
  name: string;
  description?: string | null;
  key: string;
  createdAt: string;
  scope: ApiKeyScope;
}

export const apiKeys = {
  list: () => json<ApiKeyItemV1[]>('/api/v1/apikeys'),
  create: (req: { name: string; description?: string; scope?: ApiKeyScope }) =>
    json<CreateApiKeyResponseV1>('/api/v1/apikeys', {
      method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(req),
    }),
  delete: (id: string) => json<void>(`/api/v1/apikeys/${id}`, { method: 'DELETE' }),
};

export const feedSettings = {
  get: () => json<FeedSettingsV1>('/api/v1/feed-settings'),
  update: (req: UpdateFeedSettingsRequest) =>
    json<FeedSettingsV1>('/api/v1/feed-settings', {
      method: 'PUT', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(req),
    }),
};
