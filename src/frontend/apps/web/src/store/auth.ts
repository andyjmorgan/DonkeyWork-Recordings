import { create } from 'zustand';
import { createJSONStorage, persist } from 'zustand/middleware';

export interface User {
  id: string;
  email?: string;
  name?: string;
  username?: string;
}

interface RefreshTokenResponse {
  accessToken: string;
  refreshToken: string | null;
  idToken: string | null;
  expiresIn: number;
  tokenType: string;
}

export type RefreshResult =
  | { ok: true }
  | { ok: false; reason: 'rejected' | 'network' };

interface AuthState {
  accessToken: string | null;
  refreshToken: string | null;
  idToken: string | null;
  expiresAt: number | null;
  tokenIssuedAt: number | null;
  user: User | null;
  isAuthenticated: boolean;
  isRefreshing: boolean;
  refreshPromise: Promise<RefreshResult> | null;
  hasHydrated: boolean;

  setTokens: (accessToken: string, refreshToken: string | null, expiresIn: number, idToken?: string | null) => void;
  setUser: (user: User) => void;
  logout: () => void;
  isTokenExpired: () => boolean;
  shouldRefreshToken: () => boolean;
  refreshTokens: () => Promise<RefreshResult>;
}

export const useAuthStore = create<AuthState>()(
  persist(
    (set, get) => ({
      accessToken: null,
      refreshToken: null,
      idToken: null,
      expiresAt: null,
      tokenIssuedAt: null,
      user: null,
      isAuthenticated: false,
      isRefreshing: false,
      refreshPromise: null,
      hasHydrated: false,

      setTokens: (accessToken, refreshToken, expiresIn, idToken) => {
        const now = Date.now();
        const expiresAt = now + expiresIn * 1000;
        set((state) => ({
          accessToken,
          refreshToken,
          idToken: idToken !== undefined ? idToken : state.idToken,
          expiresAt,
          tokenIssuedAt: now,
          isAuthenticated: true,
        }));
      },

      setUser: (user) => set({ user }),

      logout: () => {
        set({
          accessToken: null,
          refreshToken: null,
          idToken: null,
          expiresAt: null,
          tokenIssuedAt: null,
          user: null,
          isAuthenticated: false,
          isRefreshing: false,
          refreshPromise: null,
        });
      },

      isTokenExpired: () => {
        const { expiresAt } = get();
        if (!expiresAt) return true;
        return Date.now() > expiresAt - 30_000;
      },

      shouldRefreshToken: () => {
        const { expiresAt, tokenIssuedAt, refreshToken } = get();
        if (!expiresAt || !refreshToken) return false;

        const now = Date.now();
        const timeRemaining = expiresAt - now;

        if (tokenIssuedAt) {
          const tokenLifetime = expiresAt - tokenIssuedAt;
          const refreshThreshold = tokenLifetime * 0.2;
          return timeRemaining <= refreshThreshold;
        }

        return timeRemaining <= 120_000;
      },

      refreshTokens: async () => {
        const state = get();

        // Coalesce concurrent callers onto a single in-flight refresh. The
        // promise is published synchronously below (before any await), so a
        // second caller in the same tick sees it here instead of starting a
        // parallel refresh — which would spend the rotated refresh token and
        // log the user out.
        if (state.refreshPromise) {
          return state.refreshPromise;
        }

        const { refreshToken } = state;
        if (!refreshToken) {
          return { ok: false, reason: 'rejected' };
        }

        const refreshPromise: Promise<RefreshResult> = (async () => {
          const maxAttempts = 3;
          const baseDelay = 1000;
          let rejected = false;

          for (let attempt = 1; attempt <= maxAttempts; attempt++) {
            try {
              const currentRefreshToken = get().refreshToken;
              if (!currentRefreshToken) {
                rejected = true;
                break;
              }

              const response = await fetch('/api/v1/auth/refresh', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ refreshToken: currentRefreshToken }),
              });

              if (response.ok) {
                const data: RefreshTokenResponse = await response.json();
                const now = Date.now();
                const newExpiresAt = now + data.expiresIn * 1000;
                set((s) => ({
                  accessToken: data.accessToken,
                  refreshToken: data.refreshToken ?? currentRefreshToken,
                  idToken: data.idToken ?? s.idToken,
                  expiresAt: newExpiresAt,
                  tokenIssuedAt: now,
                  isRefreshing: false,
                  refreshPromise: null,
                }));
                return { ok: true };
              }

              if (response.status === 400 || response.status === 401) {
                rejected = true;
                break;
              }
            } catch {
              // network error — fall through to retry
            }

            if (attempt < maxAttempts) {
              await new Promise((resolve) => setTimeout(resolve, baseDelay * 2 ** (attempt - 1)));
            }
          }

          set({ isRefreshing: false, refreshPromise: null });
          return { ok: false, reason: rejected ? 'rejected' : 'network' };
        })();

        // Publish synchronously, before returning, so the guard above catches
        // any concurrent caller.
        set({ isRefreshing: true, refreshPromise });
        return refreshPromise;
      },
    }),
    {
      name: 'donkeywork-recordings-auth',
      storage: createJSONStorage(() => localStorage),
      onRehydrateStorage: () => () => {
        useAuthStore.setState({ hasHydrated: true });
      },
      partialize: (state) => ({
        accessToken: state.accessToken,
        refreshToken: state.refreshToken,
        idToken: state.idToken,
        expiresAt: state.expiresAt,
        tokenIssuedAt: state.tokenIssuedAt,
        user: state.user,
        isAuthenticated: state.isAuthenticated,
      }),
    },
  ),
);
