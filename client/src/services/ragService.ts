import type { Product } from "../types";
import { api } from "../api/client";
export { normalizeText, SYNONYM_MAP } from "./textNormalization";

export interface RagResult {
  type: "matched" | "similar" | "recommendation" | "not_found" | "general";
  message: string;
  matchedProducts: Product[];
  similarProducts: Product[];
  recommendedProducts?: Product[];
  confidence: number;
  sources: { type: string; productId: number; chunkType: string; contentHash: string }[];
  conversationId?: string;
}

interface AssistantResponse {
  answer: string;
  products: Product[];
  sources: RagResult["sources"];
  retrieval: { mode: string; hasEvidence: boolean };
  conversationId?: string;
}

export async function executeRagQuery(
  userQuery: string,
  recentTurns: { role: "user" | "assistant"; content: string }[] = [],
  conversationId?: string
): Promise<RagResult> {
  try {
    const { data } = await api.post<AssistantResponse>("/assistant/chat", {
      query: userQuery.slice(0, 1000),
      conversation: {
        conversationId,
        recentTurns: recentTurns.slice(-4).map((turn) => ({
          role: turn.role,
          content: turn.content.slice(0, 500),
        })),
      },
    });
    const products = data.products || [];
    const isRecommendation = data.retrieval?.mode === "recommendation";
    const hasEvidence = data.retrieval?.hasEvidence ?? products.length > 0;
    return {
      type: isRecommendation ? "recommendation" : hasEvidence ? "matched" : "not_found",
      message: data.answer,
      matchedProducts: isRecommendation ? [] : products,
      similarProducts: [],
      recommendedProducts: isRecommendation ? products : [],
      confidence: hasEvidence ? 0.9 : 0.2,
      sources: data.sources || [],
      conversationId: data.conversationId,
    };
  } catch {
    // Fail closed: never fall back to browser-side catalog ranking or synthesize product cards.
    return {
      type: "general",
      message: "Trợ lý đang tạm thời không khả dụng. Vui lòng thử lại sau.",
      matchedProducts: [],
      similarProducts: [],
      recommendedProducts: [],
      confidence: 0,
      sources: [],
    };
  }
}
