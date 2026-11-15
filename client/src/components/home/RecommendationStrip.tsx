import { Sparkles } from "lucide-react";
import { usePersonalizedRecommendations } from "../../api/hooks";
import { ProductCard } from "../ProductCard";
import { RecommendationReasonLabels } from "../RecommendationReasonLabels";
import { Spinner } from "../ui";

export function RecommendationStrip() {
  const { data, isLoading, isError } = usePersonalizedRecommendations(8);

  if (isError) return null;

  return (
    <section aria-label="Gợi ý sản phẩm">
      <div className="flex items-center gap-2 mb-4">
        <Sparkles className="w-5 h-5 text-indigo-500" aria-hidden />
        <div>
          <h2 className="text-lg font-bold">Gợi ý dành cho bạn</h2>
          <p className="muted text-xs">
            {data?.strategy === "personalized"
              ? "Dựa trên lịch sử mua hàng và đặc điểm sản phẩm"
              : "Những sản phẩm nổi bật trong catalog"}
          </p>
        </div>
      </div>

      {isLoading ? (
        <Spinner />
      ) : !data?.items.length ? (
        <p className="muted text-sm">Chưa có sản phẩm phù hợp để gợi ý.</p>
      ) : (
        <div className="grid grid-cols-2 md:grid-cols-3 lg:grid-cols-4 xl:grid-cols-5 gap-4">
          {data.items.map((item) => (
            <div key={item.product.id} className="min-w-0">
              <ProductCard product={item.product} />
              <RecommendationReasonLabels reasons={item.reasons} />
            </div>
          ))}
        </div>
      )}
    </section>
  );
}
