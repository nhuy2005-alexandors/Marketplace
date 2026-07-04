import { Link } from "react-router-dom";
import { ArrowRight, type LucideIcon } from "lucide-react";
import type { Product } from "../../types";
import { ProductCard } from "../ProductCard";

export function ProductSection({
  title, icon: Icon, products, to = "/products",
}: { title: string; icon?: LucideIcon; products: Product[]; to?: string }) {
  if (products.length === 0) return null;
  return (
    <section>
      <div className="flex items-center justify-between mb-4">
        <div className="flex items-center gap-2">
          {Icon && <Icon className="w-5 h-5 text-brand-600 dark:text-brand-400" aria-hidden />}
          <h2 className="text-lg font-bold">{title}</h2>
        </div>
        <Link to={to} className="text-sm text-brand-600 dark:text-brand-400 hover:underline inline-flex items-center gap-1">
          Xem tất cả <ArrowRight className="w-4 h-4" aria-hidden />
        </Link>
      </div>
      <div className="grid grid-cols-2 md:grid-cols-3 lg:grid-cols-4 xl:grid-cols-5 gap-4">
        {products.map((p) => <ProductCard key={p.id} product={p} />)}
      </div>
    </section>
  );
}

function LowStockCard({ product }: { product: Product }) {
  return (
    <Link to={`/products/${product.id}`} className="group shrink-0 w-40">
      <div className="relative aspect-square rounded-xl overflow-hidden bg-slate-100 dark:bg-slate-800">
        {product.imageUrl && (
          <img src={product.imageUrl} alt={product.name} className="w-full h-full object-cover group-hover:scale-105 transition-transform duration-300" />
        )}
        <span className="absolute bottom-1.5 left-1.5 right-1.5 rounded-md bg-amber-500/95 text-white text-[11px] font-semibold px-1.5 py-0.5 text-center">
          Chỉ còn {product.stock}
        </span>
      </div>
      <div className="mt-1.5 text-sm line-clamp-1">{product.name}</div>
      <div className="font-bold">${product.price.toFixed(2)}</div>
    </Link>
  );
}

export function LowStockSection({ title, icon: Icon, products }: { title: string; icon?: LucideIcon; products: Product[] }) {
  if (products.length === 0) return null;
  return (
    <section>
      <div className="flex items-center gap-2 mb-4">
        {Icon && <Icon className="w-5 h-5 text-amber-500" aria-hidden />}
        <h2 className="text-lg font-bold">{title}</h2>
      </div>
      <div className="flex gap-4 overflow-x-auto pb-1">
        {products.map((p) => <LowStockCard key={p.id} product={p} />)}
      </div>
    </section>
  );
}
