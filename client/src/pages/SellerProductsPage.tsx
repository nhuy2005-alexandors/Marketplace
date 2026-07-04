import { useState } from "react";
import { Clock } from "lucide-react";
import {
  useCategories, useCreateCategory, useCreateProduct, useDeleteProduct, useMe, useProducts,
  useUpdateProduct, useUploadProductImage, type ProductInput,
} from "../api/hooks";
import { apiError } from "../api/client";
import { useAuth } from "../store/auth";
import type { Product } from "../types";
import { Button, Card, Input, Select, Textarea } from "../components/ui";

const EMPTY_FORM: ProductInput = { name: "", description: "", price: 0, stock: 0, imageUrl: "", categoryId: 0 };

export function SellerProductsPage() {
  const sellerId = useAuth((s) => s.user?.id);
  const sellerStatus = useAuth((s) => s.user?.sellerStatus);
  useMe(); // đồng bộ sellerStatus mới nhất (vd vừa được Admin duyệt) vào store
  const { data: categories } = useCategories();
  const { data: products } = useProducts({ sellerId, pageSize: 50 });
  const createProduct = useCreateProduct();
  const updateProduct = useUpdateProduct();
  const deleteProduct = useDeleteProduct();
  const uploadImage = useUploadProductImage();
  const createCategory = useCreateCategory();

  const [form, setForm] = useState<ProductInput>(EMPTY_FORM);
  const [editingId, setEditingId] = useState<number | null>(null);
  const [newCategory, setNewCategory] = useState("");
  const [error, setError] = useState("");

  const addCategory = async () => {
    if (!newCategory.trim()) return;
    try {
      const cat = await createCategory.mutateAsync({ name: newCategory.trim() });
      setForm((f) => ({ ...f, categoryId: cat.id }));
      setNewCategory("");
    } catch (err) {
      setError(apiError(err));
    }
  };

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
    <div className="max-w-5xl mx-auto px-4 py-6 grid md:grid-cols-3 gap-6 animate-fade-in">
      <Card className="md:col-span-1 p-5 h-fit">
        <h2 className="font-semibold mb-3">{editingId ? `Sửa sản phẩm #${editingId}` : "Thêm sản phẩm"}</h2>
        {sellerStatus === "Pending" && (
          <div className="rounded-2xl p-4 mb-3 bg-amber-50 dark:bg-amber-500/10 text-amber-700 dark:text-amber-300 text-sm border border-amber-200 dark:border-amber-500/20 flex items-start gap-2">
            <Clock className="w-4 h-4 shrink-0 mt-0.5" aria-hidden />
            <span>Tài khoản người bán của bạn đang chờ Admin duyệt. Bạn chưa thể đăng sản phẩm cho đến khi được duyệt.</span>
          </div>
        )}
        <form onSubmit={submit} className="space-y-2">
          <Input required placeholder="Tên sản phẩm" value={form.name}
            onChange={(e) => setForm({ ...form, name: e.target.value })} />
          <Textarea placeholder="Mô tả" value={form.description ?? ""}
            onChange={(e) => setForm({ ...form, description: e.target.value })} />
          <div className="flex gap-2">
            <Input required type="number" step="0.01" min={0.01} placeholder="Giá" value={form.price || ""}
              onChange={(e) => setForm({ ...form, price: Number(e.target.value) })}
              className="w-1/2" />
            <Input required type="number" min={0} placeholder="Tồn kho" value={form.stock || ""}
              onChange={(e) => setForm({ ...form, stock: Number(e.target.value) })}
              className="w-1/2" />
          </div>
          <Select required value={form.categoryId || ""}
            onChange={(e) => setForm({ ...form, categoryId: Number(e.target.value) })}>
            <option value="">-- Chọn danh mục --</option>
            {categories?.map((c) => <option key={c.id} value={c.id}>{c.name}</option>)}
          </Select>
          <div className="flex gap-2">
            <Input value={newCategory} onChange={(e) => setNewCategory(e.target.value)}
              placeholder="Hoặc tạo danh mục mới"
              className="flex-1" />
            <Button type="button" variant="ghost" onClick={addCategory} disabled={createCategory.isPending}>+ Danh mục</Button>
          </div>
          <div>
            <input type="file" accept="image/*" onChange={onFileChange} className="text-sm" />
            {uploadImage.isPending && <span className="text-xs muted ml-2">Đang tải ảnh...</span>}
            {form.imageUrl && <img src={form.imageUrl} alt="preview" className="w-20 h-20 object-cover rounded-lg mt-2" />}
          </div>
          {error && <div className="text-rose-500 text-sm">{error}</div>}
          <div className="flex gap-2">
            <Button disabled={createProduct.isPending || updateProduct.isPending || (sellerStatus === "Pending" && !editingId)} className="flex-1">
              {editingId ? "Lưu thay đổi" : "Thêm sản phẩm"}
            </Button>
            {editingId && (
              <Button type="button" variant="ghost" onClick={resetForm}>Hủy</Button>
            )}
          </div>
        </form>
      </Card>

      <div className="md:col-span-2">
        <h1 className="text-xl font-bold mb-3">Sản phẩm của tôi</h1>
        <Card className="divide-y divide-slate-200 dark:divide-slate-800">
          {products?.items.length === 0 && <div className="p-4 muted text-sm">Chưa có sản phẩm.</div>}
          {products?.items.map((p) => (
            <div key={p.id} className="flex items-center gap-3 p-4 hover:bg-slate-50 dark:hover:bg-slate-800/50 transition-colors">
              <img src={p.imageUrl ?? "https://via.placeholder.com/60"} alt={p.name} className="w-14 h-14 rounded-lg object-cover" />
              <div className="flex-1">
                <div className="font-medium text-sm">{p.name}</div>
                <div className="text-xs muted">{p.categoryName} · Tồn: {p.stock}</div>
              </div>
              <span className="font-semibold text-brand-600 dark:text-brand-400 text-sm">${p.price.toFixed(2)}</span>
              <button onClick={() => startEdit(p)} className="text-sm text-brand-600 dark:text-brand-400 hover:underline">Sửa</button>
              <button onClick={() => deleteProduct.mutate(p.id)} className="text-sm text-rose-500 hover:underline">Xóa</button>
            </div>
          ))}
        </Card>
      </div>
    </div>
  );
}
