import { Link } from "react-router-dom";
import { CreditCard, Headphones, ShoppingBag, Truck, Wallet } from "lucide-react";
import { useCategories } from "../api/hooks";

const FEATURES = [
  { icon: ShoppingBag, title: "Nhận tại cửa hàng", desc: "Dịch vụ 24/7" },
  { icon: Truck, title: "Miễn phí vận chuyển", desc: "Đơn từ $50" },
  { icon: Wallet, title: "Thanh toán linh hoạt", desc: "Nhiều phương thức" },
  { icon: Headphones, title: "Hỗ trợ tận tâm", desc: "Luôn sẵn sàng" },
];

// Chỉ trỏ tới route thật trong ứng dụng.
const SHOP_LINKS = [
  { label: "Tất cả sản phẩm", to: "/products" },
  { label: "Giỏ hàng", to: "/cart" },
  { label: "Yêu thích", to: "/wishlist" },
  { label: "Đơn hàng của tôi", to: "/orders" },
];

const ACCOUNT_LINKS = [
  { label: "Đăng nhập", to: "/login" },
  { label: "Đơn hàng", to: "/orders" },
  { label: "Kênh người bán", to: "/seller" },
];

function LinkList({ items }: { items: { label: string; to: string }[] }) {
  return (
    <ul className="space-y-2">
      {items.map((l) => (
        <li key={l.label}>
          <Link to={l.to} className="text-sm muted hover:text-brand-600 dark:hover:text-brand-400 transition-colors">{l.label}</Link>
        </li>
      ))}
    </ul>
  );
}

export function Footer() {
  const { data: categories } = useCategories();
  const catLinks = (categories ?? []).slice(0, 4).map((c) => ({
    label: c.name,
    to: `/products?categoryId=${c.id}`,
  }));

  return (
    <footer className="mt-12 border-t border-slate-200 dark:border-slate-800">
      <div className="max-w-7xl mx-auto px-4 py-10 space-y-10">
        {/* feature strip */}
        <div className="grid grid-cols-2 md:grid-cols-4 gap-4">
          {FEATURES.map((f) => (
            <div key={f.title} className="surface rounded-2xl p-4 flex items-center gap-3">
              <span className="grid place-items-center w-11 h-11 rounded-xl bg-brand-50 dark:bg-brand-900/30 text-brand-600 dark:text-brand-400 shrink-0">
                <f.icon className="w-5 h-5" aria-hidden />
              </span>
              <div>
                <div className="text-sm font-semibold">{f.title}</div>
                <div className="text-xs muted">{f.desc}</div>
              </div>
            </div>
          ))}
        </div>

        {/* link columns */}
        <div className="grid grid-cols-2 md:grid-cols-3 gap-8">
          <div>
            <div className="font-semibold text-sm mb-3">Mua sắm</div>
            <LinkList items={SHOP_LINKS} />
          </div>
          <div>
            <div className="font-semibold text-sm mb-3">Tài khoản</div>
            <LinkList items={ACCOUNT_LINKS} />
          </div>
          {catLinks.length > 0 && (
            <div>
              <div className="font-semibold text-sm mb-3">Danh mục</div>
              <LinkList items={catLinks} />
            </div>
          )}
        </div>

        {/* bottom bar */}
        <div className="flex flex-col md:flex-row items-center justify-between gap-4 pt-6 border-t border-slate-200 dark:border-slate-800 text-xs muted">
          <div className="flex items-center gap-2">
            <span className="grid place-items-center w-6 h-6 rounded-lg bg-gradient-to-br from-brand-500 to-brand-700 text-white"><ShoppingBag className="w-3.5 h-3.5" aria-hidden /></span>
            © {new Date().getFullYear()} MiniShop. Đã đăng ký bản quyền.
          </div>
          <div className="flex items-center gap-2">
            <CreditCard className="w-4 h-4" aria-hidden />
            <span>MoMo · Visa · Mastercard</span>
          </div>
        </div>
      </div>
    </footer>
  );
}
