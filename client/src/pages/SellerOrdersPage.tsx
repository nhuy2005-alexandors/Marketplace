import { useState } from "react";
import { useSellerOrders, useUpdateItemStatus } from "../api/hooks";
import { StatusBadge } from "../components/StatusBadge";
import type { FulfillmentStatus } from "../types";

const NEXT_ITEM: Partial<Record<FulfillmentStatus, { to: FulfillmentStatus; label: string }>> = {
  Pending: { to: "Shipped", label: "Đánh dấu đã gửi" },
  Shipped: { to: "Delivered", label: "Đánh dấu đã giao" },
};

export function SellerOrdersPage() {
  const [page, setPage] = useState(1);
  const { data, isLoading } = useSellerOrders(page, 5);
  const updateItem = useUpdateItemStatus();

  if (isLoading) return <div className="text-center py-12 text-slate-400">Đang tải...</div>;

  return (
    <div className="max-w-4xl mx-auto px-4 py-6">
      <h1 className="text-xl font-bold mb-1">Đơn hàng có sản phẩm của tôi</h1>
      <p className="text-sm text-slate-400 mb-4">Chỉ hiển thị các sản phẩm thuộc cửa hàng bạn trong mỗi đơn.</p>

      <div className="space-y-4">
        {data?.items.length === 0 && <div className="text-slate-400">Chưa có đơn nào.</div>}
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
              {order.items.map((it) => {
                const next = NEXT_ITEM[it.status];
                const canFulfill = order.status !== "Cancelled" && next;
                return (
                  <div key={it.productId} className="flex items-center justify-between py-2 text-sm gap-2">
                    <span className="flex-1">{it.productName} × {it.quantity}</span>
                    <StatusBadge status={it.status} />
                    <span className="w-16 text-right">${it.subtotal.toFixed(2)}</span>
                    {canFulfill && (
                      <button
                        onClick={() => updateItem.mutate({ itemId: it.id, status: next!.to })}
                        className="px-2 py-1 text-xs rounded-lg bg-brand-600 text-white hover:bg-brand-700 whitespace-nowrap"
                      >{next!.label}</button>
                    )}
                  </div>
                );
              })}
            </div>
            <div className="mt-3 font-bold text-brand-700">
              Doanh thu của bạn từ đơn này: ${order.total.toFixed(2)}
              {order.discountAmount > 0 && (
                <span className="text-xs font-normal text-emerald-600 ml-2">
                  (đã trừ giảm giá ${order.discountAmount.toFixed(2)})
                </span>
              )}
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
