import { Link } from "react-router-dom";
import { Sparkles, Bot, ArrowRight, RefreshCw, Eye } from "lucide-react";
import type { Product } from "../../types";
import { ProductCard } from "../ProductCard";
import { useRecommendationStore } from "../../store/recommendation";

interface RecommendationSectionProps {
  products: Product[];
  reason: string;
  type: "personalized" | "search_intent" | "collaborative" | "trending";
  onRefresh?: () => void;
  similarToLastViewed?: {
    sourceProduct: Product;
    similarItems: Product[];
  } | null;
}

export function RecommendationSection({
  products,
  reason,
  type,
  onRefresh,
  similarToLastViewed,
}: RecommendationSectionProps) {
  const { sessionSearches, clearHistory } = useRecommendationStore();

  if (products.length === 0 && (!similarToLastViewed || similarToLastViewed.similarItems.length === 0)) {
    return null;
  }

  const handleAskBot = (productName: string) => {
    // Kích hoạt sự kiện tùy biến để Chatbot tự động mở và hỏi về sản phẩm này
    const event = new CustomEvent("minishop:ask-bot", {
      detail: { query: `Tư vấn cho tôi về sản phẩm "${productName}", tại sao shop lại gợi ý món này?` },
    });
    window.dispatchEvent(event);
  };

  return (
    <div className="space-y-8 animate-fade-in">
      {/* 1. KHỐI GỢI Ý CHÍNH: DÀNH RIÊNG CHO BẠN (NETFLIX STYLE) */}
      {products.length > 0 && (
        <section className="rounded-3xl p-5 sm:p-6 bg-gradient-to-br from-indigo-50/80 via-white to-sky-50/50 dark:from-slate-900/90 dark:via-slate-900/60 dark:to-indigo-950/40 border border-indigo-100/80 dark:border-indigo-900/40 shadow-sm relative overflow-hidden">
          {/* Background decorative glow */}
          <div className="absolute top-0 right-0 -mr-16 -mt-16 w-64 h-64 rounded-full bg-gradient-to-br from-indigo-400/10 to-sky-400/10 blur-3xl pointer-events-none" />

          {/* Header section */}
          <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-3 mb-6 relative z-10">
            <div>
              <div className="flex items-center gap-2">
                <span className="grid place-items-center w-8 h-8 rounded-xl bg-gradient-to-tr from-indigo-600 to-sky-500 text-white shadow-md shadow-indigo-500/20">
                  <Sparkles className="w-4 h-4" />
                </span>
                <h2 className="text-xl font-bold tracking-tight text-slate-900 dark:text-white flex items-center gap-2">
                  Gợi ý dành riêng cho bạn
                  <span className="text-[11px] font-semibold uppercase tracking-wider px-2 py-0.5 rounded-full bg-indigo-100 text-indigo-700 dark:bg-indigo-950 dark:text-indigo-300 border border-indigo-200 dark:border-indigo-800">
                    {type === "personalized" ? "Cá nhân hóa" : type === "search_intent" ? "Theo tìm kiếm" : "AI Match"}
                  </span>
                </h2>
              </div>

              {/* Dynamic Reason Badge */}
              <p className="text-xs text-slate-600 dark:text-slate-300 mt-1 flex items-center gap-1.5">
                <span className="inline-block w-2 h-2 rounded-full bg-emerald-500 animate-pulse" />
                <span className="font-medium text-indigo-600 dark:text-indigo-400">Lý do gợi ý:</span>
                <span className="italic">{reason}</span>
              </p>
            </div>

            {/* Action buttons */}
            <div className="flex items-center gap-2 shrink-0">
              {onRefresh && (
                <button
                  onClick={onRefresh}
                  title="Cập nhật lại gợi ý"
                  className="px-3 py-1.5 text-xs rounded-xl bg-white/80 dark:bg-slate-800/80 hover:bg-white dark:hover:bg-slate-800 text-slate-700 dark:text-slate-200 border border-slate-200 dark:border-slate-700 transition flex items-center gap-1.5 shadow-xs"
                >
                  <RefreshCw className="w-3.5 h-3.5" />
                  Làm mới
                </button>
              )}
              {sessionSearches.length > 0 && (
                <button
                  onClick={clearHistory}
                  title="Xóa lịch sử tìm kiếm cá nhân hóa"
                  className="px-2.5 py-1.5 text-xs rounded-xl text-slate-400 hover:text-rose-500 transition"
                >
                  Đặt lại
                </button>
              )}
              <Link
                to="/products"
                className="text-xs text-indigo-600 dark:text-indigo-400 hover:underline inline-flex items-center gap-1 font-medium ml-1"
              >
                Tất cả <ArrowRight className="w-3.5 h-3.5" />
              </Link>
            </div>
          </div>

          {/* Grid sản phẩm gợi ý */}
          <div className="grid grid-cols-2 md:grid-cols-3 lg:grid-cols-4 xl:grid-cols-4 gap-4 relative z-10">
            {products.slice(0, 8).map((p) => (
              <div key={p.id} className="relative group/rec flex flex-col">
                <ProductCard product={p} />
                
                {/* Nút hành động nhanh: Hỏi Chatbot về sản phẩm này */}
                <button
                  onClick={() => handleAskBot(p.name)}
                  className="mt-1.5 w-full py-1 px-2 text-[11px] font-medium rounded-lg bg-indigo-50 hover:bg-indigo-100 text-indigo-700 dark:bg-indigo-950/60 dark:hover:bg-indigo-900/80 dark:text-indigo-300 border border-indigo-200/60 dark:border-indigo-800/60 flex items-center justify-center gap-1.5 transition active:scale-98"
                >
                  <Bot className="w-3 h-3 text-indigo-600 dark:text-indigo-400" />
                  Hỏi AI Chatbot về món này
                </button>
              </div>
            ))}
          </div>
        </section>
      )}

      {/* 2. KHỐI GỢI Ý THỨ HAI: VÌ BẠN VỪA XEM [TÊN MÓN] (ITEM-TO-ITEM) */}
      {similarToLastViewed && similarToLastViewed.similarItems.length > 0 && (
        <section className="surface rounded-2xl p-5 border border-slate-200/80 dark:border-slate-800">
          <div className="flex items-center justify-between mb-4">
            <div className="flex items-center gap-2">
              <span className="grid place-items-center w-7 h-7 rounded-lg bg-slate-100 dark:bg-slate-800 text-slate-700 dark:text-slate-300">
                <Eye className="w-4 h-4" />
              </span>
              <div>
                <h3 className="text-base font-bold text-slate-900 dark:text-white">
                  Tương tự món bạn vừa xem:{" "}
                  <span className="text-brand-600 dark:text-brand-400 font-semibold">
                    "{similarToLastViewed.sourceProduct.name}"
                  </span>
                </h3>
                <p className="text-xs muted">Gợi ý sản phẩm cùng nhóm tính năng và tầm giá tương đương</p>
              </div>
            </div>
          </div>

          <div className="grid grid-cols-2 md:grid-cols-3 lg:grid-cols-4 xl:grid-cols-5 gap-4">
            {similarToLastViewed.similarItems.map((p) => (
              <ProductCard key={p.id} product={p} />
            ))}
          </div>
        </section>
      )}
    </div>
  );
}
