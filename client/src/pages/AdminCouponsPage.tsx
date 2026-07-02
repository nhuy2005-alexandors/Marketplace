import { useState } from "react";
import { useCoupons, useCreateCoupon, useDeleteCoupon } from "../api/hooks";
import { apiError } from "../api/client";

export function AdminCouponsPage() {
  const { data: coupons } = useCoupons();
  const createCoupon = useCreateCoupon();
  const deleteCoupon = useDeleteCoupon();

  const [form, setForm] = useState({
    code: "", type: "Percentage", value: 10, minOrderAmount: 0, maxUses: "", expiresAt: "",
  });
  const [error, setError] = useState("");

  const submit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError("");
    try {
      await createCoupon.mutateAsync({
        code: form.code.trim().toUpperCase(),
        type: form.type,
        value: form.value,
        minOrderAmount: form.minOrderAmount,
        maxUses: form.maxUses ? Number(form.maxUses) : undefined,
        expiresAt: form.expiresAt || undefined,
      });
      setForm({ code: "", type: "Percentage", value: 10, minOrderAmount: 0, maxUses: "", expiresAt: "" });
    } catch (err) {
      setError(apiError(err));
    }
  };

  return (
    <div className="max-w-3xl mx-auto px-4 py-6">
      <h1 className="text-xl font-bold mb-4">Mã giảm giá</h1>

      <form onSubmit={submit} className="bg-white rounded-xl shadow-sm border border-slate-100 p-5 mb-6 space-y-3">
        <div className="flex gap-2">
          <input required placeholder="Mã (VD: SALE20)" value={form.code}
            onChange={(e) => setForm({ ...form, code: e.target.value })}
            className="flex-1 border rounded-lg px-3 py-2 text-sm" />
          <select value={form.type} onChange={(e) => setForm({ ...form, type: e.target.value })}
            className="border rounded-lg px-3 py-2 text-sm">
            <option value="Percentage">Phần trăm (%)</option>
            <option value="FixedAmount">Số tiền cố định ($)</option>
          </select>
        </div>
        <div className="flex gap-2">
          <input required type="number" min={0.01} placeholder="Giá trị" value={form.value}
            onChange={(e) => setForm({ ...form, value: Number(e.target.value) })}
            className="flex-1 border rounded-lg px-3 py-2 text-sm" />
          <input type="number" min={0} placeholder="Đơn tối thiểu" value={form.minOrderAmount}
            onChange={(e) => setForm({ ...form, minOrderAmount: Number(e.target.value) })}
            className="flex-1 border rounded-lg px-3 py-2 text-sm" />
          <input type="number" min={1} placeholder="Số lượt tối đa" value={form.maxUses}
            onChange={(e) => setForm({ ...form, maxUses: e.target.value })}
            className="flex-1 border rounded-lg px-3 py-2 text-sm" />
        </div>
        <input type="datetime-local" value={form.expiresAt}
          onChange={(e) => setForm({ ...form, expiresAt: e.target.value })}
          className="w-full border rounded-lg px-3 py-2 text-sm" />
        {error && <div className="text-rose-500 text-sm">{error}</div>}
        <button
          disabled={createCoupon.isPending}
          className="px-4 py-2 rounded-lg bg-brand-600 text-white text-sm hover:bg-brand-700 disabled:opacity-50"
        >Tạo mã</button>
      </form>

      <div className="bg-white rounded-xl shadow-sm border border-slate-100 divide-y">
        {coupons?.length === 0 && <div className="p-4 text-slate-400 text-sm">Chưa có mã nào.</div>}
        {coupons?.map((c) => (
          <div key={c.id} className="flex items-center justify-between p-4">
            <div>
              <div className="font-medium">{c.code} {!c.isActive && <span className="text-xs text-rose-500">(vô hiệu)</span>}</div>
              <div className="text-xs text-slate-400">
                {c.type === "Percentage" ? `${c.value}%` : `$${c.value}`} · Đơn tối thiểu ${c.minOrderAmount} ·
                {" "}Đã dùng {c.timesUsed}{c.maxUses ? `/${c.maxUses}` : ""}
                {c.expiresAt && ` · HSD ${new Date(c.expiresAt).toLocaleDateString("vi-VN")}`}
              </div>
            </div>
            <button onClick={() => deleteCoupon.mutate(c.id)} className="text-sm text-rose-500 hover:underline">Xóa</button>
          </div>
        ))}
      </div>
    </div>
  );
}
