import { useState } from "react";
import { useRfmCustomers } from "../api/hooks";
import { Button, Card, EmptyState, PageHeader, Select, Spinner } from "../components/ui";

const usd = new Intl.NumberFormat("en-US", {
  style: "currency",
  currency: "USD",
  maximumFractionDigits: 2,
});

function quintile(label: string, value: number | null) {
  return value == null ? "—" : label + value;
}

export function AdminRfmPage() {
  const [page, setPage] = useState(1);
  const [pageSize, setPageSize] = useState(25);
  const { data, isLoading, isError, refetch } = useRfmCustomers(page, pageSize);
  const totalPages = Math.max(1, Math.ceil((data?.totalCustomers ?? 0) / pageSize));

  return (
    <div className="max-w-7xl mx-auto px-4 py-6 animate-fade-in">
      <PageHeader
        title="Phân nhóm khách hàng RFM"
        subtitle="Phân khúc khách hàng theo Recency, Frequency và Monetary (USD)."
        actions={
          <div className="flex items-center gap-2">
            <span className="muted text-xs whitespace-nowrap">Số dòng</span>
            <Select
              className="w-24"
              value={pageSize}
              onChange={(event) => {
                setPageSize(Number(event.target.value));
                setPage(1);
              }}
            >
              <option value={25}>25</option>
              <option value={50}>50</option>
              <option value={100}>100</option>
            </Select>
          </div>
        }
      />

      {isLoading ? (
        <Spinner />
      ) : isError ? (
        <div role="alert" className="space-y-2 text-rose-500">
          <p>Không thể tải phân khúc khách hàng RFM.</p>
          <Button type="button" onClick={() => { void refetch(); }}>Thử lại</Button>
        </div>
      ) : !data || data.items.length === 0 ? (
        <EmptyState title="Chưa có dữ liệu RFM" hint="Chưa có khách hàng với đơn hàng đủ điều kiện để phân nhóm." />
      ) : (
        <>
          <div className="grid grid-cols-2 md:grid-cols-3 lg:grid-cols-5 gap-3 mb-6">
            {data.segmentCounts.map((segment) => (
              <Card key={segment.segmentCode} className="p-4">
                <div className="muted text-xs">{segment.segmentLabel}</div>
                <div className="text-2xl font-bold mt-1">{segment.count}</div>
                <div className="muted text-xs mt-1">{segment.segmentCode}</div>
              </Card>
            ))}
          </div>

          <div className="flex flex-wrap items-center justify-between gap-2 mb-3 text-xs muted">
            <span>{data.totalCustomers.toLocaleString("vi-VN")} khách hàng · Trang {data.page}</span>
            <span>As of {new Date(data.asOfUtc).toLocaleString("vi-VN", { timeZone: "UTC" })} UTC</span>
          </div>

          <Card className="overflow-hidden">
            <div className="overflow-x-auto">
              <table className="min-w-full text-sm">
                <thead className="bg-slate-50 dark:bg-slate-800/70 text-left">
                  <tr>
                    <th className="px-4 py-3 font-semibold">User ID</th>
                    <th className="px-4 py-3 font-semibold">Đơn gần nhất</th>
                    <th className="px-4 py-3 font-semibold">Recency</th>
                    <th className="px-4 py-3 font-semibold">Frequency</th>
                    <th className="px-4 py-3 font-semibold">Monetary</th>
                    <th className="px-4 py-3 font-semibold">R / F / M</th>
                    <th className="px-4 py-3 font-semibold">Segment</th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-slate-200 dark:divide-slate-800">
                  {data.items.map((customer) => (
                    <tr key={customer.userId} className="hover:bg-slate-50/70 dark:hover:bg-slate-800/40">
                      <td className="px-4 py-3 font-medium">#{customer.userId}</td>
                      <td className="px-4 py-3 whitespace-nowrap">
                        {customer.lastEligibleOrderCreatedAtUtc
                          ? new Date(customer.lastEligibleOrderCreatedAtUtc).toLocaleDateString("vi-VN", { timeZone: "UTC" })
                          : "—"}
                      </td>
                      <td className="px-4 py-3 whitespace-nowrap">
                        {customer.recencyDays == null ? "—" : customer.recencyDays + " ngày"}
                      </td>
                      <td className="px-4 py-3 tabular-nums">{customer.frequency}</td>
                      <td className="px-4 py-3 whitespace-nowrap tabular-nums">{usd.format(customer.monetaryUsd)}</td>
                      <td className="px-4 py-3 whitespace-nowrap tabular-nums">
                        {quintile("R", customer.rQuintile)} / {quintile("F", customer.fQuintile)} / {quintile("M", customer.mQuintile)}
                      </td>
                      <td className="px-4 py-3">
                        <div className="font-medium">{customer.segmentLabel}</div>
                        <div className="muted text-xs">{customer.segmentCode}</div>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          </Card>

          {totalPages > 1 && (
            <div className="flex items-center justify-center gap-3 mt-5">
              <Button variant="ghost" disabled={page <= 1} onClick={() => setPage((value) => value - 1)}>
                Trước
              </Button>
              <span className="muted text-sm tabular-nums">{data.page} / {totalPages}</span>
              <Button variant="ghost" disabled={page >= totalPages} onClick={() => setPage((value) => value + 1)}>
                Sau
              </Button>
            </div>
          )}
        </>
      )}
    </div>
  );
}
