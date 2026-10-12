import type { Product } from "../types";
import { api } from "../api/client";

export interface RagResult {
  type: "matched" | "similar" | "not_found" | "general";
  message: string;
  matchedProducts: Product[];
  similarProducts: Product[];
  confidence: number;
}

// Hàm chuẩn hóa chuỗi tiếng Việt và tiếng Anh để so sánh không dấu
export function normalizeText(str: string): string {
  if (!str) return "";
  return str
    .toLowerCase()
    .normalize("NFD")
    .replace(/[\u0300-\u036f]/g, "")
    .replace(/đ/g, "d")
    .replace(/[^a-z0-9\s]/g, " ")
    .replace(/\s+/g, " ")
    .trim();
}

// Từ điển ngữ nghĩa & đồng nghĩa mở rộng cho RAG
const SYNONYM_MAP: Record<string, string[]> = {
  // Electronics
  headphone: ["tai nghe", "headphone", "headphones", "chong on", "earphone", "bluetooth", "am thanh", "nghe nhac"],
  keyboard: ["ban phim", "keyboard", "ban phim co", "switch", "go phim", "keycap", "rgb"],
  watch: ["dong ho", "smart watch", "smartwatch", "dong ho thong minh", "the thao", "suc khoe", "dem buoc"],
  mouse: ["chuot", "chuot gaming", "mouse", "chuot khong day", "chuot may tinh", "pad chuot"],
  computer: ["laptop", "may tinh", "pc", "macbook", "may tinh ban", "man hinh"],
  
  // Books
  cleancode: ["clean code", "robert martin", "ma sach", "code sach", "refactor"],
  pragmatic: ["pragmatic programmer", "pragmatic", "lap trinh thuc dung", "hunt", "thomas"],
  book: ["sach", "book", "sach lap trinh", "sach code", "doc sach", "giao trinh", "sach ky thuat"],
  other_book: ["sach python", "sach java", "sach toan", "truyen", "tieu thuyet", "sach kinh te"],

  // Fashion
  tshirt: ["ao thun", "t shirt", "tshirt", "ao phong", "cotton", "ao ngan tay"],
  jacket: ["ao khoac", "jacket", "denim", "ao bo", "khoac jean", "ao am"],
  clothing: ["thoi trang", "quan ao", "ao", "do mac", "outfit"],
  other_fashion: ["quan jean", "quan bo", "quan dai", "vay", "dam", "ao so mi", "hoodie", "ao len", "giay", "dep", "balo"]
};

// Phân loại danh mục dự đoán từ câu hỏi
function detectIntendedCategories(queryNorm: string): string[] {
  const categories: string[] = [];

  const electronicsKeywords = [
    "dien tu", "cong nghe", "tai nghe", "headphone", "ban phim", "keyboard",
    "dong ho", "smart watch", "chuot", "laptop", "may tinh", "sac", "loa", "usb"
  ];
  const bookKeywords = [
    "sach", "book", "doc", "lap trinh", "clean code", "pragmatic", "tac gia", "lap trinh vien"
  ];
  const fashionKeywords = [
    "thoi trang", "quan ao", "ao", "khoac", "thun", "jacket", "t shirt", "jean", "denim", "mac"
  ];

  if (electronicsKeywords.some((k) => queryNorm.includes(k))) categories.push("Electronics");
  if (bookKeywords.some((k) => queryNorm.includes(k))) categories.push("Books");
  if (fashionKeywords.some((k) => queryNorm.includes(k))) categories.push("Fashion");

  return categories;
}

// Trích xuất điều kiện giá từ câu hỏi (ví dụ: "dưới 50$", "dưới 100", "tầm 80")
function extractPriceConstraint(queryNorm: string): { maxPrice?: number; minPrice?: number } {
  const underMatch = queryNorm.match(/(duoi|nho hon|it hon|<)\s*(\d+(\.\d+)?)/);
  if (underMatch) {
    return { maxPrice: parseFloat(underMatch[2]) };
  }
  const overMatch = queryNorm.match(/(tren|lon hon|hon|>)\s*(\d+(\.\d+)?)/);
  if (overMatch) {
    return { minPrice: parseFloat(overMatch[2]) };
  }
  return {};
}

// Tính điểm tương đồng giữa câu hỏi và sản phẩm
function scoreProduct(
  p: Product,
  queryNorm: string,
  queryTokens: string[]
): { score: number; reasons: string[] } {
  let score = 0;
  const reasons: string[] = [];
  const nameNorm = normalizeText(p.name);
  const descNorm = normalizeText(p.description || "");
  const catNorm = normalizeText(p.categoryName);
  const shopNorm = normalizeText(p.sellerShopName);

  // 1. Khớp chính xác tên sản phẩm
  if (nameNorm === queryNorm) {
    score += 100;
    reasons.push("Khớp chính xác tên sản phẩm");
  } else if (nameNorm.includes(queryNorm) || queryNorm.includes(nameNorm)) {
    score += 60;
    reasons.push("Tên sản phẩm trùng khớp từ khóa");
  }

  // 2. So khớp từng token
  let tokenMatches = 0;
  for (const token of queryTokens) {
    if (token.length <= 1) continue;
    if (nameNorm.includes(token)) {
      tokenMatches++;
      score += 20;
    } else if (descNorm.includes(token)) {
      tokenMatches++;
      score += 10;
    } else if (catNorm.includes(token)) {
      tokenMatches++;
      score += 12;
    } else if (shopNorm.includes(token)) {
      score += 8;
    }
  }

  // 3. So khớp từ điển đồng nghĩa (Synonyms)
  for (const [key, synonyms] of Object.entries(SYNONYM_MAP)) {
    const hasQueryMatch = synonyms.some((syn) => queryNorm.includes(normalizeText(syn)));
    if (hasQueryMatch) {
      if (
        (key === "headphone" && nameNorm.includes("headphone")) ||
        (key === "keyboard" && nameNorm.includes("keyboard")) ||
        (key === "watch" && nameNorm.includes("watch")) ||
        (key === "cleancode" && nameNorm.includes("clean code")) ||
        (key === "pragmatic" && nameNorm.includes("pragmatic")) ||
        (key === "tshirt" && nameNorm.includes("t shirt")) ||
        (key === "jacket" && nameNorm.includes("jacket")) ||
        (key === "book" && catNorm.includes("book")) ||
        (key === "clothing" && catNorm.includes("fashion"))
      ) {
        score += 45;
        reasons.push(`Phù hợp với nhóm sản phẩm ${p.name}`);
      }
    }
  }

  return { score, reasons };
}

// Tải toàn bộ sản phẩm từ API
export async function fetchMarketProducts(): Promise<Product[]> {
  try {
    const res = await api.get<{ items: Product[] }>("/products", {
      params: { pageSize: 100 },
    });
    return res.data?.items || [];
  } catch (err) {
    console.error("Lỗi khi tải danh sách sản phẩm từ sàn:", err);
    return [];
  }
}

// BỘ XỬ LÝ RAG CHÍNH (RETRIEVAL-AUGMENTED GENERATION)
export async function executeRagQuery(
  userQuery: string,
  products: Product[],
  geminiApiKey?: string
): Promise<RagResult> {
  const queryNorm = normalizeText(userQuery);
  const queryTokens = queryNorm.split(" ").filter((t) => t.length > 1);

  // Câu chào hỏi thông thường
  const greetingPatterns = ["xin chao", "chao ban", "hello", "hi bot", "tro giup", "menu", "gioi thieu"];
  if (greetingPatterns.some((g) => queryNorm === g || queryNorm.startsWith(g + " "))) {
    return {
      type: "general",
      confidence: 1.0,
      matchedProducts: [],
      similarProducts: products.slice(0, 3),
      message:
        "Xin chào! Tôi là trợ lý mua sắm AI (RAG) của MiniShop. Tôi có thể đọc toàn bộ sản phẩm trên thị trường ảo này để:\n\n" +
        "✨ **Tìm và đề xuất chính xác** sản phẩm bạn đang cần.\n" +
        "💡 **Gợi ý sản phẩm tương tự** nếu mặt hàng bạn tìm chưa có mẫu chính xác.\n" +
        "📢 **Thông báo rõ ràng** nếu sản phẩm đó chưa có trên sàn.\n\n" +
        "Bạn đang muốn tìm mua món đồ gì hôm nay?",
    };
  }

  // 1. RETRIEVAL PHASE: Đánh giá và lọc sản phẩm
  const priceFilter = extractPriceConstraint(queryNorm);
  const intendedCategories = detectIntendedCategories(queryNorm);

  const scoredProducts = products.map((p) => {
    const { score, reasons } = scoreProduct(p, queryNorm, queryTokens);
    let finalScore = score;

    // Lọc theo giá nếu có
    if (priceFilter.maxPrice !== undefined) {
      if (p.price <= priceFilter.maxPrice) finalScore += 25;
      else finalScore -= 30;
    }
    if (priceFilter.minPrice !== undefined) {
      if (p.price >= priceFilter.minPrice) finalScore += 25;
      else finalScore -= 30;
    }

    // Lọc theo category
    if (intendedCategories.includes(p.categoryName)) {
      finalScore += 20;
    }

    return { product: p, score: finalScore, reasons };
  });

  scoredProducts.sort((a, b) => b.score - a.score);

  const matched = scoredProducts.filter((sp) => sp.score >= 40).map((sp) => sp.product);
  const partialMatches = scoredProducts
    .filter((sp) => sp.score >= 15 && sp.score < 40)
    .map((sp) => sp.product);

  // Nếu không có direct match nhưng xác định được danh mục người dùng quan tâm (ví dụ: tìm "chuột" -> danh mục Electronics)
  let similar: Product[] = [];
  if (matched.length === 0) {
    if (partialMatches.length > 0) {
      similar = partialMatches.slice(0, 3);
    } else if (intendedCategories.length > 0) {
      // Lấy các sản phẩm trong danh mục liên quan làm sản phẩm na ná
      similar = products
        .filter((p) => intendedCategories.includes(p.categoryName))
        .slice(0, 3);
    }
  }

  // Nếu có API key Gemini, gọi LLM với ngữ cảnh RAG (Context Augmentation)
  if (geminiApiKey?.trim()) {
    try {
      const llmResponse = await callGeminiWithRag(userQuery, products, matched, similar, geminiApiKey);
      if (llmResponse) {
        return {
          type: matched.length > 0 ? "matched" : similar.length > 0 ? "similar" : "not_found",
          confidence: matched.length > 0 ? 0.95 : 0.7,
          matchedProducts: matched,
          similarProducts: similar,
          message: llmResponse,
        };
      }
    } catch (e) {
      console.warn("Gemini API call failed, falling back to local RAG generator:", e);
    }
  }

  // 2. GENERATION PHASE (Local RAG Synthesizer): Tạo câu trả lời thông minh dựa trên kết quả truy xuất
  if (matched.length > 0) {
    // TRƯỜNG HỢP 1: CÓ SẢN PHẨM PHÙ HỢP -> Đề xuất & gợi ý
    let msg = `Tuyệt vời! Tôi đã tìm thấy **${matched.length} sản phẩm** rất phù hợp với nhu cầu của bạn trên sàn:\n\n`;
    for (const p of matched) {
      const stockInfo = p.stock > 0 ? `còn ${p.stock} sản phẩm` : "tạm hết hàng";
      msg += `🔹 **${p.name}** — Giá: **$${p.price.toFixed(2)}**\n`;
      msg += `   • *Mô tả:* ${p.description || "Chất lượng cao"}\n`;
      msg += `   • *Gian hàng:* ${p.sellerShopName} | *Đánh giá:* ${p.averageRating}⭐ (${p.reviewCount} lượt) | *Kho:* ${stockInfo}\n\n`;
    }
    msg += `👉 Bạn có thể xem chi tiết hoặc bấm thêm vào giỏ hàng ngay bên dưới nhé!`;

    return {
      type: "matched",
      confidence: 0.95,
      matchedProducts: matched,
      similarProducts: [],
      message: msg,
    };
  }

  if (similar.length > 0) {
    // TRƯỜNG HỢP 2: KHÔNG CÓ SẢN PHẨM CHÍNH XÁC -> Gợi ý sản phẩm na ná / cùng danh mục
    let msg = `Hiện tại trên thị trường ảo **chưa có chính xác sản phẩm "${userQuery}"** mà bạn tìm kiếm.\n\n`;
    msg += `💡 Tuy nhiên, dựa trên phân tích nhu cầu và danh mục liên quan, tôi xin **gợi ý các sản phẩm na ná / cùng loại** có thể bạn sẽ quan tâm:\n\n`;

    for (const p of similar) {
      msg += `🔸 **${p.name}** ($${p.price.toFixed(2)}) — Danh mục *${p.categoryName}*\n`;
      msg += `   • *Đặc điểm:* ${p.description || "Sản phẩm chất lượng cao"}\n`;
      msg += `   • *Gian hàng:* ${p.sellerShopName} (${p.stock > 0 ? `còn ${p.stock} cái` : "hết hàng"})\n\n`;
    }
    msg += `Bạn có muốn tham khảo các mẫu gợi ý này không?`;

    return {
      type: "similar",
      confidence: 0.75,
      matchedProducts: [],
      similarProducts: similar,
      message: msg,
    };
  }

  // TRƯỜNG HỢP 3: HOÀN TOÀN KHÔNG CÓ TRÊN THỊ TRƯỜNG
  const availableCategories = Array.from(new Set(products.map((p) => p.categoryName))).join(", ");
  const fallbackMessage =
    `Rất tiếc, hiện tại **trên thị trường chưa có sản phẩm "${userQuery}"** hoặc bất kỳ mặt hàng nào thuộc nhóm này.\n\n` +
    `🛒 Các nhóm sản phẩm hiện có sẵn trên thị trường ảo MiniShop gồm: **${availableCategories}**\n` +
    `• **Điện tử (Electronics):** Tai nghe không dây, Bàn phím cơ, Smart watch\n` +
    `• **Sách (Books):** Clean Code, The Pragmatic Programmer\n` +
    `• **Thời trang (Fashion):** Áo thun Cotton, Áo khoác Denim\n\n` +
    `Bạn có thể thử tìm kiếm theo các ngành hàng trên hoặc nhập từ khóa khác để tôi hỗ trợ nhé!`;

  return {
    type: "not_found",
    confidence: 0.9,
    matchedProducts: [],
    similarProducts: [],
    message: fallbackMessage,
  };
}

// Gọi LLM Gemini với Prompt RAG
async function callGeminiWithRag(
  userQuery: string,
  allProducts: Product[],
  _matched: Product[],
  _similar: Product[],
  apiKey: string
): Promise<string> {
  const contextData = allProducts.map((p) => ({
    id: p.id,
    name: p.name,
    category: p.categoryName,
    price: p.price,
    stock: p.stock,
    description: p.description,
    seller: p.sellerShopName,
    rating: p.averageRating,
  }));

  const systemPrompt = `
Bạn là AI Shopping Assistant thông minh của sàn thương mại điện tử MiniShop.
Dưới đây là TOÀN BỘ KHO DỮ LIỆU SẢN PHẨM hiện có trên thị trường ảo MiniShop (Ground Truth):
${JSON.stringify(contextData, null, 2)}

NHIỆM VỤ CỦA BẠN:
1. Đọc kỹ câu hỏi/nhu cầu của khách hàng: "${userQuery}"
2. Áp dụng quy tắc RAG:
   - NẾU SẢN PHẨM CÓ TRÊN THỊ TRƯỜNG: Đề xuất cụ thể tên sản phẩm, giá bán ($), ưu điểm, đánh giá và tình trạng kho.
   - NẾU KHÔNG CÓ CHÍNH XÁC NHƯNG CÓ SẢN PHẨM NA NÁ / CÙNG DANH MỤC: Thông báo rõ là sàn chưa có đúng mẫu đó, nhưng đề xuất ngay các sản phẩm tương tự có thể thay thế và nêu lý do tại sao nên chọn.
   - NẾU HOÀN TOÀN KHÔNG CÓ SẢN PHẨM NÀO PHÙ HỢP TRÊN SÀN: Thông báo lịch sự là trên thị trường hiện chưa có sản phẩm đó, và giới thiệu ngắn gọn các ngành hàng sàn đang có.
3. Phong cách trả lời: Thân thiện, lịch sự, chuyên nghiệp, dùng định dạng Markdown rõ ràng bằng tiếng Việt.
`.trim();

  const url = `https://generativelanguage.googleapis.com/v1beta/models/gemini-1.5-flash:generateContent?key=${apiKey}`;
  const response = await fetch(url, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({
      contents: [
        {
          parts: [{ text: systemPrompt }],
        },
      ],
      generationConfig: {
        temperature: 0.4,
        maxOutputTokens: 800,
      },
    }),
  });

  if (!response.ok) {
    throw new Error(`Gemini API error: ${response.statusText}`);
  }

  const json = await response.json();
  const candidate = json.candidates?.[0]?.content?.parts?.[0]?.text;
  return candidate || "";
}
