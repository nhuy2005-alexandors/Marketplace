import { useState } from "react";
import { ChevronDown, ChevronRight, Store } from "lucide-react";
import { useOrderSplit } from "../api/hooks";
import { Spinner } from "./ui";

// Hiển thị chia tiền theo từng seller; chỉ hữu ích khi đơn có nhiều người bán.
export function OrderSplitPanel({ orderId }: { orderId: number }) {
  const [open, setOpen] = useState(false);
  const { data, isLoading } = useOrderSplit(orderId, open);

  return (
    <div className="mt-3">
      <button
        onClick={() => setOpen((o) => !o)}
        className="inline-flex items-center gap-1 text-xs muted hover:text-brand-600 dark:hover:text-brand-400 transition-colors"
      >
        {open ? <ChevronDown className="w-3.5 h-3.5" aria-hidden /> : <ChevronRight className="w-3.5 h-3.5" aria-hidden />}
        {open ? "Ẩn chia tiền theo cửa hàng" : "Xem chia tiền theo cửa hàng"}
      </button>
      {open && (
        <div className="mt-2 rounded-xl border border-slate-200 dark:border-slate-800 p-3 space-y-2">
          {isLoading && <Spinner label="Đang tính..." />}
          {data?.sellers.map((s) => (
            <div key={s.sellerId} className="text-sm">
              <div className="flex items-center justify-between font-medium">
                <span className="inline-flex items-center gap-1"><Store className="w-3.5 h-3.5" aria-hidden /> {s.shopName}</span>
                <span className="text-brand-600 dark:text-brand-400">${s.netTotal.toFixed(2)}</span>
              </div>
              <div className="flex justify-between muted text-xs">
                <span>Tạm tính ${s.subtotal.toFixed(2)}</span>
                {s.discountShare > 0 && <span>Giảm -${s.discountShare.toFixed(2)}</span>}
              </div>
            </div>
          ))}
        </div>
      )}
    </div>
  );
}
