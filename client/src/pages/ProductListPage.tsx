import type { ReactNode } from "react";
import { useEffect, useState } from "react";
import { useSearchParams } from "react-router-dom";
import { Link } from "react-router-dom";
import { ArrowRight, Search, Ticket } from "lucide-react";
import { useBrands, useCatalogAttributes, useCategories, useProducts, type ProductFilters } from "../api/hooks";
import type { ProductAttributeFilter } from "../types";
import { ProductCard } from "../components/ProductCard";
import { PageHeader, Input, Select, Button, Spinner, EmptyState } from "../components/ui";

function Chip({ active, onClick, children }: { active: boolean; onClick: () => void; children: ReactNode }) {
  return (
    <button
      onClick={onClick}
      className={`shrink-0 px-4 py-1.5 rounded-full text-sm font-medium transition ${
        active
          ? "bg-brand-600 text-white"
          : "bg-slate-100 text-slate-600 hover:bg-slate-200 dark:bg-slate-800 dark:text-slate-300 dark:hover:bg-slate-700"
      }`}
    >{children}</button>
  );
}

export function ProductListPage() {
  const [params] = useSearchParams();
  const urlSearch = params.get("search") ?? undefined;
  const urlCategory = params.get("categoryId");
  const urlSortBy = params.get("sortBy") ?? undefined;
  const urlDesc = params.get("desc") === "true";
  const [filters, setFilters] = useState<ProductFilters>({
    page: 1, pageSize: 10,
    search: urlSearch,
    categoryId: urlCategory ? Number(urlCategory) : undefined,
    sortBy: urlSortBy,
    desc: urlDesc,
  });
  const [searchInput, setSearchInput] = useState(urlSearch ?? "");
  const [attributeDefinitionId, setAttributeDefinitionId] = useState<number | undefined>();
  const [attributeText, setAttributeText] = useState("");
  const [attributeMin, setAttributeMin] = useState("");
  const [attributeMax, setAttributeMax] = useState("");
  const [attributeBoolean, setAttributeBoolean] = useState("");
  const [attributeFilterError, setAttributeFilterError] = useState("");
  const { data: categories } = useCategories();
  const { data: brands } = useBrands();
  const { data: attributeDefinitions } = useCatalogAttributes(filters.categoryId);
  const filterableAttributes = (attributeDefinitions ?? []).filter((x) => x.isFilterable);
  const selectedAttribute = filterableAttributes.find((x) => x.id === attributeDefinitionId);
  const { data, isLoading } = useProducts(filters);

  // sync when navbar search / category links change the URL
  useEffect(() => {
    setFilters((f) => ({
      ...f, page: 1,
      search: urlSearch,
      categoryId: urlCategory ? Number(urlCategory) : undefined,
      sortBy: urlSortBy,
      desc: urlDesc,
      attributeFilters: undefined,
    }));
    setSearchInput(urlSearch ?? "");
    setAttributeDefinitionId(undefined);
    setAttributeText("");
    setAttributeMin("");
    setAttributeMax("");
    setAttributeBoolean("");
    setAttributeFilterError("");
  }, [urlSearch, urlCategory, urlSortBy, urlDesc]);

  const update = (patch: Partial<ProductFilters>) => {
    if (Object.prototype.hasOwnProperty.call(patch, "categoryId")) {
      setAttributeDefinitionId(undefined);
      setAttributeText("");
      setAttributeMin("");
      setAttributeMax("");
      setAttributeBoolean("");
      setAttributeFilterError("");
      setFilters((f) => ({ ...f, ...patch, attributeFilters: undefined, page: patch.page ?? 1 }));
      return;
    }
    setFilters((f) => ({ ...f, ...patch, page: patch.page ?? 1 }));
  };

  const applyAttributeFilter = () => {
    if (!selectedAttribute) return;
    const filter: ProductAttributeFilter = { attributeDefinitionId: selectedAttribute.id };
    if (selectedAttribute.dataType === 0) {
      if (!attributeText.trim()) {
        setAttributeFilterError("Nhập giá trị cần lọc.");
        return;
      }
      filter.valueText = attributeText.trim();
    } else if (selectedAttribute.dataType === 1) {
      const min = attributeMin.trim() === "" ? undefined : Number(attributeMin);
      const max = attributeMax.trim() === "" ? undefined : Number(attributeMax);
      if ((min === undefined && max === undefined) || (min !== undefined && !Number.isFinite(min)) ||
          (max !== undefined && !Number.isFinite(max)) || (min !== undefined && max !== undefined && min > max)) {
        setAttributeFilterError("Nhập khoảng số hợp lệ; giá trị nhỏ nhất không được lớn hơn giá trị lớn nhất.");
        return;
      }
      filter.minValue = min;
      filter.maxValue = max;
    } else {
      if (attributeBoolean === "") {
        setAttributeFilterError("Chọn Có hoặc Không.");
        return;
      }
      filter.valueBoolean = attributeBoolean === "true";
    }
    setAttributeFilterError("");
    update({ attributeFilters: [filter] });
  };

  const clearAttributeFilter = () => {
    setAttributeDefinitionId(undefined);
    setAttributeText("");
    setAttributeMin("");
    setAttributeMax("");
    setAttributeBoolean("");
    setAttributeFilterError("");
    update({ attributeFilters: undefined });
  };

  return (
    <div className="max-w-7xl mx-auto px-4 py-6 animate-fade-in">
      {/* slim promo strip */}
      <Link
        to="/"
        className="group flex items-center justify-between gap-3 rounded-2xl bg-gradient-to-r from-brand-600 to-brand-800 text-white px-5 py-3 mb-6 overflow-hidden"
      >
        <div className="flex items-center gap-2.5 min-w-0">
          <Ticket className="w-5 h-5 shrink-0" aria-hidden />
          <span className="text-sm font-medium truncate">Ưu đãi hôm nay · Miễn phí ship đơn từ $50 · Săn mã giảm giá</span>
        </div>
        <span className="inline-flex items-center gap-1 text-sm font-medium whitespace-nowrap group-hover:gap-2 transition-all">
          Xem <ArrowRight className="w-4 h-4" aria-hidden />
        </span>
      </Link>

      <PageHeader
        title="Khám phá sản phẩm"
        subtitle="Tìm kiếm và lọc theo danh mục, giá cả để chọn được sản phẩm phù hợp nhất."
      />

      <div className="surface rounded-2xl p-4 mb-6 space-y-4">
        <div className="flex flex-wrap gap-3 items-end">
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
            value={filters.brandId ?? ""}
            onChange={(e) => update({ brandId: e.target.value ? Number(e.target.value) : undefined })}
          >
            <option value="">Tất cả thương hiệu</option>
            {brands?.map((brand) => <option key={brand.id} value={brand.id}>{brand.name}</option>)}
          </Select>
          <Select
            className="w-auto"
            value={filters.sortBy ? `${filters.sortBy}:${filters.desc ? "desc" : "asc"}` : ""}
            onChange={(e) => {
              const [sortBy, desc] = e.target.value.split(":");
              update({ sortBy: sortBy || undefined, desc: desc === "desc" });
            }}
          >
            <option value="">Mới nhất</option>
            <option value="price:asc">Giá tăng dần</option>
            <option value="price:desc">Giá giảm dần</option>
            <option value="name:asc">Tên A-Z</option>
            <option value="rating:desc">Đánh giá cao nhất</option>
          </Select>
        </div>
        <div className="flex gap-2 overflow-x-auto pb-1 -mb-1">
          <Chip active={!filters.categoryId} onClick={() => update({ categoryId: undefined })}>Tất cả</Chip>
          {categories?.map((c) => (
            <Chip key={c.id} active={filters.categoryId === c.id} onClick={() => update({ categoryId: c.id })}>{c.name}</Chip>
          ))}
        </div>
        {filters.categoryId && filterableAttributes.length > 0 && (
          <div className="flex flex-wrap items-end gap-2 border-t border-slate-200 dark:border-slate-800 pt-3">
            <div className="min-w-[160px] flex-1">
              <label className="block text-xs muted mb-1">Lọc theo thuộc tính</label>
              <Select value={attributeDefinitionId ?? ""}
                onChange={(e) => {
                  setAttributeDefinitionId(e.target.value ? Number(e.target.value) : undefined);
                  setAttributeText("");
                  setAttributeMin("");
                  setAttributeMax("");
                  setAttributeBoolean("");
                  setAttributeFilterError("");
                  update({ attributeFilters: undefined });
                }}>
                <option value="">Chọn thuộc tính</option>
                {filterableAttributes.map((attribute) => <option key={attribute.id} value={attribute.id}>{attribute.displayName}</option>)}
              </Select>
            </div>
            {selectedAttribute?.dataType === 0 && (
              <div className="min-w-[150px] flex-1">
                <label className="block text-xs muted mb-1">Giá trị văn bản</label>
                <Input value={attributeText} maxLength={2000} placeholder="Nhập chính xác"
                  onChange={(e) => setAttributeText(e.target.value)} />
              </div>
            )}
            {selectedAttribute?.dataType === 1 && (
              <>
                <div className="min-w-[110px]">
                  <label className="block text-xs muted mb-1">Từ</label>
                  <Input type="number" step="0.0001" value={attributeMin} placeholder="Min"
                    onChange={(e) => setAttributeMin(e.target.value)} />
                </div>
                <div className="min-w-[110px]">
                  <label className="block text-xs muted mb-1">Đến</label>
                  <Input type="number" step="0.0001" value={attributeMax} placeholder="Max"
                    onChange={(e) => setAttributeMax(e.target.value)} />
                </div>
              </>
            )}
            {selectedAttribute?.dataType === 2 && (
              <div className="min-w-[120px]">
                <label className="block text-xs muted mb-1">Giá trị</label>
                <Select value={attributeBoolean} onChange={(e) => setAttributeBoolean(e.target.value)}>
                  <option value="">Chọn</option>
                  <option value="true">Có</option>
                  <option value="false">Không</option>
                </Select>
              </div>
            )}
            <Button type="button" onClick={applyAttributeFilter} disabled={!selectedAttribute}>Áp dụng</Button>
            <Button type="button" variant="ghost" onClick={clearAttributeFilter}>Xóa lọc</Button>
            {attributeFilterError && <p className="w-full text-rose-500 text-xs">{attributeFilterError}</p>}
          </div>
        )}
      </div>

      {isLoading ? (
        <Spinner />
      ) : (
        <>
          <div className="grid grid-cols-2 md:grid-cols-3 lg:grid-cols-4 xl:grid-cols-5 gap-4">
            {data?.items.map((p) => <ProductCard key={p.id} product={p} />)}
          </div>
          {data && data.items.length === 0 && (
            <EmptyState icon={Search} title="Không có sản phẩm" hint="Thử thay đổi từ khóa hoặc bộ lọc." />
          )}
          {data && data.totalPages > 1 && (
            <div className="flex justify-center gap-2 mt-8">
              {Array.from({ length: data.totalPages }, (_, i) => i + 1).map((p) => (
                <button
                  key={p} onClick={() => update({ page: p })}
                  className={`w-10 h-10 rounded-full text-sm font-medium transition ${
                    p === data.page
                      ? "bg-brand-600 text-white shadow-sm"
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
