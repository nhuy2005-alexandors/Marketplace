import { create } from "zustand";
import { persist } from "zustand/middleware";
import type { Product } from "../types";

interface RecommendationState {
  sessionSearches: string[];
  viewedProductIds: number[];
  activeRecommendations: Product[];
  recommendationReason: string;
  recommendationType: "personalized" | "search_intent" | "collaborative" | "trending";

  trackSearch: (query: string) => void;
  trackView: (productId: number) => void;
  setRecommendations: (
    products: Product[],
    reason: string,
    type: "personalized" | "search_intent" | "collaborative" | "trending"
  ) => void;
  clearHistory: () => void;
}

export const useRecommendationStore = create<RecommendationState>()(
  persist(
    (set) => ({
      sessionSearches: [],
      viewedProductIds: [],
      activeRecommendations: [],
      recommendationReason: "Sản phẩm được gợi ý cho bạn",
      recommendationType: "trending",

      trackSearch: (query: string) => {
        const trimmed = query.trim();
        if (!trimmed) return;
        set((state) => {
          const filtered = state.sessionSearches.filter(
            (q) => q.toLowerCase() !== trimmed.toLowerCase()
          );
          // Lưu tối đa 8 tìm kiếm gần nhất
          return { sessionSearches: [...filtered, trimmed].slice(-8) };
        });
      },

      trackView: (productId: number) => {
        if (!productId) return;
        set((state) => {
          const filtered = state.viewedProductIds.filter((id) => id !== productId);
          // Lưu tối đa 10 sản phẩm xem gần nhất
          return { viewedProductIds: [...filtered, productId].slice(-10) };
        });
      },

      setRecommendations: (products, reason, type) => {
        set({
          activeRecommendations: products,
          recommendationReason: reason,
          recommendationType: type,
        });
      },

      clearHistory: () => {
        set({
          sessionSearches: [],
          viewedProductIds: [],
          activeRecommendations: [],
        });
      },
    }),
    {
      name: "minishop-recommendations",
    }
  )
);
