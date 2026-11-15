import type { Product } from "../types";

export type UserScope = number | "guest";

export function userScope(userId: number | null): UserScope {
  return userId ?? "guest";
}

export const privateQueryKeys = {
  cart: (userId: number | null) => ["cart", userScope(userId)] as const,
  orders: (userId: number | null, page?: number, pageSize?: number) =>
    ["orders", userScope(userId), ...(page === undefined ? [] : [page]), ...(pageSize === undefined ? [] : [pageSize])] as const,
  order: (userId: number | null, orderId: number) => ["order", userScope(userId), orderId] as const,
  orderSplit: (userId: number | null, orderId: number) => ["order-split", userScope(userId), orderId] as const,
  wishlist: (userId: number | null) => ["wishlist", userScope(userId)] as const,
  recommendations: (userId: number | null, limit = 8) => ["recommendations", userScope(userId), limit] as const,
  similarProducts: (userId: number | null, productId: number, limit: number) =>
    ["similar-products", userScope(userId), productId, limit] as const,
  me: (userId: number | null) => ["me", userScope(userId)] as const,
  sellerOrders: (userId: number | null, page: number, pageSize: number) =>
    ["seller-orders", userScope(userId), page, pageSize] as const,
  sellerDashboard: (userId: number | null) => ["seller-dashboard", userScope(userId)] as const,
};

export function privateQueryPrefixes(userId: number | null): readonly (readonly unknown[])[] {
  const scope = userScope(userId);
  return [
    ["cart", scope],
    ["orders", scope],
    ["order", scope],
    ["order-split", scope],
    ["wishlist", scope],
    ["recommendations", scope],
    ["similar-products", scope],
    ["me", scope],
    ["seller-orders", scope],
    ["seller-dashboard", scope],
  ];
}

export interface RecommendationSessionData {
  ownerUserId: number | null;
  sessionSearches: string[];
  viewedProductIds: number[];
  activeRecommendations: Product[];
  recommendationReason: string;
  recommendationType: "personalized" | "popular" | "top_rated" | "no_signal" | "empty_catalog";
}

export function emptyRecommendationSession(ownerUserId: number | null): RecommendationSessionData {
  return {
    ownerUserId,
    sessionSearches: [],
    viewedProductIds: [],
    activeRecommendations: [],
    recommendationReason: "",
    recommendationType: "no_signal",
  };
}

export function resetRecommendationSession(
  current: RecommendationSessionData,
  nextUserId: number | null,
): RecommendationSessionData {
  return current.ownerUserId === nextUserId ? current : emptyRecommendationSession(nextUserId);
}

export function strategyReason(strategy: RecommendationSessionData["recommendationType"]): string {
  switch (strategy) {
    case "personalized": return "Dựa trên lịch sử mua đủ điều kiện";
    case "popular": return "Phổ biến theo đơn hàng đủ điều kiện";
    case "top_rated": return "Được đánh giá cao theo đánh giá khách hàng";
    case "empty_catalog": return "Danh mục hiện chưa có sản phẩm";
    default: return "Chưa có đủ tín hiệu để gợi ý";
  }
}

export function strategyBadge(strategy: RecommendationSessionData["recommendationType"]): string {
  switch (strategy) {
    case "personalized": return "Cá nhân hóa";
    case "popular": return "Phổ biến";
    case "top_rated": return "Được đánh giá cao";
    case "empty_catalog":
    case "no_signal":
    default: return "Chưa có gợi ý";
  }
}
