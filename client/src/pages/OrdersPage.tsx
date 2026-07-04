import { useState } from "react";
import { Package } from "lucide-react";
import { useSearchParams } from "react-router-dom";
import { useCancelOrder, useOrders, useUpdateOrderStatus } from "../api/hooks";
import { useAuth } from "../store/auth";
import { StatusBadge } from "../components/StatusBadge";
import { OrderSplitPanel } from "../components/OrderSplitPanel";
import type { OrderStatus } from "../types";
import { Button, Card, EmptyState, PageHeader, Spinner } from "../components/ui";

const NEXT_STATUS: Partial<Record<OrderStatus, OrderStatus>> = {
  Paid: "Shipped",
  Shipped: "Delivered",
};

export function OrdersPage() {
  const [page, setPage] = useState(1);
  const [params] = useSearchParams();
  const paymentResult = params.get("payment"); // success | failed (redirect từ MoMo)
  const { data, isLoading } = useOrders(page, 5);
  const isAdmin = useAuth((s) => s.user?.role === "Admin");
  const updateStatus = useUpdateOrderStatus();
  const cancelOrder = useCancelOrder();

  if (isLoading) return <Spinner />;

  return (
    <div className="max-w-4xl mx-auto px-4 py-6 animate-fade-in">
      <PageHeader title={isAdmin ? "Tất cả đơn hàng" : "Đơn hàng của tôi"} />

      {paymentResult === "success" && (
        <div className="mb-4 p-3 rounded-xl bg-emerald-50 dark:bg-emerald-500/10 text-emerald-700 dark:text-emerald-300 text-sm">Thanh toán thành công!</div>
      )}
      {paymentResult === "failed" && (
        <div className="mb-4 p-3 rounded-xl bg-rose-50 dark:bg-rose-500/10 text-rose-600 dark:text-rose-300 text-sm">Thanh toán thất bại, vui lòng thử lại.</div>
      )}

      <div className="space-y-4">
        {data?.items.length === 0 && <EmptyState icon={Package} title="Chưa có đơn hàng" />}
        {data?.items.map((order) => (
          <Card key={order.id} className="p-5">
            <div className="flex items-center justify-between mb-3">
              <div>
                <span className="font-semibold">Đơn #{order.id}</span>
                <span className="muted text-xs ml-3">
                  {new Date(order.createdAt).toLocaleString("vi-VN")}
                </span>
              </div>
              <StatusBadge status={order.status} />
            </div>
            <div className="muted text-sm mb-2">Giao tới: {order.shippingAddress}</div>
            <div className="divide-y divide-slate-200 dark:divide-slate-800 border-y border-slate-200 dark:border-slate-800">
              {order.items.map((it) => (
                <div key={it.productId} className="flex items-center justify-between py-2 text-sm gap-2">
                  <span className="flex-1">{it.productName} × {it.quantity}</span>
                  <StatusBadge status={it.status} />
                  <span className="w-16 text-right">${it.subtotal.toFixed(2)}</span>
                </div>
              ))}
            </div>
            <div className="text-sm muted mt-2 space-y-0.5">
              <div className="flex justify-between"><span>Tạm tính</span><span>${order.subtotal.toFixed(2)}</span></div>
              {order.discountAmount > 0 && (
                <div className="flex justify-between text-emerald-600 dark:text-emerald-400">
                  <span>Giảm giá {order.couponCode ? `(${order.couponCode})` : ""}</span>
                  <span>-${order.discountAmount.toFixed(2)}</span>
                </div>
              )}
            </div>
            <OrderSplitPanel orderId={order.id} />
            <div className="flex items-center justify-between mt-3">
              <span className="font-bold text-brand-600 dark:text-brand-400">Tổng: ${order.total.toFixed(2)}</span>
              <div className="flex gap-2">
                {!isAdmin && (order.status === "Pending" || order.status === "Paid") && (
                  <Button variant="danger" className="text-sm py-1.5" onClick={() => cancelOrder.mutate(order.id)}>Hủy đơn</Button>
                )}
                {isAdmin && NEXT_STATUS[order.status] && (
                  <Button
                    className="text-sm py-1.5"
                    onClick={() => updateStatus.mutate({ id: order.id, status: NEXT_STATUS[order.status]! })}
                  >→ {NEXT_STATUS[order.status]}</Button>
                )}
              </div>
            </div>
          </Card>
        ))}
      </div>

      {data && data.totalPages > 1 && (
        <div className="flex justify-center gap-2 mt-6">
          {Array.from({ length: data.totalPages }, (_, i) => i + 1).map((p) => (
            <button
              key={p} onClick={() => setPage(p)}
              className={`w-9 h-9 rounded-lg text-sm transition ${
                p === data.page ? "bg-brand-600 text-white shadow-glow" : "surface hover:bg-slate-100 dark:hover:bg-slate-800"
              }`}
            >{p}</button>
          ))}
        </div>
      )}
    </div>
  );
}
