import { create } from "zustand";
import { persist } from "zustand/middleware";
import type { User } from "../types";
import { clearUserScopedQueries } from "../api/queryClient";
import { useRecommendationStore } from "./recommendation";

interface AuthState {
  token: string | null;
  refreshToken: string | null;
  user: User | null;
  setAuth: (token: string, refreshToken: string, user: User) => void;
  setToken: (token: string, refreshToken: string) => void;
  setUser: (user: User) => void;
  logout: () => void;
}

export const useAuth = create<AuthState>()(
  persist(
    (set, get) => ({
      token: null,
      refreshToken: null,
      user: null,
      setAuth: (token, refreshToken, user) => {
        const oldUserId = get().user?.id ?? null;
        if (oldUserId !== user.id) {
          clearUserScopedQueries(oldUserId);
          useRecommendationStore.getState().resetForIdentity(user.id);
        }
        set({ token, refreshToken, user });
      },
      setToken: (token, refreshToken) => set({ token, refreshToken }),
      setUser: (user) => {
        const oldUserId = get().user?.id ?? null;
        if (oldUserId !== user.id) {
          clearUserScopedQueries(oldUserId);
          useRecommendationStore.getState().resetForIdentity(user.id);
        }
        set({ user });
      },
      logout: () => {
        const oldUserId = get().user?.id ?? null;
        clearUserScopedQueries(oldUserId);
        useRecommendationStore.getState().resetForIdentity(null);
        set({ token: null, refreshToken: null, user: null });
      },
    }),
    {
      name: "ecommerce-auth",
      onRehydrateStorage: () => (state) => {
        if (state) {
          // Reconcile the separately persisted recommendation store before React renders private context.
          useRecommendationStore.getState().resetForIdentity(state.user?.id ?? null);
        }
      },
    }
  )
);
