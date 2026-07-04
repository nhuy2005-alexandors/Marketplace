import { useState } from "react";
import { Receipt } from "lucide-react";
import { useSellerOrders, useUpdateItemStatus } from "../api/hooks";
import { StatusBadge } from "../components/StatusBadge";
import { Card, EmptyState, PageHeader, Spinner } from "../components/ui";
import type { FulfillmentStatus } from "../types";

const NEXT_ITEM: Partial<Record<FulfillmentStatus, { to: FulfillmentStatus; label: string }>> = {
  Pending: { to: "Shipped", label: "Đánh dấu đã gửi" },
  Shipped: { to: "Delivered", label: "Đánh dấu đã giao" },
};

export function SellerOrdersPage() {
  const [page, setPage] = useState(1);
  const { data, isLoading } = useSellerOrders(page, 5);
  const updateItem = useUpdateItemStatus();

  if (isLoading) return <Spinner />;

  return (
    <div className="max-w-4xl mx-auto px-4 py-6 animate-fade-in">
      <PageHeader title="Đơn hàng có sản phẩm của tôi" subtitle="Chỉ hiển thị các sản phẩm thuộc cửa hàng bạn trong mỗi đơn." />

      <div className="space-y-4">
        {data?.items.length === 0 && <EmptyState icon={Receipt} title="Chưa có đơn nào." />}
        {data?.items.map((order) => (
          <Card key={order.id} className="p-5">
            <div className="flex items-center justify-between mb-3">
              <div>
                <span className="font-semibold">Đơn #{order.id}</span>
                <span className="text-xs muted ml-3">
                  {new Date(order.createdAt).toLocaleString("vi-VN")}
                </span>
              </div>
              <StatusBadge status={order.status} />
            </div>
            <div className="text-sm muted mb-2">Giao tới: {order.shippingAddress}</div>
            <div className="divide-y divide-slate-200 dark:divide-slate-800 border-y border-slate-200 dark:border-slate-800">
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
                        className="btn-primary px-2 py-1 text-xs whitespace-nowrap"
                      >{next!.label}</button>
                    )}
                  </div>
                );
              })}
            </div>
            <div className="mt-3 font-bold text-brand-600 dark:text-brand-400">
              Doanh thu của bạn từ đơn này: ${order.total.toFixed(2)}
              {order.discountAmount > 0 && (
                <span className="text-xs font-normal text-emerald-600 dark:text-emerald-400 ml-2">
                  (đã trừ giảm giá ${order.discountAmount.toFixed(2)})
                </span>
              )}
            </div>
          </Card>
        ))}
      </div>

      {data && data.totalPages > 1 && (
        <div className="flex justify-center gap-2 mt-6">
          {Array.from({ length: data.totalPages }, (_, i) => i + 1).map((p) => (
            <button
              key={p} onClick={() => setPage(p)}
              className={`w-9 h-9 rounded-lg text-sm transition-colors ${
                p === data.page
                  ? "bg-brand-600 text-white"
                  : "surface hover:bg-slate-100 dark:hover:bg-slate-800"
              }`}
            >{p}</button>
          ))}
        </div>
      )}
    </div>
  );
}
