import { useState } from "react";
import { useCategories, useProducts, type ProductFilters } from "../api/hooks";
import { ProductCard } from "../components/ProductCard";

export function ProductListPage() {
  const [filters, setFilters] = useState<ProductFilters>({ page: 1, pageSize: 8 });
  const [searchInput, setSearchInput] = useState("");
  const { data: categories } = useCategories();
  const { data, isLoading } = useProducts(filters);

  const update = (patch: Partial<ProductFilters>) =>
    setFilters((f) => ({ ...f, ...patch, page: patch.page ?? 1 }));

  return (
    <div className="max-w-6xl mx-auto px-4 py-6">
      <div className="flex flex-wrap gap-3 mb-6 items-end">
        <form
          onSubmit={(e) => { e.preventDefault(); update({ search: searchInput }); }}
          className="flex gap-2 flex-1 min-w-[200px]"
        >
          <input
            className="flex-1 border rounded-lg px-3 py-2 text-sm"
            placeholder="Tìm sản phẩm..." value={searchInput}
            onChange={(e) => setSearchInput(e.target.value)}
          />
          <button className="px-4 py-2 rounded-lg bg-brand-600 text-white text-sm">Tìm</button>
        </form>
        <select
          className="border rounded-lg px-3 py-2 text-sm"
          onChange={(e) => update({ categoryId: e.target.value ? Number(e.target.value) : undefined })}
        >
          <option value="">Tất cả danh mục</option>
          {categories?.map((c) => <option key={c.id} value={c.id}>{c.name}</option>)}
        </select>
        <select
          className="border rounded-lg px-3 py-2 text-sm"
          onChange={(e) => {
            const [sortBy, desc] = e.target.value.split(":");
            update({ sortBy: sortBy || undefined, desc: desc === "desc" });
          }}
        >
          <option value="">Mới nhất</option>
          <option value="price:asc">Giá tăng dần</option>
          <option value="price:desc">Giá giảm dần</option>
          <option value="name:asc">Tên A-Z</option>
        </select>
      </div>

      {isLoading ? (
        <div className="text-center text-slate-400 py-12">Đang tải...</div>
      ) : (
        <>
          <div className="grid grid-cols-2 md:grid-cols-4 gap-4">
            {data?.items.map((p) => <ProductCard key={p.id} product={p} />)}
          </div>
          {data && data.items.length === 0 && (
            <div className="text-center text-slate-400 py-12">Không có sản phẩm.</div>
          )}
          {data && data.totalPages > 1 && (
            <div className="flex justify-center gap-2 mt-8">
              {Array.from({ length: data.totalPages }, (_, i) => i + 1).map((p) => (
                <button
                  key={p} onClick={() => update({ page: p })}
                  className={`w-9 h-9 rounded-lg text-sm ${p === data.page ? "bg-brand-600 text-white" : "bg-white border"}`}
                >{p}</button>
              ))}
            </div>
          )}
        </>
      )}
    </div>
  );
}
