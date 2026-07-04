import { useState } from "react";
import { Check, Copy, Ticket } from "lucide-react";
import type { Coupon } from "../../types";

function discountLabel(c: Coupon) {
  return c.type === "Percentage" ? `Giảm ${c.value}%` : `Giảm $${c.value.toFixed(0)}`;
}

function VoucherCard({ coupon }: { coupon: Coupon }) {
  const [copied, setCopied] = useState(false);
  const copy = () => {
    navigator.clipboard.writeText(coupon.code).then(() => {
      setCopied(true);
      setTimeout(() => setCopied(false), 1600);
    }).catch(() => {});
  };
  return (
    <div className="relative shrink-0 w-64 flex rounded-2xl overflow-hidden border border-dashed border-brand-300 dark:border-brand-800 bg-brand-50 dark:bg-brand-900/20">
      {/* left ticket stub */}
      <div className="grid place-items-center w-16 bg-brand-600 text-white shrink-0">
        <Ticket className="w-6 h-6" aria-hidden />
      </div>
      <div className="flex-1 p-3 min-w-0">
        <div className="font-bold text-brand-700 dark:text-brand-300 truncate">{discountLabel(coupon)}</div>
        <div className="text-xs muted">Đơn tối thiểu ${coupon.minOrderAmount.toFixed(0)}</div>
        <button
          onClick={copy}
          className="mt-2 inline-flex items-center gap-1.5 rounded-lg bg-white dark:bg-slate-800 border border-brand-200 dark:border-brand-700 px-2.5 py-1 text-xs font-mono font-medium hover:bg-brand-100 dark:hover:bg-brand-900/40 transition"
        >
          {copied ? <Check className="w-3.5 h-3.5 text-emerald-500" aria-hidden /> : <Copy className="w-3.5 h-3.5" aria-hidden />}
          {copied ? "Đã chép" : coupon.code}
        </button>
      </div>
    </div>
  );
}

export function VoucherStrip({ coupons }: { coupons: Coupon[] }) {
  if (coupons.length === 0) return null;
  return (
    <section>
      <div className="flex items-center gap-2 mb-4">
        <Ticket className="w-5 h-5 text-brand-600 dark:text-brand-400" aria-hidden />
        <h2 className="text-lg font-bold">Mã giảm giá</h2>
      </div>
      <div className="flex gap-3 overflow-x-auto pb-2">
        {coupons.map((c) => <VoucherCard key={c.id} coupon={c} />)}
      </div>
    </section>
  );
}
