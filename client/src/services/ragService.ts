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
  evidenceStrength: "strong" | "weak";
  sources: { type: string; productId: number; chunkType: string; contentHash: string }[];
  conversationId?: string;
  intent?: "product_search" | "greeting" | "account_action" | "out_of_scope" | "blocked";
}

interface AssistantResponse {
  answer: string;
  products: Product[];
  sources: RagResult["sources"];
  retrieval: { mode: string; hasEvidence: boolean };
  evidenceStrength?: "strong" | "weak";
  conversationId?: string;
  intent?: RagResult["intent"];
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
    const intent = data.intent ?? "product_search";
    if (intent !== "product_search") {
      return {
        type: "general",
        message: data.answer,
        matchedProducts: [],
        similarProducts: [],
        recommendedProducts: [],
        confidence: 0,
        evidenceStrength: data.evidenceStrength ?? "weak",
        sources: data.sources || [],
        conversationId: data.conversationId,
        intent,
      };
    }
    const isRecommendation = data.retrieval?.mode === "recommendation";
    const hasEvidence = data.retrieval?.hasEvidence ?? products.length > 0;
    const evidenceStrength = data.evidenceStrength ?? (hasEvidence ? "strong" : "weak");
    return {
      type: isRecommendation ? "recommendation" : hasEvidence ? "matched" : "not_found",
      message: data.answer,
      matchedProducts: isRecommendation ? [] : products,
      similarProducts: [],
      recommendedProducts: isRecommendation ? products : [],
      confidence: hasEvidence ? (evidenceStrength === "strong" ? 0.9 : 0.45) : 0.2,
      evidenceStrength,
      sources: data.sources || [],
      conversationId: data.conversationId,
      intent,
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
      evidenceStrength: "weak",
      sources: [],
    };
  }
}
