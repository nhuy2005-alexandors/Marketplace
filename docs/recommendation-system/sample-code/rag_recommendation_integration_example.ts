/**
 * FILE THAM KHẢO (SAMPLE CODE): rag_recommendation_integration_example.ts
 * 
 * Đây là code mẫu minh họa cách mở rộng ragService.ts để kết hợp
 * hồ sơ mua sắm của khách hàng vào ChatBot RAG mà không làm mất logic cũ.
 */

import type { Product } from "../../../client/src/types";

// 1. Interface hồ sơ người dùng
export interface UserShoppingProfile {
  userId?: number;
  userName?: string;
  recentPurchasedProductIds: number[];
  recentPurchasedNames: string[];
  wishlistProductIds: number[];
  favoriteCategories: string[];
}

// 2. Interface kết quả trả về từ RAG có thêm Recommendation
export interface EnhancedRagResult {
  type: "matched" | "similar" | "recommendation" | "not_found" | "general";
  message: string;
  matchedProducts: Product[];
  recommendedProducts: Product[]; // Danh sách đề xuất cá nhân hóa
  confidence: number;
}

// 3. Hàm tính độ tương đồng Cosine cơ bản giữa 2 vector số thực
export function cosineSimilarity(vecA: number[], vecB: number[]): number {
  if (vecA.length !== vecB.length || vecA.length === 0) return 0;
  let dotProduct = 0;
  let normA = 0;
  let normB = 0;
  for (let i = 0; i < vecA.length; i++) {
    dotProduct += vecA[i] * vecB[i];
    normA += vecA[i] * vecA[i];
    normB += vecB[i] * vecB[i];
  }
  if (normA === 0 || normB === 0) return 0;
  return dotProduct / (Math.sqrt(normA) * Math.sqrt(normB));
}

// 4. Hàm trích xuất sản phẩm gợi ý dựa trên lịch sử mua (Rule-based & Content-based)
export function getRecommendedCandidates(
  allProducts: Product[],
  profile: UserShoppingProfile | null,
  limit = 4
): Product[] {
  if (!profile || allProducts.length === 0) {
    // Nếu chưa đăng nhập: Trả về sản phẩm tồn kho dồi dào, đánh giá tốt
    return allProducts
      .filter((p) => p.stock > 0)
      .slice(0, limit);
  }

  const purchasedSet = new Set(profile.recentPurchasedProductIds);
  const wishlistSet = new Set(profile.wishlistProductIds);
  const favCatSet = new Set(profile.favoriteCategories.map((c) => c.toLowerCase()));

  // Chấm điểm ưu tiên cho từng sản phẩm trong kho
  const scored = allProducts
    .filter((p) => !purchasedSet.has(p.id) && p.stock > 0) // Chưa mua và còn hàng
    .map((p) => {
      let score = 0;
      // Nếu nằm trong wishlist -> ưu tiên cao
      if (wishlistSet.has(p.id)) score += 50;
      // Nếu cùng danh mục yêu thích -> ưu tiên
      if (favCatSet.has(p.categoryName.toLowerCase())) score += 30;
      return { product: p, score };
    });

  scored.sort((a, b) => b.score - a.score);
  return scored.slice(0, limit).map((s) => s.product);
}

// 5. Hàm xây dựng Prompt gửi tới Gemini kết hợp Recommendation
export function buildPersonalizedPrompt(
  query: string,
  productsContext: string,
  profile: UserShoppingProfile | null
): string {
  const profileInfo = profile
    ? `
- Tên khách hàng: ${profile.userName || "Khách hàng thân thiết"}
- Các món đã từng mua gần đây: ${profile.recentPurchasedNames.join(", ") || "Chưa có"}
- Danh mục yêu thích: ${profile.favoriteCategories.join(", ") || "Đa dạng"}`
    : `
- Khách vãng lai (Chưa đăng nhập)`;

  return `
Bạn là Trợ lý Mua sắm Cá nhân Thông minh của sàn thương mại điện tử MiniShop.

THÔNG TIN KHÁCH HÀNG:
${profileInfo}

KHO SẢN PHẨM HIỆN CÓ TRÊN SÀN (Mã SP, Tên, Giá, Danh mục, Shop):
${productsContext}

CÂU HỎI HIỆN TẠI CỦA KHÁCH:
"${query}"

YÊU CẦU TRẢ LỜI:
1. Trả lời trực tiếp và ngắn gọn vào đúng nhu cầu của khách hàng.
2. Dựa vào các sản phẩm khách ĐÃ TỪNG MUA, hãy đưa ra 1-2 lời gợi ý tinh tế (phong cách Netflix: "Vì bạn đã có... nên shop đề xuất thêm...").
3. Chỉ đề xuất sản phẩm CÓ THẬT trong danh sách kho ở trên.
4. Giọng điệu thân thiện, nhiệt tình, có biểu tượng cảm xúc.
`;
}
