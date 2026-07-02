import { useState } from "react";
import {
  useCategories, useCreateProduct, useDeleteProduct, useProducts,
  useUpdateProduct, useUploadProductImage, type ProductInput,
} from "../api/hooks";
import { apiError } from "../api/client";
import { useAuth } from "../store/auth";
import type { Product } from "../types";

const EMPTY_FORM: ProductInput = { name: "", description: "", price: 0, stock: 0, imageUrl: "", categoryId: 0 };

export function SellerProductsPage() {
  const sellerId = useAuth((s) => s.user?.id);
  const { data: categories } = useCategories();
  const { data: products } = useProducts({ sellerId, pageSize: 50 });
  const createProduct = useCreateProduct();
  const updateProduct = useUpdateProduct();
  const deleteProduct = useDeleteProduct();
  const uploadImage = useUploadProductImage();

  const [form, setForm] = useState<ProductInput>(EMPTY_FORM);
  const [editingId, setEditingId] = useState<number | null>(null);
  const [error, setError] = useState("");

  const startEdit = (p: Product) => {
    setEditingId(p.id);
    setForm({ name: p.name, description: p.description, price: p.price, stock: p.stock, imageUrl: p.imageUrl, categoryId: p.categoryId });
  };
  const resetForm = () => { setEditingId(null); setForm(EMPTY_FORM); };

  const submit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError("");
    try {
      if (editingId) await updateProduct.mutateAsync({ id: editingId, body: form });
      else await createProduct.mutateAsync(form);
      resetForm();
    } catch (err) {
      setError(apiError(err));
    }
  };

  const onFileChange = async (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0];
    if (!file) return;
    try {
      const url = await uploadImage.mutateAsync(file);
      setForm((f) => ({ ...f, imageUrl: url }));
    } catch (err) {
      setError(apiError(err));
    }
  };

  return (
    <div className="max-w-5xl mx-auto px-4 py-6 grid md:grid-cols-3 gap-6">
      <div className="md:col-span-1 bg-white rounded-xl shadow-sm border border-slate-100 p-5 h-fit">
        <h2 className="font-semibold mb-3">{editingId ? `Sửa sản phẩm #${editingId}` : "Thêm sản phẩm"}</h2>
        <form onSubmit={submit} className="space-y-2">
          <input required placeholder="Tên sản phẩm" value={form.name}
            onChange={(e) => setForm({ ...form, name: e.target.value })}
            className="w-full border rounded-lg px-3 py-2 text-sm" />
          <textarea placeholder="Mô tả" value={form.description ?? ""}
            onChange={(e) => setForm({ ...form, description: e.target.value })}
            className="w-full border rounded-lg px-3 py-2 text-sm" />
          <div className="flex gap-2">
            <input required type="number" step="0.01" min={0.01} placeholder="Giá" value={form.price || ""}
              onChange={(e) => setForm({ ...form, price: Number(e.target.value) })}
              className="w-1/2 border rounded-lg px-3 py-2 text-sm" />
            <input required type="number" min={0} placeholder="Tồn kho" value={form.stock || ""}
              onChange={(e) => setForm({ ...form, stock: Number(e.target.value) })}
              className="w-1/2 border rounded-lg px-3 py-2 text-sm" />
          </div>
          <select required value={form.categoryId || ""}
            onChange={(e) => setForm({ ...form, categoryId: Number(e.target.value) })}
            className="w-full border rounded-lg px-3 py-2 text-sm">
            <option value="">-- Chọn danh mục --</option>
            {categories?.map((c) => <option key={c.id} value={c.id}>{c.name}</option>)}
          </select>
          <div>
            <input type="file" accept="image/*" onChange={onFileChange} className="text-sm" />
            {uploadImage.isPending && <span className="text-xs text-slate-400 ml-2">Đang tải ảnh...</span>}
            {form.imageUrl && <img src={form.imageUrl} alt="preview" className="w-20 h-20 object-cover rounded-lg mt-2" />}
          </div>
          {error && <div className="text-rose-500 text-sm">{error}</div>}
          <div className="flex gap-2">
            <button
              disabled={createProduct.isPending || updateProduct.isPending}
              className="flex-1 py-2 rounded-lg bg-brand-600 text-white text-sm hover:bg-brand-700 disabled:opacity-50"
            >{editingId ? "Lưu thay đổi" : "Thêm sản phẩm"}</button>
            {editingId && (
              <button type="button" onClick={resetForm} className="px-4 py-2 rounded-lg border text-sm">Hủy</button>
            )}
          </div>
        </form>
      </div>

      <div className="md:col-span-2">
        <h1 className="text-xl font-bold mb-3">Sản phẩm của tôi</h1>
        <div className="bg-white rounded-xl shadow-sm border border-slate-100 divide-y">
          {products?.items.length === 0 && <div className="p-4 text-slate-400 text-sm">Chưa có sản phẩm.</div>}
          {products?.items.map((p) => (
            <div key={p.id} className="flex items-center gap-3 p-4">
              <img src={p.imageUrl ?? "https://via.placeholder.com/60"} alt={p.name} className="w-14 h-14 rounded-lg object-cover" />
              <div className="flex-1">
                <div className="font-medium text-sm">{p.name}</div>
                <div className="text-xs text-slate-400">{p.categoryName} · Tồn: {p.stock}</div>
              </div>
              <span className="font-semibold text-brand-700 text-sm">${p.price.toFixed(2)}</span>
              <button onClick={() => startEdit(p)} className="text-sm text-brand-600 hover:underline">Sửa</button>
              <button onClick={() => deleteProduct.mutate(p.id)} className="text-sm text-rose-500 hover:underline">Xóa</button>
            </div>
          ))}
        </div>
      </div>
    </div>
  );
}
