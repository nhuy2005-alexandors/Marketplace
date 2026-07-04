import { Link } from "react-router-dom";
import { ArrowRight, Flame, Headphones, PackageCheck, ShieldCheck, Shirt, ShoppingBag, Sparkles, Star, Tag, Truck } from "lucide-react";
import { useActiveCoupons, useCategories, useProducts } from "../api/hooks";
import { ProductCard } from "../components/ProductCard";
import { HeroBanners } from "../components/home/HeroBanners";
import { VoucherStrip } from "../components/home/VoucherStrip";
import { FlashSale } from "../components/home/FlashSale";
import { LowStockSection, ProductSection } from "../components/home/ProductSection";
import { Spinner } from "../components/ui";
import type { Category } from "../types";

const CATEGORY_ICONS = [ShoppingBag, Shirt, Sparkles, Headphones, Tag];

const PERKS = [
  { icon: Truck, title: "Miễn phí vận chuyển", desc: "Cho đơn từ $50" },
  { icon: ShieldCheck, title: "Thanh toán an toàn", desc: "Bảo mật 100%" },
  { icon: PackageCheck, title: "Đổi trả dễ dàng", desc: "Trong 7 ngày" },
];

function CategoryCircle({ category, index }: { category: Category; index: number }) {
  const Icon = CATEGORY_ICONS[index % CATEGORY_ICONS.length];
  return (
    <Link to={`/products?categoryId=${category.id}`} className="group flex flex-col items-center gap-2 shrink-0 w-24">
      <span className="grid place-items-center w-20 h-20 rounded-full bg-slate-100 dark:bg-slate-800 text-brand-600 dark:text-brand-400 group-hover:bg-brand-100 dark:group-hover:bg-brand-900/40 group-hover:scale-105 transition-all">
        <Icon className="w-8 h-8" strokeWidth={1.75} aria-hidden />
      </span>
      <span className="text-xs text-center text-slate-600 dark:text-slate-300 line-clamp-1">{category.name}</span>
    </Link>
  );
}

export function HomePage() {
  const { data: categories } = useCategories();
  const { data: coupons } = useActiveCoupons();
  const { data: featured, isLoading } = useProducts({ page: 1, pageSize: 10, sortBy: "price", desc: true });
  const { data: topRated } = useProducts({ page: 1, pageSize: 5, sortBy: "rating", desc: true });
  const { data: newest } = useProducts({ page: 1, pageSize: 10, sortBy: "createdAt", desc: true });
  const { data: lowStock } = useProducts({ page: 1, pageSize: 8, sortBy: "stock", desc: false });

  const heroProducts = featured?.items ?? [];
  const flashProducts = newest?.items.slice(0, 8) ?? [];
  const lowStockItems = (lowStock?.items ?? []).filter((p) => p.stock > 0 && p.stock <= 15).slice(0, 8);

  return (
    <div className="max-w-7xl mx-auto px-4 py-6 animate-fade-in space-y-10">
      <HeroBanners products={heroProducts} />

      {/* perks strip */}
      <div className="grid grid-cols-1 sm:grid-cols-3 gap-3">
        {PERKS.map((p) => (
          <div key={p.title} className="surface rounded-2xl p-4 flex items-center gap-3">
            <span className="grid place-items-center w-11 h-11 rounded-xl bg-brand-50 dark:bg-brand-900/30 text-brand-600 dark:text-brand-400 shrink-0">
              <p.icon className="w-5 h-5" aria-hidden />
            </span>
            <div>
              <div className="text-sm font-semibold">{p.title}</div>
              <div className="text-xs muted">{p.desc}</div>
            </div>
          </div>
        ))}
      </div>

      {coupons && <VoucherStrip coupons={coupons} />}

      {/* Categories */}
      {categories && categories.length > 0 && (
        <section>
          <div className="flex items-center justify-between mb-4">
            <h2 className="text-lg font-bold">Danh mục phổ biến</h2>
            <Link to="/products" className="text-sm text-brand-600 dark:text-brand-400 hover:underline inline-flex items-center gap-1">
              Xem tất cả <ArrowRight className="w-4 h-4" aria-hidden />
            </Link>
          </div>
          <div className="flex gap-3 overflow-x-auto pb-2">
            {categories.map((c, i) => <CategoryCircle key={c.id} category={c} index={i} />)}
          </div>
        </section>
      )}

      <FlashSale products={flashProducts} />

      {topRated && <ProductSection title="Được yêu thích nhất" icon={Star} products={topRated.items} to="/products?sortBy=rating&desc=true" />}

      {lowStockItems.length > 0 && <LowStockSection title="Sắp hết hàng" icon={Flame} products={lowStockItems} />}

      {/* Featured grid */}
      <section>
        <div className="flex items-center justify-between mb-4">
          <h2 className="text-lg font-bold">Sản phẩm nổi bật</h2>
          <Link to="/products" className="text-sm text-brand-600 dark:text-brand-400 hover:underline inline-flex items-center gap-1">
            Xem tất cả <ArrowRight className="w-4 h-4" aria-hidden />
          </Link>
        </div>
        {isLoading ? (
          <Spinner />
        ) : (
          <div className="grid grid-cols-2 md:grid-cols-3 lg:grid-cols-4 xl:grid-cols-5 gap-4">
            {heroProducts.map((p) => <ProductCard key={p.id} product={p} />)}
          </div>
        )}
      </section>
    </div>
  );
}
