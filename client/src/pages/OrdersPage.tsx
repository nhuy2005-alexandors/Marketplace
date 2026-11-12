import { useState } from "react";
import { Package } from "lucide-react";
import { useSearchParams } from "react-router-dom";
import { useCancelOrder, useMyRefunds, useOrders, useRequestRefund, useUpdateOrderStatus } from "../api/hooks";
import { useAuth } from "../store/auth";
import { apiError } from "../api/client";
import { StatusBadge } from "../components/StatusBadge";
import { OrderSplitPanel } from "../components/OrderSplitPanel";
import type { OrderStatus } from "../types";
import { Button, Card, EmptyState, PageHeader, Spinner, Textarea } from "../components/ui";

const NEXT_STATUS: Partial<Record<OrderStatus, OrderStatus>> = {
  Paid: "Shipped",
  Shipped: "Delivered",
};

export function OrdersPage() {
  const [page, setPage] = useState(1);
  const [params] = useSearchParams();
  const paymentResult = params.get("payment"); // success | failed (redirect từ MoMo)
  const { data, isLoading } = useOrders(page, 5);
  const { data: refundData, isLoading: refundsLoading } = useMyRefunds();
  const isAdmin = useAuth((s) => s.user?.role === "Admin");
  const updateStatus = useUpdateOrderStatus();
  const cancelOrder = useCancelOrder();
  const requestRefund = useRequestRefund();
  const [refundOrderId, setRefundOrderId] = useState<number | null>(null);
  const [refundReason, setRefundReason] = useState("");
  const [refundError, setRefundError] = useState("");

  const submitRefund = async (orderId: number) => {
    setRefundError("");
    try {
      await requestRefund.mutateAsync({ orderId, reason: refundReason.trim() });
      setRefundOrderId(null);
      setRefundReason("");
    } catch (error) {
      setRefundError(apiError(error));
    }
  };

  if (isLoading || refundsLoading) return <Spinner />;

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
                {!isAdmin && (order.status === "Pending" ||
                  (order.status === "Paid" && order.payment?.method === "CashOnDelivery")) && (
                  <Button variant="danger" className="text-sm py-1.5" onClick={() => cancelOrder.mutate(order.id)}>Hủy đơn</Button>
                )}
                {!isAdmin && order.status === "Paid" &&
                  order.payment?.status === "Completed" &&
                  order.payment?.method !== "CashOnDelivery" &&
                  order.items.length > 0 &&
                  order.items.every((item) => item.status === "Pending") &&
                  !refundData?.items.some((refund) => refund.orderId === order.id) && (
                  <Button className="text-sm py-1.5" onClick={() => {
                    setRefundOrderId(order.id);
                    setRefundReason("");
                    setRefundError("");
                  }}>Yêu cầu hoàn tiền</Button>
                )}
                {isAdmin && NEXT_STATUS[order.status] && (
                  <Button
                    className="text-sm py-1.5"
                    onClick={() => updateStatus.mutate({ id: order.id, status: NEXT_STATUS[order.status]! })}
                  >→ {NEXT_STATUS[order.status]}</Button>
                )}
              </div>
            </div>
            {!isAdmin && refundData?.items.filter((refund) => refund.orderId === order.id).map((refund) => (
              <div key={refund.id} className="mt-3 rounded-xl bg-slate-50 dark:bg-slate-800/60 p-3 text-sm">
                <div className="font-medium">Yêu cầu hoàn tiền: {
                  refund.status === "Pending" ? "Đang chờ xử lý" :
                  refund.status === "Completed" ? "Đã hoàn tiền" : "Đã từ chối"
                }</div>
                <div className="muted mt-1">Lý do: {refund.reason}</div>
                {refund.note && <div className="muted mt-1">Ghi chú: {refund.note}</div>}
                {refund.externalReference && <div className="muted mt-1">Mã giao dịch: {refund.externalReference}</div>}
              </div>
            ))}
            {!isAdmin && refundOrderId === order.id && (
              <form onSubmit={(e) => { e.preventDefault(); void submitRefund(order.id); }} className="mt-3 space-y-2 rounded-xl border border-slate-200 dark:border-slate-700 p-3">
                <label className="block text-sm font-medium" htmlFor={`refund-reason-${order.id}`}>Lý do hoàn tiền</label>
                <Textarea
                  id={`refund-reason-${order.id}`}
                  required
                  minLength={5}
                  maxLength={1000}
                  rows={3}
                  value={refundReason}
                  onChange={(e) => setRefundReason(e.target.value)}
                  placeholder="Mô tả lý do yêu cầu hoàn tiền..."
                />
                {refundError && <div className="text-rose-500 text-sm">{refundError}</div>}
                <div className="flex gap-2">
                  <Button type="submit" disabled={requestRefund.isPending || refundReason.trim().length < 5}>Gửi yêu cầu</Button>
                  <Button type="button" variant="ghost" onClick={() => setRefundOrderId(null)}>Đóng</Button>
                </div>
              </form>
            )}
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
