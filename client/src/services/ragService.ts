import type { Product, Paged } from "../types";
import { api } from "../api/client";
import type { UserShoppingProfile } from "./recommendationService";

export interface RagResult {
  type: "matched" | "similar" | "recommendation" | "not_found" | "general";
  message: string;
  matchedProducts: Product[];
  similarProducts: Product[];
  recommendedProducts?: Product[];
  confidence: number;
}

const MAX_CONTEXT_PRODUCTS = 4;
const PAGE_SIZE = 100;

const STOP_WORDS = new Set([
  "toi", "minh", "ban", "cho", "voi", "la", "co", "khong", "nao", "gi", "nhe", "nha",
  "va", "cua", "mot", "cac", "nhung", "hay", "duoc", "can", "muon", "tim", "mua", "loai",
  "san", "pham", "hang", "shop", "gia", "re", "de", "duoi", "tren", "tam", "khoang",
  "the", "please", "find", "show", "me", "some", "a", "an", "the", "for", "with", "under",
  "over", "below", "above", "buy", "want", "looking",
]);

// Chuẩn hóa tiếng Việt có dấu/không dấu và tiếng Anh về cùng một dạng.
export function normalizeText(value: string): string {
  return (value || "")
    .toLowerCase()
    .normalize("NFD")
    .replace(/[\u0300-\u036f]/g, "")
    .replace(/đ/g, "d")
    .replace(/\btainghe\b/g, "tai nghe")
    .replace(/\bbanphim\b/g, "ban phim")
    .replace(/[^a-z0-9\s]/g, " ")
    .replace(/\s+/g, " ")
    .trim();
}

// Từ điển song ngữ nhỏ giúp truy xuất lexical hiểu các cách gọi phổ biến.
export const SYNONYM_MAP: Record<string, string[]> = {
  headphone: ["tai nghe", "tai nghe khong day", "headphone", "headphones", "earphone", "earphones", "chong on", "noise cancelling", "noise cancellation", "nghe nhac"],
  keyboard: ["ban phim", "keyboard", "ban phim co", "mechanical keyboard", "switch", "go phim", "keycap", "rgb"],
  watch: ["dong ho", "smart watch", "smartwatch", "dong ho thong minh", "the thao", "suc khoe", "dem buoc"],
  mouse: ["chuot", "mouse", "chuot gaming", "chuot khong day", "chuot may tinh"],
  computer: ["laptop", "may tinh", "pc", "macbook", "may tinh ban", "man hinh"],
  cleanCode: ["clean code", "robert martin", "uncle bob", "refactor"],
  pragmaticProgrammer: ["pragmatic programmer", "lap trinh thuc dung", "david thomas", "andrew hunt"],
  book: ["sach", "book", "sach lap trinh", "sach code", "giao trinh", "sach ky thuat"],
  tshirt: ["ao thun", "t shirt", "tshirt", "ao phong", "cotton", "ao ngan tay"],
  jacket: ["ao khoac", "jacket", "denim", "khoac jean", "ao am"],
  clothing: ["thoi trang", "quan ao", "outfit", "quan jean", "vay", "dam", "ao so mi", "hoodie", "ao len", "giay", "dep"],
};

function containsPhrase(text: string, phrase: string): boolean {
  if (!text || !phrase) return false;
  return ` ${text} `.includes(` ${phrase} `);
}

function getQueryTerms(queryNorm: string): string[] {
  const terms = queryNorm.split(" ").filter((term) =>
    term.length > 1 && !/^\d/.test(term) && !STOP_WORDS.has(term) &&
    !(term === "phim" && !containsPhrase(queryNorm, "ban phim")) &&
    !(term === "tai" && !containsPhrase(queryNorm, "tai nghe"))
  );
  const expanded = new Set(terms);

  for (const aliases of Object.values(SYNONYM_MAP)) {
    if (aliases.some((alias) => containsPhrase(queryNorm, normalizeText(alias)))) {
      for (const alias of aliases) {
        for (const term of normalizeText(alias).split(" ")) {
          if (term.length > 1 && !STOP_WORDS.has(term)) expanded.add(term);
        }
      }
    }
  }
  return [...expanded];
}

function detectIntendedCategories(queryNorm: string, products: Product[]): string[] {
  const requested = new Set<string>();
  for (const product of products) {
    const category = normalizeText(product.categoryName);
    if (category && containsPhrase(queryNorm, category)) requested.add(category);
  }

  const aliasesByCategory: Record<string, string[]> = {
    electronics: ["dien tu", "cong nghe", "tai nghe", "ban phim", "dong ho", "chuot", "laptop", "may tinh", "sac", "loa"],
    books: ["sach", "book", "doc sach", "lap trinh", "tac gia", "giao trinh"],
    fashion: ["thoi trang", "quan ao", "ao dai", "ao thun", "ao khoac", "quan jean", "giay", "vay"],
  };
  for (const [category, aliases] of Object.entries(aliasesByCategory)) {
    if (aliases.some((alias) => containsPhrase(queryNorm, normalizeText(alias)))) requested.add(category);
  }

  return [...requested];
}

interface PriceConstraint {
  min?: number;
  max?: number;
}

function parseAmount(raw: string): number {
  const normalized = raw.toLowerCase().normalize("NFD").replace(/[\u0300-\u036f]/g, "").replace(/đ/g, "d");
  const suffix = normalized.match(/\s*(k|nghin|ngan|trieu|million|m)\b/)?.[1];
  const numberText = normalized.match(/\d[\d.,]*/)?.[0] || "0";
  let amount: number;
  if (/^[\d]+(?:[.,]\d{3})+$/.test(numberText)) {
    amount = Number(numberText.replace(/[.,]/g, ""));
  } else {
    amount = Number(numberText.replace(/,(?=\d{3}$)/, "").replace(",", "."));
  }

  if (["k", "nghin", "ngan"].includes(suffix || "")) amount *= 1000;
  if (["trieu", "million", "m"].includes(suffix || "")) amount *= 1_000_000;
  const isVnd = /(?:vnd|dong|(?<![a-z])d)\s*$/.test(normalized) || (suffix && !normalized.includes("$"));
  return isVnd ? amount / 25_000 : amount;
}

function extractPriceConstraint(rawQuery: string): PriceConstraint {
  const query = rawQuery.toLowerCase().normalize("NFD").replace(/[\u0300-\u036f]/g, "").replace(/đ/g, "d");
  const amount = String.raw`(\$?\s*\d[\d.,]*(?:\s*(?:k|nghin|ngan|trieu|million|m))?\s*(?:usd|vnd|dong|(?<![a-z])d(?=\s|$))?)`;
  const result: PriceConstraint = {};

  const range = query.match(new RegExp(`(?:tu|from)\\s*${amount}\\s*(?:den|toi|to|-)\\s*${amount}`));
  if (range) {
    result.min = parseAmount(range[1]);
    result.max = parseAmount(range[2]);
    return result;
  }

  const under = query.match(new RegExp(`(?:duoi|nho hon|it hon|under|below|<)\\s*${amount}`));
  if (under) result.max = parseAmount(under[1]);
  const over = query.match(new RegExp(`(?:tren|lon hon|above|over|>)\\s*${amount}`));
  if (over) result.min = parseAmount(over[1]);

  const around = query.match(new RegExp(`(?:tam|khoang|around|about)\\s*${amount}`));
  if (around) {
    const target = parseAmount(around[1]);
    result.min = target * 0.8;
    result.max = target * 1.2;
  }
  return result;
}

function scoreProduct(product: Product, terms: string[], queryNorm: string, categories: string[]) {
  const name = normalizeText(product.name);
  const description = normalizeText(product.description || "");
  const category = normalizeText(product.categoryName);
  const shop = normalizeText(product.sellerShopName || "");
  let score = 0;

  if (name && containsPhrase(name, queryNorm)) score += 30;
  for (const term of terms) {
    let termScore = 0;
    if (containsPhrase(name, term)) termScore = 12;
    else if (containsPhrase(category, term)) termScore = 9;
    else if (containsPhrase(description, term)) termScore = 5;
    else if (containsPhrase(shop, term)) termScore = 2;
    if (termScore > 0) {
      score += termScore;
    }
  }

  if (categories.includes(category)) score += 8;
  if (product.stock <= 0) score *= 0.6;

  return score;
}

// Đọc đủ các trang để chatbot không bỏ sót sản phẩm ngoài 100 kết quả đầu.
export async function fetchMarketProducts(): Promise<Product[]> {
  const first = await api.get<Paged<Product>>("/products", { params: { page: 1, pageSize: PAGE_SIZE } });
  const pageCount = Math.max(1, first.data.totalPages || 1);
  const products = [...(first.data.items || [])];

  for (let page = 2; page <= pageCount; page += 1) {
    const response = await api.get<Paged<Product>>("/products", { params: { page, pageSize: PAGE_SIZE } });
    products.push(...(response.data.items || []));
  }
  return products;
}

export async function executeRagQuery(
  userQuery: string,
  products: Product[],
  userProfile?: UserShoppingProfile | null,
  activeRecommendations?: Product[]
): Promise<RagResult> {
  const queryNorm = normalizeText(userQuery);
  const greetings = new Set(["xin chao", "chao", "chao ban", "hello", "hi", "hi bot", "tro giup", "menu", "gioi thieu"]);
  const purchasedIds = new Set(userProfile?.purchasedProductIds || []);
  const recommendations = (activeRecommendations || []).filter((p) => p.stock > 0 && !purchasedIds.has(p.id));

  if (!queryNorm) {
    return {
      type: "general", confidence: 1, matchedProducts: [], similarProducts: [], recommendedProducts: [],
      message: "Mình chưa nhận được câu hỏi. Bạn hãy nhập tên sản phẩm hoặc nhu cầu mua sắm nhé.",
    };
  }

  if (greetings.has(queryNorm) || queryNorm.startsWith("chao shop ") || queryNorm.startsWith("chao ban ") || queryNorm.startsWith("chao oi")) {
    return {
      type: "general", confidence: 1, matchedProducts: [], similarProducts: [], recommendedProducts: [],
      message: "Xin chào! Mình là trợ lý ảo MiniShop, hỗ trợ tìm kiếm và gợi ý sản phẩm. Bạn đang muốn tìm món nào?",
    };
  }

  if ([
    "ban la ai", "ban la ai hay nguoi that", "ban la gi", "ai hay nguoi that", "ban la ai hay nguoi that",
    "who are you", "what are you", "what can you do", "what is your name", "who created you",
    "ten ban la gi", "ban ten gi", "ban lam gi", "ban lam duoc gi", "ban co the giup gi", "ban co the lam gi",
    "ban co phai ai khong", "ban la ai hay nguoi that",
  ]
    .some((phrase) => containsPhrase(queryNorm, phrase))) {
    return {
      type: "general", confidence: 1, matchedProducts: [], similarProducts: [], recommendedProducts: [],
      message: "Mình là trợ lý ảo của MiniShop, chuyên hỗ trợ tìm kiếm và gợi ý sản phẩm theo danh mục, giá và tình trạng còn hàng. Bạn hãy hỏi mình về một sản phẩm hoặc nhu cầu mua sắm nhé.",
    };
  }

  const price = extractPriceConstraint(userQuery);
  const categories = detectIntendedCategories(queryNorm, products);
  const terms = getQueryTerms(queryNorm);
  const shoppingIntent = [
    "san pham", "mat hang", "mua sam", "tim mua", "tim", "mua", "can mua", "muon mua", "can tim", "co ban", "dang ban", "con hang",
    "het hang", "gia bao nhieu", "price", "product", "products", "cheapest", "re nhat", "gia thap nhat",
  ].some((phrase) => containsPhrase(queryNorm, phrase));
  const genericRecommendation = [
    "goi y san pham", "de xuat san pham", "goi y mon phu hop theo so thich",
    "goi y phu hop voi so thich", "goi y cho toi", "nen mua gi", "what should i buy",
    "recommend products", "recommend me something nice", "recommend something", "goi y tren man hinh", "mon dang goi y tren man hinh",
    "san pham ban chay", "mon nao ban chay", "mon ban chay", "ban chay nhat", "best seller", "best selling",
  ].some((phrase) => containsPhrase(queryNorm, phrase));
  const unsupportedCommerceIntent = [
    "doi tra", "bao hanh", "freeship", "mien phi ship", "phi ship", "van chuyen", "ship ve", "don hang",
    "lich su mua hang", "thanh toan", "momo", "khuyen mai", "nguoi ban", "tro thanh nguoi ban", "huy don",
    "hotline", "so dien thoai", "shop o dau", "chinh sach", "tra hang", "hoan tien",
  ].some((phrase) => containsPhrase(queryNorm, phrase));
  const explicitNonProductIntent = [
    "viet code", "code python", "python sap xep", "sap xep mang", "ke chuyen cuoi", "cau chuyen cuoi",
    "dich giup", "translate", "write code", "write a poem", "bai tho", "thoi tiet", "tong thong",
    "ti so", "ty so", "gia vang", "an gi hom nay", "hop thu dien tu", "system prompt", "api key",
  ].some((phrase) => containsPhrase(queryNorm, phrase));
  const isCheapestIntent = ["re nhat", "gia thap nhat", "cheapest", "most affordable", "gia re nhat"]
    .some((phrase) => containsPhrase(queryNorm, phrase));
  const isBestSellerIntent = ["san pham ban chay", "mon nao ban chay", "mon ban chay", "ban chay nhat", "best seller", "best selling"]
    .some((phrase) => containsPhrase(queryNorm, phrase));
  const ranked = products
    .filter((product) => (price.min === undefined || product.price >= price.min) && (price.max === undefined || product.price <= price.max))
    .map((product) => ({ product, score: scoreProduct(product, terms, queryNorm, categories) }))
    .sort((a, b) => isCheapestIntent
      ? a.product.price - b.product.price
      : isBestSellerIntent
        ? b.product.reviewCount - a.product.reviewCount || b.product.averageRating - a.product.averageRating
        : b.score - a.score || b.product.averageRating - a.product.averageRating);
  const hasProductEvidence = ranked.some((item) => item.score >= 12);
  const recommendationIntent = genericRecommendation || (
    ["goi y", "de xuat", "nen mua", "tu van", "so thich", "man hinh", "ban chay"]
      .some((phrase) => containsPhrase(queryNorm, phrase)) && (shoppingIntent || categories.length > 0 || hasProductEvidence)
  );
  const hasCatalogCategory = categories.length > 0;
  const hasShoppingContext = shoppingIntent || hasCatalogCategory || hasProductEvidence;

  if (unsupportedCommerceIntent || explicitNonProductIntent) {
    return {
      type: "general", confidence: 1, matchedProducts: [], similarProducts: [], recommendedProducts: [],
      message: "Mình là trợ lý ảo MiniShop, chỉ hỗ trợ tìm kiếm và tư vấn sản phẩm trong catalog. Mình chưa thể xử lý chính sách, đơn hàng, vận chuyển, thanh toán hoặc yêu cầu ngoài phạm vi; bạn hãy hỏi về tên sản phẩm, danh mục, giá hay tồn kho nhé.",
    };
  }

  // Không biến câu hỏi đời sống/ngoài catalog thành gợi ý sản phẩm mặc định.
  if (!hasShoppingContext && !genericRecommendation) {
    return {
      type: "general", confidence: 1, matchedProducts: [], similarProducts: [], recommendedProducts: [],
      message: "Mình là trợ lý ảo MiniShop, chỉ hỗ trợ tìm kiếm và tư vấn sản phẩm trên cửa hàng. Câu hỏi này nằm ngoài phạm vi của mình; bạn hãy hỏi về sản phẩm, danh mục, giá hoặc tồn kho nhé.",
    };
  }

  if (recommendationIntent) {
    const hasConstraints = price.min !== undefined || price.max !== undefined || categories.length > 0;
    const recommendationPool = hasConstraints
      ? products.filter((p) => p.stock > 0 && !purchasedIds.has(p.id))
      : recommendations.length ? recommendations : products.filter((p) => p.stock > 0 && !purchasedIds.has(p.id));
    const filteredPool = recommendationPool.filter((product) =>
      (price.min === undefined || product.price >= price.min) &&
      (price.max === undefined || product.price <= price.max) &&
      (!categories.length || categories.includes(normalizeText(product.categoryName)))
    );
    const candidates = (filteredPool.length ? filteredPool : hasConstraints ? [] : recommendationPool)
      .map((product) => ({ product, score: scoreProduct(product, terms, queryNorm, categories) }))
      .sort((a, b) => isBestSellerIntent
        ? b.product.reviewCount - a.product.reviewCount || b.product.averageRating - a.product.averageRating
        : b.score - a.score || b.product.averageRating - a.product.averageRating)
      .slice(0, MAX_CONTEXT_PRODUCTS)
      .map((item) => item.product);
    const generated = candidates.length
      ? await generateGroundedAnswer(userQuery, candidates, userProfile).catch((error) => {
          console.warn("Gemini call failed; using grounded local response:", error);
          return "";
        })
      : "";
    const message = generated || (candidates.length
      ? buildRecommendationMessage(candidates, userProfile, isBestSellerIntent)
      : "Chưa có sản phẩm còn hàng khớp danh mục hoặc ngân sách bạn vừa chọn.");
    return {
      type: "recommendation", confidence: candidates.length ? 0.82 : 0.2,
      matchedProducts: [], similarProducts: [], recommendedProducts: candidates.slice(0, 4), message,
    };
  }

  if (containsPhrase(queryNorm, "het hang")) {
    const scopedOutOfStock = ranked.filter((item) => item.product.stock <= 0 && item.score >= 5).map((item) => item.product);
    const outOfStock = (scopedOutOfStock.length ? scopedOutOfStock : categories.length
      ? products.filter((product) => product.stock <= 0 && categories.includes(normalizeText(product.categoryName)))
      : products.filter((product) => product.stock <= 0)).slice(0, MAX_CONTEXT_PRODUCTS);
    if (!outOfStock.length) {
      return { type: "not_found", confidence: 0.9, matchedProducts: [], similarProducts: [], message: "Hiện không có sản phẩm nào trong catalog đang hết hàng." };
    }
    return {
      type: "matched", confidence: 0.9, matchedProducts: outOfStock, similarProducts: [],
      message: buildProductMessage("Các sản phẩm đang hết hàng:", outOfStock),
    };
  }

  if (isCheapestIntent && !ranked.some((item) => item.score >= 12)) {
    const cheapest = ranked.filter((item) => item.product.stock > 0).slice(0, MAX_CONTEXT_PRODUCTS).map((item) => item.product);
    if (cheapest.length) {
      return { type: "matched", confidence: 0.7, matchedProducts: cheapest, similarProducts: [], message: buildProductMessage("Các sản phẩm còn hàng có giá thấp nhất:", cheapest) };
    }
  }

  const matched = ranked.filter((item) => item.score >= 12).slice(0, MAX_CONTEXT_PRODUCTS).map((item) => item.product);
  let similar: Product[] = [];
  if (!matched.length) {
    const related = ranked.filter((item) => item.score >= 5 && item.product.stock > 0);
    if (related.length) similar = related.slice(0, 3).map((item) => item.product);
    else if (categories.length) {
      similar = products
        .filter((p) => categories.includes(normalizeText(p.categoryName)) && p.stock > 0 &&
          (price.min === undefined || p.price >= price.min) && (price.max === undefined || p.price <= price.max))
        .slice(0, 3);
    }
  }

  const evidence = matched.length ? matched : similar;
  if (evidence.length) {
    try {
      const generated = await generateGroundedAnswer(userQuery, evidence, userProfile);
      if (generated) {
        return {
          type: matched.length ? "matched" : "similar",
          confidence: matched.length ? 0.86 : 0.62,
          matchedProducts: matched,
          similarProducts: similar,
          message: generated,
        };
      }
    } catch (error) {
      console.warn("Gemini call failed; using grounded local response:", error);
    }
  }

  if (matched.length) {
    return {
      type: "matched", confidence: 0.82, matchedProducts: matched, similarProducts: [],
      message: buildProductMessage("Mình tìm thấy các sản phẩm khớp nhất:", matched),
    };
  }
  if (similar.length) {
    return {
      type: "similar", confidence: 0.58, matchedProducts: [], similarProducts: similar,
      message: buildProductMessage("Chưa thấy kết quả khớp hoàn toàn. Đây là một số lựa chọn gần với nhu cầu của bạn:", similar),
    };
  }

  const availableCategories = [...new Set(products.map((p) => p.categoryName).filter(Boolean))];
  const constraintText = price.max !== undefined ? ` trong ngân sách dưới $${price.max}` : price.min !== undefined ? ` từ $${price.min} trở lên` : "";
  return {
    type: "not_found", confidence: products.length ? 0.75 : 0.1, matchedProducts: [], similarProducts: [],
    message: products.length
      ? `Mình chưa tìm thấy sản phẩm phù hợp với “${userQuery}”${constraintText}. Danh mục hiện có: ${availableCategories.join(", ") || "chưa có dữ liệu"}. Bạn thử đổi từ khóa hoặc ngân sách nhé.`
      : "Mình chưa tải được danh sách sản phẩm để tìm kiếm. Bạn thử lại sau nhé.",
  };
}

function buildProductMessage(intro: string, products: Product[]): string {
  const details = products.map((p) =>
    `• **${p.name}** — $${p.price.toFixed(2)} | ${p.categoryName} | ${p.stock > 0 ? `còn ${p.stock}` : "hết hàng"} | ${p.averageRating.toFixed(1)}⭐ (${p.reviewCount} đánh giá)\n  ${p.description || "Chưa có mô tả."}`
  ).join("\n\n");
  return `${intro}\n\n${details}\n\nGiá, tồn kho và đánh giá được lấy từ dữ liệu sản phẩm hiện có.`;
}

function buildRecommendationMessage(products: Product[], profile?: UserShoppingProfile | null, bestSellerIntent = false): string {
  if (!products.length) return "Mình chưa tìm được gợi ý mới phù hợp trong catalog.";
  const reason = bestSellerIntent
    ? "Mình chưa có dữ liệu số lượng bán. Các sản phẩm dưới đây được xếp theo lượt đánh giá và điểm rating hiện có."
    : profile?.favoriteCategories?.length
    ? `Mình ưu tiên các sản phẩm thuộc nhóm bạn thường quan tâm (${profile.favoriteCategories.slice(0, 3).join(", ")}).`
    : "Mình ưu tiên sản phẩm còn hàng và đang được hệ thống gợi ý.";
  return `${reason}\n\n${buildProductMessage("Bạn có thể tham khảo:", products)}`;
}

async function generateGroundedAnswer(
  userQuery: string,
  retrievedProducts: Product[],
  userProfile?: UserShoppingProfile | null
): Promise<string> {
  const response = await api.post<{ answer: string }>("/assistant/chat", {
    query: userQuery.slice(0, 1000),
    productIds: retrievedProducts.slice(0, MAX_CONTEXT_PRODUCTS).map((p) => p.id),
    preferredCategories: userProfile?.favoriteCategories?.slice(0, 3) || [],
  });
  return response.data.answer?.trim() || "";
}
