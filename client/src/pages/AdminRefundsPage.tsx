import { useState } from "react";
import { useAdminRefunds, useCompleteRefund, useRejectRefund } from "../api/hooks";
import { apiError } from "../api/client";
import type { Refund } from "../types";
import { Button, Card, EmptyState, Input, PageHeader, Spinner, Textarea } from "../components/ui";

type FilterStatus = "Pending" | "Completed" | "Rejected" | undefined;

const TABS: { label: string; value: FilterStatus }[] = [
  { label: "Chờ xử lý", value: "Pending" },
  { label: "Đã hoàn tiền", value: "Completed" },
  { label: "Từ chối", value: "Rejected" },
  { label: "Tất cả", value: undefined },
];

function RefundCard({ refund }: { refund: Refund }) {
  const completeRefund = useCompleteRefund();
  const rejectRefund = useRejectRefund();
  const [externalReference, setExternalReference] = useState("");
  const [note, setNote] = useState("");
  const [rejectionReason, setRejectionReason] = useState("");
  const [error, setError] = useState("");

  const complete = async (e: React.FormEvent) => {
    e.preventDefault();
    setError("");
    try {
      await completeRefund.mutateAsync({
        id: refund.id,
        externalReference: externalReference.trim(),
        note: note.trim() || undefined,
      });
    } catch (err) {
      setError(apiError(err));
    }
  };

  const reject = async (e: React.FormEvent) => {
    e.preventDefault();
    setError("");
    try {
      await rejectRefund.mutateAsync({ id: refund.id, reason: rejectionReason.trim() });
    } catch (err) {
      setError(apiError(err));
    }
  };

  return (
    <Card className="p-4 space-y-3">
      <div className="flex flex-wrap items-start justify-between gap-2">
        <div>
          <div className="font-semibold">Đơn #{refund.orderId} · {refund.customerName}</div>
          <div className="text-xs muted mt-1">Yêu cầu lúc {new Date(refund.requestedAt).toLocaleString("vi-VN")}</div>
        </div>
        <div className="font-bold text-brand-600 dark:text-brand-400">{"$" + refund.amount.toFixed(2) + " USD"}</div>
      </div>
      <div className="rounded-lg bg-slate-50 dark:bg-slate-800/60 p-3 text-sm space-y-1">
        <div><span className="font-medium">Nghĩa vụ hoàn:</span> {"$" + refund.amount.toFixed(2) + " USD"}</div>
        {refund.providerAmount !== undefined && refund.providerCurrency && (
          <div><span className="font-medium">Capture gốc tại cổng:</span> {refund.providerAmount.toLocaleString("en-US", {
            minimumFractionDigits: refund.providerCurrency === "VND" ? 0 : 2,
            maximumFractionDigits: refund.providerCurrency === "VND" ? 0 : 2,
          })} {refund.providerCurrency}</div>
        )}
        {refund.paymentMethod && <div><span className="font-medium">Phương thức:</span> {refund.paymentMethod}</div>}
        {refund.paymentTransactionId && <div><span className="font-medium">Mã giao dịch thanh toán:</span> {refund.paymentTransactionId}</div>}
        <p className="text-xs muted pt-1">Đối chiếu capture gốc và khoản chuyển tiền trên hệ thống của nhà cung cấp/ngân hàng. Không quy đổi capture lịch sử bằng tỷ giá hiện tại.</p>
      </div>
      <div className="text-sm"><span className="font-medium">Lý do:</span> {refund.reason}</div>
      {refund.status !== "Pending" && (
        <div className="rounded-lg bg-slate-50 dark:bg-slate-800/60 p-3 text-sm">
          <div className="font-medium">{refund.status === "Completed" ? "Đã ghi nhận hoàn tiền" : "Yêu cầu bị từ chối"}</div>
          {refund.processedAt && <div className="muted mt-1">Xử lý lúc {new Date(refund.processedAt).toLocaleString("vi-VN")}</div>}
          {refund.externalReference && <div className="muted mt-1">Mã giao dịch: {refund.externalReference}</div>}
          {refund.note && <div className="muted mt-1">Ghi chú: {refund.note}</div>}
          {refund.processedByName && <div className="muted mt-1">Người xử lý: {refund.processedByName}</div>}
        </div>
      )}
      {refund.status === "Pending" && (
        <div className="grid gap-3 md:grid-cols-2">
          <form onSubmit={complete} className="space-y-2 rounded-xl border border-slate-200 dark:border-slate-700 p-3">
            <div className="text-sm font-medium">Xác nhận đã chuyển tiền</div>
            <Input
              required
              minLength={3}
              maxLength={100}
              value={externalReference}
              onChange={(e) => setExternalReference(e.target.value)}
              placeholder="Mã giao dịch / chứng từ chuyển tiền"
            />
            <Textarea
              rows={2}
              maxLength={1000}
              value={note}
              onChange={(e) => setNote(e.target.value)}
              placeholder="Ghi chú (không bắt buộc)"
            />
            <Button type="submit" disabled={completeRefund.isPending || externalReference.trim().length < 3}>
              Xác nhận hoàn tiền
            </Button>
          </form>
          <form onSubmit={reject} className="space-y-2 rounded-xl border border-slate-200 dark:border-slate-700 p-3">
            <div className="text-sm font-medium">Từ chối yêu cầu</div>
            <Textarea
              required
              minLength={5}
              maxLength={1000}
              rows={3}
              value={rejectionReason}
              onChange={(e) => setRejectionReason(e.target.value)}
              placeholder="Lý do từ chối..."
            />
            <Button type="submit" variant="danger" disabled={rejectRefund.isPending || rejectionReason.trim().length < 5}>
              Từ chối
            </Button>
          </form>
        </div>
      )}
      {error && <div className="text-rose-500 text-sm">{error}</div>}
      <div className="text-xs muted">Admin chỉ xác nhận hoàn tất sau khi đã thực hiện chuyển tiền bên ngoài hệ thống.</div>
    </Card>
  );
}

export function AdminRefundsPage() {
  const [filter, setFilter] = useState<FilterStatus>("Pending");
  const [page, setPage] = useState(1);
  const { data, isLoading, isError, refetch } = useAdminRefunds(filter, page, 20);

  return (
    <div className="max-w-4xl mx-auto px-4 py-6 animate-fade-in">
      <PageHeader title="Yêu cầu hoàn tiền" subtitle="Kiểm tra yêu cầu và ghi nhận kết quả hoàn tiền thủ công." />
      <div className="flex flex-wrap gap-2 mb-4">
        {TABS.map((tab) => (
          <button
            key={tab.label}
            onClick={() => { setFilter(tab.value); setPage(1); }}
            className={`px-3 py-1.5 rounded-lg text-sm transition ${
              filter === tab.value
                ? "bg-brand-600 text-white shadow-glow"
                : "surface hover:bg-slate-100 dark:hover:bg-slate-800"
            }`}
          >{tab.label}</button>
        ))}
      </div>
      {isLoading ? (
        <Spinner />
      ) : isError ? (
        <div role="alert" className="space-y-2 text-rose-500">
          <p>Không thể tải danh sách hoàn tiền. Vui lòng thử lại.</p>
          <Button type="button" onClick={() => { void refetch(); }}>Thử lại</Button>
        </div>
      ) : data?.items.length === 0 ? (
        <EmptyState title="Không có yêu cầu hoàn tiền" hint="Không tìm thấy yêu cầu ở trạng thái này." />
      ) : (
        <div className="space-y-3">
          {data?.items.map((refund) => <RefundCard key={refund.id} refund={refund} />)}
        </div>
      )}
      {data && data.totalPages > 1 && (
        <div className="flex justify-center gap-2 mt-6">
          {Array.from({ length: data.totalPages }, (_, i) => i + 1).map((p) => (
            <button
              key={p}
              onClick={() => setPage(p)}
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
