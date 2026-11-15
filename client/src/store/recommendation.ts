import { create } from "zustand";
import { persist } from "zustand/middleware";
import type { Product } from "../types";
import {
  emptyRecommendationSession,
  resetRecommendationSession,
  type RecommendationSessionData,
} from "../api/sessionIsolation";

interface RecommendationState extends RecommendationSessionData {
  trackSearch: (query: string, userId: number | null) => void;
  trackView: (productId: number, userId: number | null) => void;
  setOwnerUserId: (userId: number | null) => void;
  setRecommendations: (
    products: Product[],
    reason: string,
    type: RecommendationSessionData["recommendationType"],
    userId: number | null,
  ) => void;
  resetForIdentity: (userId: number | null) => void;
  clearHistory: (userId: number | null) => void;
}

export const useRecommendationStore = create<RecommendationState>()(
  persist(
    (set) => ({
      ...emptyRecommendationSession(null),

      trackSearch: (query, userId) => {
        const trimmed = query.trim();
        if (!trimmed) return;
        set((state) => {
          const current = resetRecommendationSession(state, userId);
          const filtered = current.sessionSearches.filter(
            (item) => item.toLowerCase() !== trimmed.toLowerCase(),
          );
          return { ...current, sessionSearches: [...filtered, trimmed].slice(-8) };
        });
      },

      trackView: (productId, userId) => {
        if (!productId) return;
        set((state) => {
          const current = resetRecommendationSession(state, userId);
          const filtered = current.viewedProductIds.filter((id) => id !== productId);
          return { ...current, viewedProductIds: [...filtered, productId].slice(-10) };
        });
      },

      setOwnerUserId: (userId) => {
        set((state) => ({ ...resetRecommendationSession(state, userId) }));
      },

      setRecommendations: (products, reason, type, userId) => {
        set((state) => ({
          ...resetRecommendationSession(state, userId),
          activeRecommendations: products,
          recommendationReason: reason,
          recommendationType: type,
        }));
      },

      resetForIdentity: (userId) => {
        set((state) => ({ ...resetRecommendationSession(state, userId) }));
      },

      clearHistory: (userId) => {
        const empty = emptyRecommendationSession(userId);
        set({
          ownerUserId: empty.ownerUserId,
          sessionSearches: empty.sessionSearches,
          viewedProductIds: empty.viewedProductIds,
          activeRecommendations: empty.activeRecommendations,
          recommendationReason: empty.recommendationReason,
          recommendationType: empty.recommendationType,
        });
      },
    }),
    {
      name: "minishop-recommendations",
      version: 2,
      // Recommendations are fetched again per identity; do not hydrate another session's cards into ChatBot.
      partialize: (state) => ({
        ownerUserId: state.ownerUserId,
        sessionSearches: state.sessionSearches,
        viewedProductIds: state.viewedProductIds,
      }),
      // v1 persisted cards without an owner, so discard them instead of guessing whose they were.
      migrate: () => emptyRecommendationSession(null),
    },
  ),
);
