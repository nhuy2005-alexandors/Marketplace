import { useSellerDashboard } from "../api/hooks";
import { StatusBadge } from "../components/StatusBadge";
import { useAuth } from "../store/auth";

function StatCard({ label, value }: { label: string; value: string | number }) {
  return (
    <div className="bg-white rounded-xl shadow-sm border border-slate-100 p-5">
      <div className="text-sm text-slate-400">{label}</div>
      <div className="text-2xl font-bold text-brand-700 mt-1">{value}</div>
    </div>
  );
}

export function SellerDashboardPage() {
  const { data, isLoading } = useSellerDashboard();
  const shopName = useAuth((s) => s.user?.shopName);

  if (isLoading || !data) return <div className="text-center py-12 text-slate-400">Đang tải...</div>;

  return (
    <div className="max-w-5xl mx-auto px-4 py-6">
      <h1 className="text-xl font-bold mb-1">Bảng điều khiển người bán</h1>
      <p className="text-sm text-slate-400 mb-4">Cửa hàng: {shopName}</p>

      <div className="grid grid-cols-2 md:grid-cols-4 gap-4 mb-8">
        <StatCard label="Doanh thu" value={`$${data.totalRevenue.toFixed(2)}`} />
        <StatCard label="Đơn liên quan" value={data.totalOrders} />
        <StatCard label="Sản phẩm" value={data.totalProducts} />
        <StatCard label="Khách hàng (toàn sàn)" value={data.totalCustomers} />
      </div>

      <div className="grid md:grid-cols-2 gap-6">
        <div className="bg-white rounded-xl shadow-sm border border-slate-100 p-5">
          <h2 className="font-semibold mb-3">Đơn theo trạng thái</h2>
          {data.ordersByStatus.length === 0 && <div className="text-slate-400 text-sm">Chưa có dữ liệu.</div>}
          <div className="space-y-2">
            {data.ordersByStatus.map((s) => (
              <div key={s.status} className="flex items-center justify-between">
                <StatusBadge status={s.status} />
                <span className="font-medium">{s.count}</span>
              </div>
            ))}
          </div>
        </div>

        <div className="bg-white rounded-xl shadow-sm border border-slate-100 p-5">
          <h2 className="font-semibold mb-3">Top sản phẩm bán chạy</h2>
          {data.topProducts.length === 0 && <div className="text-slate-400 text-sm">Chưa có dữ liệu.</div>}
          <div className="space-y-2">
            {data.topProducts.map((p) => (
              <div key={p.productId} className="flex items-center justify-between text-sm">
                <span>{p.productName}</span>
                <span className="text-slate-500">{p.unitsSold} bán · ${p.revenue.toFixed(2)}</span>
              </div>
            ))}
          </div>
        </div>
      </div>
    </div>
  );
}
