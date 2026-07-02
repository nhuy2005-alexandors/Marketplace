import { useState } from "react";
import { useCoupons, useCreateCoupon, useDeleteCoupon } from "../api/hooks";
import { apiError } from "../api/client";
import { Button, Card, Input, PageHeader, Select } from "../components/ui";

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
    <div className="max-w-3xl mx-auto px-4 py-6 animate-fade-in">
      <PageHeader title="Mã giảm giá" />

      <Card className="p-5 mb-6">
        <form onSubmit={submit} className="space-y-3">
          <div className="flex gap-2">
            <Input required placeholder="Mã (VD: SALE20)" value={form.code}
              onChange={(e) => setForm({ ...form, code: e.target.value })}
              className="flex-1" />
            <Select value={form.type} onChange={(e) => setForm({ ...form, type: e.target.value })}
              className="w-auto">
              <option value="Percentage">Phần trăm (%)</option>
              <option value="FixedAmount">Số tiền cố định ($)</option>
            </Select>
          </div>
          <div className="flex gap-2">
            <Input required type="number" min={0.01} placeholder="Giá trị" value={form.value}
              onChange={(e) => setForm({ ...form, value: Number(e.target.value) })}
              className="flex-1" />
            <Input type="number" min={0} placeholder="Đơn tối thiểu" value={form.minOrderAmount}
              onChange={(e) => setForm({ ...form, minOrderAmount: Number(e.target.value) })}
              className="flex-1" />
            <Input type="number" min={1} placeholder="Số lượt tối đa" value={form.maxUses}
              onChange={(e) => setForm({ ...form, maxUses: e.target.value })}
              className="flex-1" />
          </div>
          <Input type="datetime-local" value={form.expiresAt}
            onChange={(e) => setForm({ ...form, expiresAt: e.target.value })}
            className="w-full" />
          {error && <div className="text-rose-500 text-sm">{error}</div>}
          <Button disabled={createCoupon.isPending}>Tạo mã</Button>
        </form>
      </Card>

      <Card className="divide-y divide-slate-200 dark:divide-slate-800">
        {coupons?.length === 0 && <div className="p-4 muted text-sm">Chưa có mã nào.</div>}
        {coupons?.map((c) => (
          <div key={c.id} className="flex items-center justify-between p-4 hover:bg-slate-50 dark:hover:bg-slate-800/50 transition-colors">
            <div>
              <div className="font-medium">{c.code} {!c.isActive && <span className="text-xs text-rose-500">(vô hiệu)</span>}</div>
              <div className="text-xs muted">
                {c.type === "Percentage" ? `${c.value}%` : `$${c.value}`} · Đơn tối thiểu ${c.minOrderAmount} ·
                {" "}Đã dùng {c.timesUsed}{c.maxUses ? `/${c.maxUses}` : ""}
                {c.expiresAt && ` · HSD ${new Date(c.expiresAt).toLocaleDateString("vi-VN")}`}
              </div>
            </div>
            <button onClick={() => deleteCoupon.mutate(c.id)} className="text-sm text-rose-500 hover:underline">Xóa</button>
          </div>
        ))}
      </Card>
    </div>
  );
}
