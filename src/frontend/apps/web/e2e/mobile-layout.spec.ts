import { test, expect, type Page } from '@playwright/test';

// Layout regressions are caught by mocking a channel + a ready recording and
// asserting nothing escapes the (mobile) viewport. Auth is real (storageState);
// the data is mocked so the check is deterministic and doesn't need the backend.
const CHANNEL_ID = '0e2e0000-0000-4000-8000-000000000001';

const channel = {
  id: CHANNEL_ID,
  name: 'Pipeline Tests (e2e)',
  description: 'Scratch channel for verifying the TTS pipeline end to end.',
  defaultTtsModel: 'kokoro',
  defaultVoice: 'af_heart',
  defaultLanguage: 'en-US',
  tone: 'clear, neutral technical narrator',
  isExplicit: false,
  recordingCount: 1,
  createdAt: new Date(0).toISOString(),
};

const recording = {
  id: '0e2e0000-0000-4000-8000-000000000002',
  name: 'End-to-end smoke test (retry)',
  description: '',
  filePath: 'https://s3.donkeywork.dev/kokoro-samples/af_heart.wav',
  transcript: 'Input text fixture for the mobile layout check.',
  processedTranscript: 'Processed transcript fixture.',
  ttsModel: 'kokoro',
  contentType: 'audio/wav',
  sizeBytes: 100000,
  durationSeconds: 12,
  voice: 'af_heart',
  language: 'en-US',
  collectionId: CHANNEL_ID,
  sequenceNumber: 1,
  status: 'Ready',
  progress: 1,
  createdAt: new Date(0).toISOString(),
};

async function mockChannel(page: Page) {
  await page.route(`**/api/v1/collections/${CHANNEL_ID}`, (route) => route.fulfill({ json: channel }));
  await page.route(`**/api/v1/collections/${CHANNEL_ID}/recordings**`, (route) =>
    route.fulfill({ json: { items: [recording], totalCount: 1 } }));
}

async function horizontalOverflow(page: Page): Promise<number> {
  return page.evaluate(() => document.documentElement.scrollWidth - document.documentElement.clientWidth);
}

test('channels list fits the mobile viewport', async ({ page }) => {
  await page.route('**/api/v1/collections?**', (route) =>
    route.fulfill({ json: { items: [channel], totalCount: 1 } }));

  await page.goto('/channels');
  await expect(page.getByRole('heading', { name: 'Channels' })).toBeVisible();
  expect(await horizontalOverflow(page), 'channels list must not scroll horizontally').toBeLessThanOrEqual(1);
});

test('channel detail fits the mobile viewport', async ({ page }) => {
  await mockChannel(page);

  await page.goto(`/channels/${CHANNEL_ID}`);
  await expect(page.getByRole('heading', { name: channel.name })).toBeVisible();
  await expect(page.getByText(recording.name)).toBeVisible();

  expect(await horizontalOverflow(page), 'channel detail must not scroll horizontally').toBeLessThanOrEqual(1);
});

test('the audio player card stays within the viewport', async ({ page }) => {
  await mockChannel(page);

  await page.goto(`/channels/${CHANNEL_ID}`);
  const card = page.getByTestId('audio-player').first();
  await expect(card).toBeVisible();

  const box = await card.boundingBox();
  const viewport = page.viewportSize();
  expect(box, 'player card should render').not.toBeNull();
  expect(box!.x, 'player card must not start off the left edge').toBeGreaterThanOrEqual(-1);
  expect(box!.x + box!.width, 'player card must not exceed the viewport width').toBeLessThanOrEqual((viewport?.width ?? 0) + 1);
});
