import type { Product, Order } from "../types";
import { normalizeText, SYNONYM_MAP } from "./ragService";

export interface UserShoppingProfile {
  userId?: number;
  userName?: string;
  purchasedProductIds: number[];
  purchasedProductNames: string[];
  wishlistProductIds: number[];
  cartProductIds: number[];
  recentSearchQueries: string[];
  viewedProductIds: number[];
  favoriteCategories: string[];
}

export interface RecommendationResult {
  products: Product[];
  reason: string;
  type: "personalized" | "search_intent" | "collaborative" | "trending";
}

/**
 * Trích xuất các danh mục mà khách hàng hay tương tác nhất
 */
export function extractFavoriteCategories(
  allProducts: Product[],
  purchasedIds: number[],
  wishlistIds: number[],
  cartIds: number[]
): string[] {
  const categoryCounts: Record<string, number> = {};
  const productMap = new Map<number, Product>(allProducts.map((p) => [p.id, p]));

  const addScore = (id: number, weight: number) => {
    const p = productMap.get(id);
    if (p) {
      categoryCounts[p.categoryName] = (categoryCounts[p.categoryName] || 0) + weight;
    }
  };

  purchasedIds.forEach((id) => addScore(id, 5));
  cartIds.forEach((id) => addScore(id, 4));
  wishlistIds.forEach((id) => addScore(id, 3));

  return Object.entries(categoryCounts)
    .sort((a, b) => b[1] - a[1])
    .map(([cat]) => cat);
}

/**
 * Tính điểm tương đồng ngữ nghĩa giữa 1 sản phẩm và từ khóa tìm kiếm (Bi-Encoder style)
 */
function scoreSearchSemantic(product: Product, query: string): number {
  const qNorm = normalizeText(query);
  if (!qNorm) return 0;
  const tokens = qNorm.split(" ").filter((t) => t.length > 1);
  const nameNorm = normalizeText(product.name);
  const descNorm = normalizeText(product.description || "");
  const catNorm = normalizeText(product.categoryName);

  let score = 0;
  if (nameNorm.includes(qNorm)) score += 40;
  if (descNorm.includes(qNorm)) score += 20;
  if (catNorm.includes(qNorm)) score += 25;

  for (const token of tokens) {
    if (nameNorm.includes(token)) score += 10;
    if (descNorm.includes(token)) score += 5;
  }

  // So khớp từ điển đồng nghĩa (Synonyms)
  for (const [, synonyms] of Object.entries(SYNONYM_MAP)) {
    if (synonyms.some((s) => qNorm.includes(normalizeText(s)))) {
      if (synonyms.some((s) => nameNorm.includes(normalizeText(s)))) {
        score += 30;
      }
    }
  }

  return score;
}

/**
 * Thuật toán tính toán đề xuất cá nhân hóa kiểu Netflix (Multi-Signal Scoring)
 */
export function computePersonalizedRecommendations(
  allProducts: Product[],
  profile: UserShoppingProfile | null,
  limit = 8
): RecommendationResult {
  const inStockProducts = allProducts.filter((p) => p.stock > 0);
  if (inStockProducts.length === 0) {
    return {
      products: [],
      reason: "Kho hàng hiện đang cập nhật sản phẩm mới.",
      type: "trending",
    };
  }

  // Nếu là khách vãng lai hoặc chưa có bất kỳ tín hiệu nào
  if (
    !profile ||
    (profile.purchasedProductIds.length === 0 &&
      profile.wishlistProductIds.length === 0 &&
      profile.cartProductIds.length === 0 &&
      profile.recentSearchQueries.length === 0 &&
      profile.viewedProductIds.length === 0)
  ) {
    return computeTrendingRecommendations(inStockProducts, limit);
  }

  const purchasedSet = new Set(profile.purchasedProductIds);
  const wishlistSet = new Set(profile.wishlistProductIds);
  const cartSet = new Set(profile.cartProductIds);
  const viewedSet = new Set(profile.viewedProductIds);
  const favCats = new Set(profile.favoriteCategories.map((c) => c.toLowerCase()));

  // 1. Chấm điểm từng sản phẩm dựa trên trọng số đa tầng
  const scored = inStockProducts.map((product) => {
    let score = 0;
    const reasons: string[] = [];

    // Không gợi ý lại sản phẩm khách ĐÃ MUA (trừ khi là sản phẩm tiêu dùng nhanh)
    if (purchasedSet.has(product.id)) {
      score -= 100;
    }

    // Đã có trong Wishlist (Ưu tiên rất cao)
    if (wishlistSet.has(product.id)) {
      score += 45;
      reasons.push("Đang có trong danh sách yêu thích của bạn");
    }

    // Đang có trong Giỏ hàng nhưng chưa checkout
    if (cartSet.has(product.id)) {
      score += 40;
      reasons.push("Bạn đang quan tâm trong giỏ hàng");
    }

    // Vừa mới xem gần đây
    if (viewedSet.has(product.id)) {
      score += 25;
      reasons.push("Bạn vừa xem gần đây");
    }

    // Thuộc danh mục yêu thích từ các đơn hàng trước
    if (favCats.has(product.categoryName.toLowerCase())) {
      score += 30;
      reasons.push(`Phù hợp với sở thích danh mục ${product.categoryName}`);
    }

    // So khớp với các từ khóa tìm kiếm gần đây trong phiên
    if (profile.recentSearchQueries.length > 0) {
      let maxSearchScore = 0;
      let matchedQuery = "";
      for (const query of profile.recentSearchQueries.slice(-3)) {
        const s = scoreSearchSemantic(product, query);
        if (s > maxSearchScore) {
          maxSearchScore = s;
          matchedQuery = query;
        }
      }
      if (maxSearchScore > 0) {
        score += maxSearchScore * 0.8;
        reasons.push(`Tương đồng với từ khóa bạn vừa tìm "${matchedQuery}"`);
      }
    }

    // Điểm cộng theo rating trung bình (chất lượng sản phẩm)
    score += (product.averageRating || 4.0) * 4;

    return { product, score, reasons };
  });

  // Lọc các sản phẩm có điểm dương và sắp xếp giảm dần
  const validCandidates = scored
    .filter((s) => s.score > 15)
    .sort((a, b) => b.score - a.score);

  if (validCandidates.length > 0) {
    // Xác định lý do tổng thể nổi bật nhất
    let overallReason = "Gợi ý được cá nhân hóa dựa trên sở thích và hành vi của bạn";
    if (profile.recentSearchQueries.length > 0) {
      overallReason = `Dựa trên tìm kiếm gần đây "${profile.recentSearchQueries[profile.recentSearchQueries.length - 1]}" và lịch sử mua sắm của bạn`;
    } else if (profile.favoriteCategories.length > 0) {
      overallReason = `Dành riêng cho bạn dựa trên sở thích ngành hàng ${profile.favoriteCategories.slice(0, 2).join(", ")}`;
    } else if (profile.purchasedProductNames.length > 0) {
      overallReason = `Vì bạn đã mua "${profile.purchasedProductNames[profile.purchasedProductNames.length - 1]}"`;
    }

    return {
      products: validCandidates.slice(0, limit).map((c) => c.product),
      reason: overallReason,
      type: "personalized",
    };
  }

  // Fallback nếu điểm số không đủ cao
  return computeTrendingRecommendations(inStockProducts, limit);
}

/**
 * Gợi ý sản phẩm tương tự cho 1 sản phẩm cụ thể (Item-to-Item / "Because you bought/viewed X")
 */
export function computeSimilarProducts(
  targetProduct: Product,
  allProducts: Product[],
  limit = 5
): Product[] {
  const targetNameNorm = normalizeText(targetProduct.name);
  const targetTokens = targetNameNorm.split(" ").filter((t) => t.length > 1);

  return allProducts
    .filter((p) => p.id !== targetProduct.id && p.stock > 0)
    .map((p) => {
      let similarity = 0;

      // Cùng danh mục: +50 điểm
      if (p.categoryId === targetProduct.categoryId) {
        similarity += 50;
      }

      // Trùng shop bán: +10 điểm
      if (p.sellerId === targetProduct.sellerId) {
        similarity += 10;
      }

      // Tương đồng giá tiền (trong khoảng +/- 30%): +20 điểm
      const priceDiffRatio = Math.abs(p.price - targetProduct.price) / (targetProduct.price || 1);
      if (priceDiffRatio <= 0.3) {
        similarity += 20;
      } else if (priceDiffRatio <= 0.6) {
        similarity += 10;
      }

      // Trùng token tên sản phẩm
      const pNameNorm = normalizeText(p.name);
      for (const token of targetTokens) {
        if (pNameNorm.includes(token)) similarity += 15;
      }

      return { product: p, similarity };
    })
    .sort((a, b) => b.similarity - a.similarity)
    .slice(0, limit)
    .map((item) => item.product);
}

/**
 * Gợi ý xu hướng / bán chạy cho khách mới (Cold-Start)
 */
export function computeTrendingRecommendations(
  products: Product[],
  limit = 8
): RecommendationResult {
  const sorted = [...products]
    .filter((p) => p.stock > 0)
    .sort((a, b) => {
      // Ưu tiên rating và lượt review
      const scoreA = (a.averageRating || 0) * 10 + (a.reviewCount || 0);
      const scoreB = (b.averageRating || 0) * 10 + (b.reviewCount || 0);
      return scoreB - scoreA;
    });

  return {
    products: sorted.slice(0, limit),
    reason: "Sản phẩm nổi bật và được đánh giá cao nhất trên MiniShop",
    type: "trending",
  };
}

/**
 * Tạo UserShoppingProfile từ các entity của MiniShop
 */
export function buildUserProfileFromData(
  userId: number | undefined,
  userName: string | undefined,
  orders: Order[] = [],
  wishlistProductIds: number[] = [],
  cartProductIds: number[] = [],
  sessionSearches: string[] = [],
  viewedProductIds: number[] = [],
  allProducts: Product[] = []
): UserShoppingProfile {
  const purchasedIds: number[] = [];
  const purchasedNames: string[] = [];

  for (const order of orders) {
    if (order.items) {
      for (const item of order.items) {
        purchasedIds.push(item.productId);
        if (item.productName && !purchasedNames.includes(item.productName)) {
          purchasedNames.push(item.productName);
        }
      }
    }
  }

  const favoriteCategories = extractFavoriteCategories(
    allProducts,
    purchasedIds,
    wishlistProductIds,
    cartProductIds
  );

  return {
    userId,
    userName,
    purchasedProductIds: Array.from(new Set(purchasedIds)),
    purchasedProductNames: purchasedNames,
    wishlistProductIds,
    cartProductIds,
    recentSearchQueries: sessionSearches,
    viewedProductIds,
    favoriteCategories,
  };
}
