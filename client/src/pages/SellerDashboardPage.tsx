import { Clock, DollarSign, Package, Receipt, Users, type LucideIcon } from "lucide-react";
import { useMe, useSellerDashboard } from "../api/hooks";
import { StatusBadge } from "../components/StatusBadge";
import { PageHeader, Spinner } from "../components/ui";
import { useAuth } from "../store/auth";

function StatCard({ icon: Icon, label, value, accent }: { icon: LucideIcon; label: string; value: string | number; accent: string }) {
  return (
    <div className="surface rounded-2xl p-5 relative overflow-hidden hover:-translate-y-0.5 hover:shadow-card transition-all duration-200">
      <div className={`absolute inset-x-0 top-0 h-1 bg-gradient-to-r ${accent}`} />
      <div className="flex items-center justify-between gap-3">
        <div>
          <div className="muted text-sm">{label}</div>
          <div className="text-2xl font-bold mt-1">{value}</div>
        </div>
        <div className={`w-11 h-11 rounded-xl grid place-items-center text-white shadow-sm bg-gradient-to-br ${accent}`}>
          <Icon className="w-5 h-5" strokeWidth={2} aria-hidden />
        </div>
      </div>
    </div>
  );
}

export function SellerDashboardPage() {
  useMe(); // đồng bộ sellerStatus mới nhất vào store (banner tự ẩn sau khi được duyệt)
  const { data, isLoading } = useSellerDashboard();
  const shopName = useAuth((s) => s.user?.shopName);
  const sellerStatus = useAuth((s) => s.user?.sellerStatus);

  if (isLoading || !data) return <Spinner />;

  return (
    <div className="max-w-5xl mx-auto px-4 py-6 animate-fade-in">
      <PageHeader title="Bảng điều khiển người bán" subtitle={`Cửa hàng: ${shopName}`} />

      {sellerStatus === "Pending" && (
        <div className="rounded-2xl p-4 mb-6 bg-amber-50 dark:bg-amber-500/10 text-amber-700 dark:text-amber-300 text-sm border border-amber-200 dark:border-amber-500/20 flex items-start gap-2">
          <Clock className="w-4 h-4 shrink-0 mt-0.5" aria-hidden />
          <span>
            Tài khoản người bán của bạn đang chờ Admin duyệt. Bạn có thể xem bảng điều khiển nhưng chưa thể đăng sản phẩm cho đến khi được duyệt.
          </span>
        </div>
      )}

      <div className="grid grid-cols-2 md:grid-cols-4 gap-4 mb-8">
        <StatCard icon={DollarSign} label="Doanh thu" value={`$${data.totalRevenue.toFixed(2)}`} accent="from-brand-500 to-brand-600" />
        <StatCard icon={Receipt} label="Đơn liên quan" value={data.totalOrders} accent="from-blue-500 to-blue-600" />
        <StatCard icon={Package} label="Sản phẩm" value={data.totalProducts} accent="from-amber-500 to-amber-600" />
        <StatCard icon={Users} label="Khách hàng (toàn sàn)" value={data.totalCustomers} accent="from-emerald-500 to-emerald-600" />
      </div>

      <div className="grid md:grid-cols-2 gap-6">
        <div className="surface rounded-2xl p-5">
          <h2 className="font-semibold mb-3">Đơn theo trạng thái</h2>
          {data.ordersByStatus.length === 0 && <div className="muted text-sm">Chưa có dữ liệu.</div>}
          <div className="divide-y divide-slate-200 dark:divide-slate-800">
            {data.ordersByStatus.map((s) => (
              <div key={s.status} className="flex items-center justify-between py-2">
                <StatusBadge status={s.status} />
                <span className="font-medium">{s.count}</span>
              </div>
            ))}
          </div>
        </div>

        <div className="surface rounded-2xl p-5">
          <h2 className="font-semibold mb-3">Top sản phẩm bán chạy</h2>
          {data.topProducts.length === 0 && <div className="muted text-sm">Chưa có dữ liệu.</div>}
          <div className="divide-y divide-slate-200 dark:divide-slate-800">
            {data.topProducts.map((p) => (
              <div key={p.productId} className="flex items-center justify-between text-sm py-2">
                <span>{p.productName}</span>
                <span className="muted">{p.unitsSold} bán · ${p.revenue.toFixed(2)}</span>
              </div>
            ))}
          </div>
        </div>
      </div>
    </div>
  );
}
