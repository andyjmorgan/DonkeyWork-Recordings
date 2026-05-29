# DonkeyWork Recordings — Web frontend

pnpm workspace, single app for now (`apps/web`).

## Stack

- React 19 + Vite 7, TypeScript ~5.9
- Tailwind 3.4 + shadcn primitives (lifted from `DonkeyWork-Agents`)
- Zustand persisted auth store, react-router-dom v7, sonner toasts
- `@/lib/api` typed helpers wrap `fetchWithAuth` against the backend

## Dev

```bash
pnpm install
pnpm dev          # http://localhost:5199, proxies /api, /feeds, /health, /.well-known → http://localhost:5050
pnpm build
```

The Vite dev proxy expects the .NET host running on `:5050`. See the root `README.md` for the
backend + Postgres + cluster port-forward instructions.

## Layout

```
apps/web/src/
  App.tsx                  # routes (login + callback open; everything else guarded + wrapped in AppLayout)
  main.tsx                 # root: BrowserRouter + Sonner Toaster
  components/
    AuthGuard.tsx          # redirects to /login until hydrated + authenticated
    layout/                # AppLayout + Sidebar (Channels / Feed Settings / Profile)
    audio/                 # AudioPlayer, NewRecordingDialog, AudioCollectionFormDialog, MoveRecordingDialog
    ui/                    # shadcn primitives (button, card, dialog, input, label, select, etc.)
  hooks/
    useTokenRefresh.ts     # 60s interval + focus-driven proactive Keycloak refresh
    useRecordingStatus.ts  # 3s poll while Pending/Generating; stops on Ready/Failed
  lib/
    api.ts                 # typed wrappers over /api/v1/* (recordings, collections, voices, feedSettings)
    fetchWithAuth.ts       # Bearer + refresh + 401-retry + logout-on-rejected
    utils.ts               # cn(...) helper
  pages/                   # LoginPage, LoginCallbackPage, HomePage, ChannelsListPage, ChannelDetailPage,
                           # FeedSettingsPage, ProfilePage
  store/
    auth.ts                # Zustand store, persisted to localStorage as donkeywork-recordings-auth
  index.css                # design tokens lifted verbatim from DonkeyWork-Agents
```

## Production build

`Dockerfile` here: node:22-alpine → pnpm build → nginx:alpine serving `apps/web/dist`. The bundled
nginx config proxies `/api/`, `/feeds/`, `/.well-known/`, and `POST /` (MCP) to `http://api:8080` —
expects an `api` service in the same namespace.
