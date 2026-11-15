import { useState } from "react";
import { useRfmCustomers } from "../api/hooks";
import { Card, Spinner } from "../components/ui";

const usd = new Intl.NumberFormat("en-US", { style: "currency", currency: "USD" });
const date = new Intl.DateTimeFormat("vi-VN", { dateStyle: "medium" });

export function AdminCustomerSegmentsPage() {
  const [page, setPage] = useState(1);
  const pageSize = 25;
  const { data, isLoading, isError, error } = useRfmCustomers(page, pageSize);
  const pageCount = data ? Math.max(1, Math.ceil(data.totalCustomers / data.pageSize)) : 1;

  return (
    <div className="max-w-7xl mx-auto px-4 py-6 space-y-6">
      <header>
        <h1 className="text-2xl font-bold">Phân loại khách hàng</h1>
        <p className="muted text-sm mt-1">
          RFM phân nhóm khách hàng theo lần mua gần nhất, tần suất và tổng giá trị mua hàng (USD).
        </p>
      </header>

      {isLoading ? <Spinner /> : null}
      {isError ? (
        <p role="alert" className="text-sm text-rose-600">
          Không tải được dữ liệu phân loại khách hàng. {error instanceof Error ? error.message : ""}
        </p>
      ) : null}

      {data ? (
        <>
          <section className="grid grid-cols-2 md:grid-cols-3 lg:grid-cols-5 gap-3" aria-label="Số lượng theo phân khúc">
            {data.segmentCounts.map((segment) => (
              <Card key={segment.segmentCode} className="p-4">
                <div className="muted text-xs">{segment.segmentLabel}</div>
                <div className="text-2xl font-bold mt-1">{segment.count}</div>
              </Card>
            ))}
          </section>

          <Card className="overflow-x-auto">
            <div className="p-4 border-b border-slate-200 dark:border-slate-800">
              <h2 className="font-semibold">Khách hàng ({data.totalCustomers})</h2>
              <p className="muted text-xs mt-1">
                Mốc dữ liệu: {date.format(new Date(data.asOfUtc))}
              </p>
            </div>
            <table className="w-full text-sm">
              <thead className="bg-slate-50 dark:bg-slate-800/60 text-left">
                <tr>
                  <th className="p-3">Customer ID</th>
                  <th className="p-3">Lần mua gần nhất</th>
                  <th className="p-3 text-right">Recency</th>
                  <th className="p-3 text-right">Frequency</th>
                  <th className="p-3 text-right">Monetary</th>
                  <th className="p-3">R/F/M</th>
                  <th className="p-3">Phân khúc</th>
                </tr>
              </thead>
              <tbody>
                {data.items.map((customer) => (
                  <tr key={customer.userId} className="border-t border-slate-100 dark:border-slate-800">
                    <td className="p-3 font-medium">#{customer.userId}</td>
                    <td className="p-3">
                      {customer.lastEligibleOrderCreatedAtUtc
                        ? date.format(new Date(customer.lastEligibleOrderCreatedAtUtc))
                        : "Chưa mua"}
                    </td>
                    <td className="p-3 text-right">{customer.recencyDays ?? "—"}</td>
                    <td className="p-3 text-right">{customer.frequency}</td>
                    <td className="p-3 text-right tabular-nums">{usd.format(customer.monetaryUsd)}</td>
                    <td className="p-3 tabular-nums">
                      {customer.rQuintile ?? "—"} / {customer.fQuintile ?? "—"} / {customer.mQuintile ?? "—"}
                    </td>
                    <td className="p-3">
                      <span className="inline-flex rounded-full bg-slate-100 dark:bg-slate-800 px-2.5 py-1 text-xs">
                        {customer.segmentLabel}
                      </span>
                    </td>
                  </tr>
                ))}
                {data.items.length === 0 && (
                  <tr><td className="p-4 muted" colSpan={7}>Không có khách hàng trong trang này.</td></tr>
                )}
              </tbody>
            </table>
            <div className="flex items-center justify-between p-4 border-t border-slate-200 dark:border-slate-800">
              <span className="muted text-xs">Trang {data.page} / {pageCount}</span>
              <div className="flex gap-2">
                <button
                  type="button"
                  disabled={page <= 1}
                  onClick={() => setPage((value) => Math.max(1, value - 1))}
                  className="rounded-lg border border-slate-300 dark:border-slate-700 px-3 py-1.5 text-xs disabled:opacity-40"
                >Trước</button>
                <button
                  type="button"
                  disabled={page >= pageCount}
                  onClick={() => setPage((value) => Math.min(pageCount, value + 1))}
                  className="rounded-lg border border-slate-300 dark:border-slate-700 px-3 py-1.5 text-xs disabled:opacity-40"
                >Sau</button>
              </div>
            </div>
          </Card>
        </>
      ) : null}
    </div>
  );
}
