import { useState } from "react";
import { Store, Package } from "lucide-react";
import { useParams } from "react-router-dom";
import { useProducts, useSellerShop } from "../api/hooks";
import { ProductCard } from "../components/ProductCard";
import { PageHeader, Spinner, EmptyState } from "../components/ui";

export function SellerShopPage() {
  const { sellerId } = useParams();
  const id = Number(sellerId);
  const [page, setPage] = useState(1);
  const { data: shop, isLoading: shopLoading, isError } = useSellerShop(id);
  const { data, isLoading } = useProducts({ sellerId: id, page, pageSize: 8 });

  if (shopLoading) return <Spinner />;
  if (isError || !shop)
    return (
      <div className="max-w-7xl mx-auto px-4 py-6">
        <EmptyState icon={Store} title="Không tìm thấy cửa hàng" hint="Người bán này không tồn tại." />
      </div>
    );

  return (
    <div className="max-w-7xl mx-auto px-4 py-6 animate-fade-in">
      <PageHeader title={shop.shopName} subtitle="Sản phẩm của cửa hàng." />

      {isLoading ? (
        <Spinner />
      ) : (
        <>
          <div className="grid grid-cols-2 md:grid-cols-3 lg:grid-cols-4 xl:grid-cols-5 gap-4">
            {data?.items.map((p) => <ProductCard key={p.id} product={p} />)}
          </div>
          {data && data.items.length === 0 && (
            <EmptyState icon={Package} title="Chưa có sản phẩm" hint="Cửa hàng này chưa đăng sản phẩm nào." />
          )}
          {data && data.totalPages > 1 && (
            <div className="flex justify-center gap-2 mt-8">
              {Array.from({ length: data.totalPages }, (_, i) => i + 1).map((p) => (
                <button
                  key={p} onClick={() => setPage(p)}
                  className={`w-9 h-9 rounded-lg text-sm transition ${
                    p === data.page
                      ? "bg-brand-600 text-white shadow-glow"
                      : "surface hover:bg-slate-100 dark:hover:bg-slate-800"
                  }`}
                >{p}</button>
              ))}
            </div>
          )}
        </>
      )}
    </div>
  );
}
