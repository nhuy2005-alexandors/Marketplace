import { useState } from "react";
import { Store } from "lucide-react";
import { useApproveSeller, useSellerApplications } from "../api/hooks";
import { Button, Card, EmptyState, PageHeader, Spinner } from "../components/ui";

type FilterStatus = "Pending" | "Approved" | undefined;

const TABS: { label: string; value: FilterStatus }[] = [
  { label: "Chờ duyệt", value: "Pending" },
  { label: "Đã duyệt", value: "Approved" },
  { label: "Tất cả", value: undefined },
];

export function AdminSellersPage() {
  const [filter, setFilter] = useState<FilterStatus>("Pending");
  const { data, isLoading } = useSellerApplications(filter);
  const approveSeller = useApproveSeller();

  return (
    <div className="max-w-4xl mx-auto px-4 py-6 animate-fade-in">
      <PageHeader title="Duyệt người bán" subtitle="Xét duyệt tài khoản seller mới đăng ký." />

      <div className="flex gap-2 mb-4">
        {TABS.map((tab) => (
          <button
            key={tab.label}
            onClick={() => setFilter(tab.value)}
            className={`px-3 py-1.5 rounded-lg text-sm transition ${
              filter === tab.value
                ? "bg-brand-600 text-white shadow-glow"
                : "surface hover:bg-slate-100 dark:hover:bg-slate-800"
            }`}
          >
            {tab.label}
          </button>
        ))}
      </div>

      {isLoading ? (
        <Spinner />
      ) : data?.length === 0 ? (
        <EmptyState icon={Store} title="Không có seller" hint="Chưa có đơn đăng ký seller nào ở trạng thái này." />
      ) : (
        <div className="space-y-3">
          {data?.map((app) => (
            <Card key={app.id} className="p-4 flex items-center justify-between gap-3">
              <div className="flex-1">
                <div className="flex items-center gap-2">
                  <span className="font-medium text-sm">{app.shopName || app.fullName}</span>
                  <span
                    className={`px-2 py-0.5 rounded-full text-xs ${
                      app.status === "Pending"
                        ? "bg-amber-100 text-amber-700 dark:bg-amber-500/10 dark:text-amber-300"
                        : "bg-emerald-100 text-emerald-700 dark:bg-emerald-500/10 dark:text-emerald-300"
                    }`}
                  >
                    {app.status === "Pending" ? "Chờ duyệt" : "Đã duyệt"}
                  </span>
                </div>
                <div className="text-xs muted mt-0.5">{app.fullName} · {app.email}</div>
                <div className="text-xs muted">{new Date(app.createdAt).toLocaleDateString("vi-VN")}</div>
              </div>
              {app.status === "Pending" && (
                <Button
                  disabled={approveSeller.isPending}
                  onClick={() => approveSeller.mutate(app.id)}
                  className="text-sm py-1.5"
                >
                  Duyệt
                </Button>
              )}
            </Card>
          ))}
        </div>
      )}
    </div>
  );
}
