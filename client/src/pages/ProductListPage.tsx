import { useState } from "react";
import { useCategories, useProducts, type ProductFilters } from "../api/hooks";
import { ProductCard } from "../components/ProductCard";
import { PageHeader, Input, Select, Button, Spinner, EmptyState } from "../components/ui";

export function ProductListPage() {
  const [filters, setFilters] = useState<ProductFilters>({ page: 1, pageSize: 8 });
  const [searchInput, setSearchInput] = useState("");
  const { data: categories } = useCategories();
  const { data, isLoading } = useProducts(filters);

  const update = (patch: Partial<ProductFilters>) =>
    setFilters((f) => ({ ...f, ...patch, page: patch.page ?? 1 }));

  return (
    <div className="max-w-6xl mx-auto px-4 py-6 animate-fade-in">
      <PageHeader
        title="Khám phá sản phẩm"
        subtitle="Tìm kiếm và lọc theo danh mục, giá cả để chọn được sản phẩm phù hợp nhất."
      />

      <div className="surface rounded-2xl p-4 mb-6 flex flex-wrap gap-3 items-end">
        <form
          onSubmit={(e) => { e.preventDefault(); update({ search: searchInput }); }}
          className="flex gap-2 flex-1 min-w-[200px]"
        >
          <Input
            placeholder="Tìm sản phẩm..." value={searchInput}
            onChange={(e) => setSearchInput(e.target.value)}
          />
          <Button type="submit">Tìm</Button>
        </form>
        <Select
          className="w-auto"
          onChange={(e) => update({ categoryId: e.target.value ? Number(e.target.value) : undefined })}
        >
          <option value="">Tất cả danh mục</option>
          {categories?.map((c) => <option key={c.id} value={c.id}>{c.name}</option>)}
        </Select>
        <Select
          className="w-auto"
          onChange={(e) => {
            const [sortBy, desc] = e.target.value.split(":");
            update({ sortBy: sortBy || undefined, desc: desc === "desc" });
          }}
        >
          <option value="">Mới nhất</option>
          <option value="price:asc">Giá tăng dần</option>
          <option value="price:desc">Giá giảm dần</option>
          <option value="name:asc">Tên A-Z</option>
        </Select>
      </div>

      {isLoading ? (
        <Spinner />
      ) : (
        <>
          <div className="grid grid-cols-2 md:grid-cols-4 gap-4">
            {data?.items.map((p) => <ProductCard key={p.id} product={p} />)}
          </div>
          {data && data.items.length === 0 && (
            <EmptyState icon="🔍" title="Không có sản phẩm" hint="Thử thay đổi từ khóa hoặc bộ lọc." />
          )}
          {data && data.totalPages > 1 && (
            <div className="flex justify-center gap-2 mt-8">
              {Array.from({ length: data.totalPages }, (_, i) => i + 1).map((p) => (
                <button
                  key={p} onClick={() => update({ page: p })}
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
