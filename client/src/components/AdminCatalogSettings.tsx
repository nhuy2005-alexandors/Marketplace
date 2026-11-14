import { useState } from "react";
import {
  useCatalogAttributes, useCreateAttributeDefinition, useCreateBrand, useDeleteAttributeDefinition,
  useDeleteBrand, useUpdateAttributeDefinition, useUpdateBrand, useBrands,
} from "../api/hooks";
import type { AttributeDefinition, Brand, CatalogAttributeType, Category } from "../types";
import { apiError } from "../api/client";
import { Button, Card, Input, Select, Textarea } from "./ui";

type Props = { categories: Category[] };

const EMPTY_ATTRIBUTE = {
  code: "",
  displayName: "",
  dataType: 0 as CatalogAttributeType,
  unit: "",
  isRequired: false,
  isFilterable: true,
  isComparable: false,
  isActive: true,
};

export function AdminCatalogSettings({ categories }: Props) {
  const { data: brands } = useBrands();
  const createBrand = useCreateBrand();
  const updateBrand = useUpdateBrand();
  const deleteBrand = useDeleteBrand();
  const createAttribute = useCreateAttributeDefinition();
  const updateAttribute = useUpdateAttributeDefinition();
  const deleteAttribute = useDeleteAttributeDefinition();

  const [brandName, setBrandName] = useState("");
  const [brandSlug, setBrandSlug] = useState("");
  const [brandDescription, setBrandDescription] = useState("");
  const [editingBrandId, setEditingBrandId] = useState<number | null>(null);
  const [brandError, setBrandError] = useState("");

  const [categoryId, setCategoryId] = useState(0);
  const selectedCategoryId = categoryId || categories[0]?.id || 0;
  const { data: attributes } = useCatalogAttributes(selectedCategoryId, true);
  const [attributeForm, setAttributeForm] = useState(EMPTY_ATTRIBUTE);
  const [editingAttributeId, setEditingAttributeId] = useState<number | null>(null);
  const [attributeError, setAttributeError] = useState("");

  const resetBrand = () => {
    setEditingBrandId(null);
    setBrandName("");
    setBrandSlug("");
    setBrandDescription("");
  };

  const submitBrand = async (e: React.FormEvent) => {
    e.preventDefault();
    setBrandError("");
    const body = {
      name: brandName.trim(),
      slug: brandSlug.trim() || undefined,
      description: brandDescription.trim() || undefined,
    };
    try {
      if (editingBrandId) await updateBrand.mutateAsync({ id: editingBrandId, body });
      else await createBrand.mutateAsync(body);
      resetBrand();
    } catch (err) {
      setBrandError(apiError(err));
    }
  };

  const editBrand = (brand: Brand) => {
    setEditingBrandId(brand.id);
    setBrandName(brand.name);
    setBrandSlug(brand.slug);
    setBrandDescription(brand.description ?? "");
  };

  const removeBrand = async (id: number) => {
    setBrandError("");
    try { await deleteBrand.mutateAsync(id); }
    catch (err) { setBrandError(apiError(err)); }
  };

  const resetAttribute = () => {
    setEditingAttributeId(null);
    setAttributeForm(EMPTY_ATTRIBUTE);
  };

  const editAttribute = (attribute: AttributeDefinition) => {
    setCategoryId(attribute.categoryId);
    setEditingAttributeId(attribute.id);
    setAttributeForm({
      code: attribute.code,
      displayName: attribute.displayName,
      dataType: attribute.dataType,
      unit: attribute.unit ?? "",
      isRequired: attribute.isRequired,
      isFilterable: attribute.isFilterable,
      isComparable: attribute.isComparable,
      isActive: attribute.isActive,
    });
  };

  const submitAttribute = async (e: React.FormEvent) => {
    e.preventDefault();
    setAttributeError("");
    if (!selectedCategoryId) return;
    const body = { categoryId: selectedCategoryId, ...attributeForm, unit: attributeForm.unit.trim() || undefined };
    try {
      if (editingAttributeId) await updateAttribute.mutateAsync({ id: editingAttributeId, body });
      else await createAttribute.mutateAsync(body);
      resetAttribute();
    } catch (err) {
      setAttributeError(apiError(err));
    }
  };

  const removeAttribute = async (id: number) => {
    setAttributeError("");
    try { await deleteAttribute.mutateAsync(id); }
    catch (err) { setAttributeError(apiError(err)); }
  };

  return (
    <Card className="p-5 space-y-5">
      <div>
        <h2 className="font-semibold">Quản lý catalog</h2>
        <p className="muted text-xs mt-1">Khai báo thương hiệu và bộ thuộc tính kỹ thuật theo từng danh mục.</p>
      </div>

      <section className="space-y-3">
        <h3 className="text-sm font-semibold">Thương hiệu</h3>
        <form onSubmit={submitBrand} className="space-y-2">
          <Input required maxLength={200} placeholder="Tên thương hiệu" value={brandName}
            onChange={(e) => setBrandName(e.target.value)} />
          <Input maxLength={200} placeholder="Slug (để trống để tự tạo)" value={brandSlug}
            onChange={(e) => setBrandSlug(e.target.value)} />
          <Textarea maxLength={2000} rows={2} placeholder="Mô tả thương hiệu" value={brandDescription}
            onChange={(e) => setBrandDescription(e.target.value)} />
          {brandError && <p className="text-rose-500 text-xs">{brandError}</p>}
          <div className="flex gap-2">
            <Button type="submit" disabled={!brandName.trim() || createBrand.isPending || updateBrand.isPending}>
              {editingBrandId ? "Lưu thương hiệu" : "Thêm thương hiệu"}
            </Button>
            {editingBrandId && <Button type="button" variant="ghost" onClick={resetBrand}>Hủy</Button>}
          </div>
        </form>
        <div className="space-y-1">
          {brands?.map((brand) => (
            <div key={brand.id} className="flex items-center gap-2 text-xs py-1 border-t border-slate-100 dark:border-slate-800">
              <div className="flex-1 min-w-0">
                <div className="font-medium truncate">{brand.name}</div>
                <div className="muted truncate">{brand.slug}</div>
              </div>
              <button type="button" className="text-brand-600 hover:underline" onClick={() => editBrand(brand)}>Sửa</button>
              <button type="button" className="text-rose-500 hover:underline" onClick={() => removeBrand(brand.id)}>Xóa</button>
            </div>
          ))}
        </div>
      </section>

      <section className="space-y-3 border-t border-slate-200 dark:border-slate-800 pt-4">
        <h3 className="text-sm font-semibold">Thuộc tính theo danh mục</h3>
        <Select
          value={selectedCategoryId || ""}
          disabled={!!editingAttributeId}
          onChange={(e) => { setCategoryId(Number(e.target.value)); resetAttribute(); }}
        >
          <option value="">-- Chọn danh mục --</option>
          {categories.map((category) => <option key={category.id} value={category.id}>{category.name}</option>)}
        </Select>
        <form onSubmit={submitAttribute} className="space-y-2">
          <Input required maxLength={100} placeholder="Mã thuộc tính (VD: RAM_GB)" value={attributeForm.code}
            onChange={(e) => setAttributeForm((f) => ({ ...f, code: e.target.value }))} />
          <Input required maxLength={200} placeholder="Tên hiển thị (VD: RAM)" value={attributeForm.displayName}
            onChange={(e) => setAttributeForm((f) => ({ ...f, displayName: e.target.value }))} />
          <div className="flex gap-2">
            <Select className="flex-1" value={attributeForm.dataType} disabled={!!editingAttributeId}
              onChange={(e) => setAttributeForm((f) => ({ ...f, dataType: Number(e.target.value) as CatalogAttributeType }))}>
              <option value={0}>Văn bản</option>
              <option value={1}>Số</option>
              <option value={2}>Có / Không</option>
            </Select>
            <Input className="w-1/3" maxLength={40} placeholder="Đơn vị" value={attributeForm.unit}
              onChange={(e) => setAttributeForm((f) => ({ ...f, unit: e.target.value }))} />
          </div>
          <div className="grid grid-cols-2 gap-2 text-xs">
            {([
              ["isRequired", "Bắt buộc"],
              ["isFilterable", "Cho phép lọc"],
              ["isComparable", "Cho phép so sánh"],
              ["isActive", "Đang hoạt động"],
            ] as const).map(([key, label]) => (
              <label key={key} className="flex items-center gap-2">
                <input type="checkbox" checked={attributeForm[key]}
                  onChange={(e) => setAttributeForm((f) => ({ ...f, [key]: e.target.checked }))} />
                {label}
              </label>
            ))}
          </div>
          {attributeError && <p className="text-rose-500 text-xs">{attributeError}</p>}
          <div className="flex gap-2">
            <Button type="submit" disabled={!selectedCategoryId || !attributeForm.code.trim() || !attributeForm.displayName.trim() || createAttribute.isPending || updateAttribute.isPending}>
              {editingAttributeId ? "Lưu thuộc tính" : "Thêm thuộc tính"}
            </Button>
            {editingAttributeId && <Button type="button" variant="ghost" onClick={resetAttribute}>Hủy</Button>}
          </div>
        </form>
        <div className="space-y-1">
          {attributes?.map((attribute) => (
            <div key={attribute.id} className="flex items-center gap-2 text-xs py-2 border-t border-slate-100 dark:border-slate-800">
              <div className="flex-1 min-w-0">
                <div className="font-medium truncate">{attribute.displayName} <span className="muted">({attribute.code})</span></div>
                <div className="muted">{["Văn bản", "Số", "Có / Không"][attribute.dataType]} · {attribute.isRequired ? "Bắt buộc" : "Tùy chọn"} · {attribute.isActive ? "Active" : "Inactive"}</div>
              </div>
              <button type="button" className="text-brand-600 hover:underline" onClick={() => editAttribute(attribute)}>Sửa</button>
              <button type="button" className="text-rose-500 hover:underline" onClick={() => removeAttribute(attribute.id)}>Xóa</button>
            </div>
          ))}
        </div>
      </section>
    </Card>
  );
}
