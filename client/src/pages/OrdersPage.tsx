import { useState } from "react";
import { useSearchParams } from "react-router-dom";
import { useCancelOrder, useOrders, useUpdateOrderStatus } from "../api/hooks";
import { useAuth } from "../store/auth";
import { StatusBadge } from "../components/StatusBadge";
import type { OrderStatus } from "../types";

const NEXT_STATUS: Partial<Record<OrderStatus, OrderStatus>> = {
  Paid: "Shipped",
  Shipped: "Delivered",
};

export function OrdersPage() {
  const [page, setPage] = useState(1);
  const [params] = useSearchParams();
  const paymentResult = params.get("payment"); // success | failed (redirect từ VNPay/Stripe)
  const { data, isLoading } = useOrders(page, 5);
  const isAdmin = useAuth((s) => s.user?.role === "Admin");
  const updateStatus = useUpdateOrderStatus();
  const cancelOrder = useCancelOrder();

  if (isLoading) return <div className="text-center py-12 text-slate-400">Đang tải...</div>;

  return (
    <div className="max-w-4xl mx-auto px-4 py-6">
      <h1 className="text-xl font-bold mb-4">{isAdmin ? "Tất cả đơn hàng" : "Đơn hàng của tôi"}</h1>

      {paymentResult === "success" && (
        <div className="mb-4 p-3 rounded-lg bg-emerald-50 text-emerald-700 text-sm">Thanh toán thành công!</div>
      )}
      {paymentResult === "failed" && (
        <div className="mb-4 p-3 rounded-lg bg-rose-50 text-rose-600 text-sm">Thanh toán thất bại, vui lòng thử lại.</div>
      )}

      <div className="space-y-4">
        {data?.items.length === 0 && <div className="text-slate-400">Chưa có đơn hàng.</div>}
        {data?.items.map((order) => (
          <div key={order.id} className="bg-white rounded-xl shadow-sm border border-slate-100 p-5">
            <div className="flex items-center justify-between mb-3">
              <div>
                <span className="font-semibold">Đơn #{order.id}</span>
                <span className="text-xs text-slate-400 ml-3">
                  {new Date(order.createdAt).toLocaleString("vi-VN")}
                </span>
              </div>
              <StatusBadge status={order.status} />
            </div>
            <div className="text-sm text-slate-500 mb-2">Giao tới: {order.shippingAddress}</div>
            <div className="divide-y border-y">
              {order.items.map((it) => (
                <div key={it.productId} className="flex justify-between py-2 text-sm">
                  <span>{it.productName} × {it.quantity}</span>
                  <span>${it.subtotal.toFixed(2)}</span>
                </div>
              ))}
            </div>
            <div className="text-sm text-slate-500 mt-2 space-y-0.5">
              <div className="flex justify-between"><span>Tạm tính</span><span>${order.subtotal.toFixed(2)}</span></div>
              {order.discountAmount > 0 && (
                <div className="flex justify-between text-emerald-600">
                  <span>Giảm giá {order.couponCode ? `(${order.couponCode})` : ""}</span>
                  <span>-${order.discountAmount.toFixed(2)}</span>
                </div>
              )}
            </div>
            <div className="flex items-center justify-between mt-3">
              <span className="font-bold text-brand-700">Tổng: ${order.total.toFixed(2)}</span>
              <div className="flex gap-2">
                {!isAdmin && (order.status === "Pending" || order.status === "Paid") && (
                  <button
                    onClick={() => cancelOrder.mutate(order.id)}
                    className="px-3 py-1.5 text-sm rounded-lg border text-rose-500 hover:bg-rose-50"
                  >Hủy đơn</button>
                )}
                {isAdmin && NEXT_STATUS[order.status] && (
                  <button
                    onClick={() => updateStatus.mutate({ id: order.id, status: NEXT_STATUS[order.status]! })}
                    className="px-3 py-1.5 text-sm rounded-lg bg-brand-600 text-white hover:bg-brand-700"
                  >→ {NEXT_STATUS[order.status]}</button>
                )}
              </div>
            </div>
          </div>
        ))}
      </div>

      {data && data.totalPages > 1 && (
        <div className="flex justify-center gap-2 mt-6">
          {Array.from({ length: data.totalPages }, (_, i) => i + 1).map((p) => (
            <button
              key={p} onClick={() => setPage(p)}
              className={`w-9 h-9 rounded-lg text-sm ${p === data.page ? "bg-brand-600 text-white" : "bg-white border"}`}
            >{p}</button>
          ))}
        </div>
      )}
    </div>
  );
}
