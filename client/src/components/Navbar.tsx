import { Link, NavLink, useNavigate } from "react-router-dom";
import { useAuth } from "../store/auth";
import { useTheme } from "../store/theme";
import { useCart } from "../api/hooks";

function navClass({ isActive }: { isActive: boolean }) {
  return `text-sm transition-colors ${
    isActive
      ? "text-brand-600 dark:text-brand-400 font-medium"
      : "text-slate-600 dark:text-slate-300 hover:text-brand-600 dark:hover:text-brand-400"
  }`;
}

export function Navbar() {
  const { user, token, logout } = useAuth();
  const { theme, toggle } = useTheme();
  const navigate = useNavigate();
  const { data: cart } = useCart();
  const cartCount = cart?.items.reduce((n, i) => n + i.quantity, 0) ?? 0;

  return (
    <nav className="sticky top-0 z-20 backdrop-blur-lg bg-white/80 dark:bg-slate-950/80 border-b border-slate-200/70 dark:border-slate-800">
      <div className="max-w-6xl mx-auto px-4 h-16 flex items-center gap-6">
        <Link to="/" className="flex items-center gap-2 font-bold text-lg">
          <span className="grid place-items-center w-8 h-8 rounded-xl bg-gradient-to-br from-brand-500 to-brand-700 text-white text-sm shadow-glow">🛍️</span>
          <span className="bg-gradient-to-r from-brand-600 to-brand-400 bg-clip-text text-transparent">MiniShop</span>
        </Link>
        <NavLink to="/" className={navClass} end>Sản phẩm</NavLink>
        {user?.role === "Customer" && <NavLink to="/orders" className={navClass}>Đơn hàng</NavLink>}
        {user?.role === "Customer" && <NavLink to="/wishlist" className={navClass}>Yêu thích</NavLink>}
        {user?.role === "Admin" && (
          <>
            <NavLink to="/admin" className={navClass} end>Dashboard</NavLink>
            <NavLink to="/admin/products" className={navClass}>SP/Danh mục</NavLink>
            <NavLink to="/admin/coupons" className={navClass}>Mã giảm giá</NavLink>
          </>
        )}
        {user?.role === "Seller" && (
          <>
            <NavLink to="/seller" className={navClass} end>Dashboard</NavLink>
            <NavLink to="/seller/products" className={navClass}>SP của tôi</NavLink>
            <NavLink to="/seller/orders" className={navClass}>Đơn hàng</NavLink>
          </>
        )}
        <div className="ml-auto flex items-center gap-3">
          <button
            onClick={toggle} aria-label="Đổi giao diện sáng/tối"
            className="grid place-items-center w-9 h-9 rounded-lg border border-slate-200 dark:border-slate-700 text-slate-600 dark:text-slate-300 hover:bg-slate-100 dark:hover:bg-slate-800 transition"
          >{theme === "dark" ? "☀️" : "🌙"}</button>
          {user?.role === "Customer" && (
            <Link to="/cart" className="relative grid place-items-center w-9 h-9 rounded-lg text-slate-600 dark:text-slate-300 hover:bg-slate-100 dark:hover:bg-slate-800 transition">
              🛒
              {cartCount > 0 && (
                <span className="absolute -top-1 -right-1 bg-brand-600 text-white text-[10px] rounded-full min-w-[18px] h-[18px] px-1 flex items-center justify-center font-medium">
                  {cartCount}
                </span>
              )}
            </Link>
          )}
          {token ? (
            <div className="flex items-center gap-3 text-sm">
              <span className="muted hidden sm:block">{user?.fullName}</span>
              <button
                onClick={() => { logout(); navigate("/"); }}
                className="text-rose-500 hover:text-rose-600 dark:hover:text-rose-400 transition"
              >Đăng xuất</button>
            </div>
          ) : (
            <Link to="/login" className="btn-primary">Đăng nhập</Link>
          )}
        </div>
      </div>
    </nav>
  );
}
