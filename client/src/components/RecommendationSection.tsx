import { Sparkles } from "lucide-react";
import type { RecommendationResponse } from "../types";
import { ProductCard } from "./ProductCard";

const STRATEGY_COPY: Record<string, { title: string; description: string }> = {
  personalized: {
    title: "Gợi ý dành riêng cho bạn",
    description: "Dựa trên lịch sử mua hàng và đặc điểm sản phẩm.",
  },
  popular: {
    title: "Sản phẩm phổ biến",
    description: "Dựa trên những đơn hàng đã thanh toán thành công.",
  },
  top_rated: {
    title: "Được đánh giá cao",
    description: "Các sản phẩm có đánh giá tốt trong catalog.",
  },
  catalog_fallback: {
    title: "Khám phá sản phẩm",
    description: "Gợi ý từ những sản phẩm hiện có trong catalog.",
  },
  similar: {
    title: "Sản phẩm tương tự",
    description: "Dựa trên danh mục, thương hiệu và thuộc tính sản phẩm.",
  },
};

export function RecommendationSection({
  data,
  title,
}: {
  data: RecommendationResponse;
  title?: string;
}) {
  if (!data.items.length) return null;

  const copy = STRATEGY_COPY[data.strategy] ?? {
    title: "Gợi ý sản phẩm",
    description: "Được chọn từ catalog hiện có.",
  };

  return (
    <section>
      <div className="flex items-center gap-2 mb-4">
        <Sparkles className="w-5 h-5 text-brand-600 dark:text-brand-400" aria-hidden />
        <div>
          <h2 className="text-lg font-bold">{title ?? copy.title}</h2>
          <p className="muted text-xs mt-0.5">{copy.description}</p>
        </div>
      </div>
      <div className="grid grid-cols-2 md:grid-cols-3 lg:grid-cols-4 xl:grid-cols-5 gap-4">
        {data.items.map((item) => (
          <div key={item.product.id} className="min-w-0">
            <ProductCard product={item.product} />
            {item.reasons.length > 0 && (
              <p className="muted text-xs mt-2 px-2 line-clamp-2">
                {item.reasons.slice(0, 2).map((reason) => reason.label).join(" · ")}
              </p>
            )}
          </div>
        ))}
      </div>
    </section>
  );
}
