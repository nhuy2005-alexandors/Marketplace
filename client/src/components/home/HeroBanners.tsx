import { Link } from "react-router-dom";
import { ArrowRight } from "lucide-react";
import type { Product } from "../../types";

function HeroBanner({ product, variant }: { product?: Product; variant: "primary" | "sale" }) {
  const primary = variant === "primary";
  return (
    <Link
      to={product ? `/products/${product.id}` : "/products"}
      className={`group relative overflow-hidden rounded-3xl p-6 md:p-8 flex items-center min-h-[220px] transition-all hover:shadow-card ${
        primary
          ? "bg-gradient-to-br from-brand-600 to-brand-800 text-white md:col-span-2"
          : "bg-gradient-to-br from-amber-400 to-orange-500 text-white"
      }`}
    >
      <div className="relative z-10 max-w-[60%]">
        {primary ? (
          <>
            <div className="text-xs font-medium opacity-80 mb-1">Ưu đãi nổi bật</div>
            <div className="text-2xl md:text-3xl font-bold leading-tight">{product?.name ?? "Khám phá sản phẩm"}</div>
            {product && <div className="mt-1 text-lg font-semibold">Chỉ từ ${product.price.toFixed(2)}</div>}
            <span className="mt-4 inline-flex items-center gap-1.5 rounded-full bg-white text-brand-700 px-4 py-2 text-sm font-medium group-hover:gap-2.5 transition-all">
              Mua ngay <ArrowRight className="w-4 h-4" aria-hidden />
            </span>
          </>
        ) : (
          <>
            <div className="text-4xl md:text-5xl font-black leading-none">SALE</div>
            <div className="text-lg font-bold mt-1">Giảm đến 50%</div>
            <span className="mt-3 inline-flex items-center gap-1 text-sm font-medium underline underline-offset-2">
              Xem ngay <ArrowRight className="w-4 h-4" aria-hidden />
            </span>
          </>
        )}
      </div>
      {product?.imageUrl && (
        <img
          src={product.imageUrl}
          alt=""
          aria-hidden
          className="absolute right-0 top-1/2 -translate-y-1/2 h-[110%] w-1/2 object-cover object-center opacity-90 rounded-l-3xl group-hover:scale-105 transition-transform duration-500"
        />
      )}
    </Link>
  );
}

export function HeroBanners({ products }: { products: Product[] }) {
  return (
    <div className="grid md:grid-cols-3 gap-4">
      <HeroBanner product={products[0]} variant="primary" />
      <HeroBanner product={products[1]} variant="sale" />
    </div>
  );
}
