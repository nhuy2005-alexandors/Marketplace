import { useSellerDashboard } from "../api/hooks";
import { StatusBadge } from "../components/StatusBadge";
import { PageHeader, Spinner } from "../components/ui";
import { useAuth } from "../store/auth";

function StatCard({ icon, label, value, accent }: { icon: string; label: string; value: string | number; accent: string }) {
  return (
    <div className="surface rounded-2xl p-5 relative overflow-hidden hover:-translate-y-0.5 hover:shadow-card transition-all duration-200">
      <div className={`absolute inset-x-0 top-0 h-1 bg-gradient-to-r ${accent}`} />
      <div className="flex items-center justify-between gap-3">
        <div>
          <div className="muted text-sm">{label}</div>
          <div className="text-2xl font-bold mt-1">{value}</div>
        </div>
        <div className={`w-11 h-11 rounded-xl grid place-items-center text-lg text-white shadow-sm bg-gradient-to-br ${accent}`}>
          {icon}
        </div>
      </div>
    </div>
  );
}

export function SellerDashboardPage() {
  const { data, isLoading } = useSellerDashboard();
  const shopName = useAuth((s) => s.user?.shopName);

  if (isLoading || !data) return <Spinner />;

  return (
    <div className="max-w-5xl mx-auto px-4 py-6 animate-fade-in">
      <PageHeader title="Bảng điều khiển người bán" subtitle={`Cửa hàng: ${shopName}`} />

      <div className="grid grid-cols-2 md:grid-cols-4 gap-4 mb-8">
        <StatCard icon="💰" label="Doanh thu" value={`$${data.totalRevenue.toFixed(2)}`} accent="from-brand-500 to-brand-600" />
        <StatCard icon="🧾" label="Đơn liên quan" value={data.totalOrders} accent="from-blue-500 to-blue-600" />
        <StatCard icon="📦" label="Sản phẩm" value={data.totalProducts} accent="from-amber-500 to-amber-600" />
        <StatCard icon="👥" label="Khách hàng (toàn sàn)" value={data.totalCustomers} accent="from-emerald-500 to-emerald-600" />
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
