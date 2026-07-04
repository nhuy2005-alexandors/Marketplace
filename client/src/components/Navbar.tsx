import { useState } from "react";
import { Link, NavLink, useNavigate } from "react-router-dom";
import { MapPin, Moon, Search, ShoppingBag, ShoppingCart, Sun, User } from "lucide-react";
import { useAuth } from "../store/auth";
import { useTheme } from "../store/theme";
import { useCart, useCategories } from "../api/hooks";
import { api } from "../api/client";

function navClass({ isActive }: { isActive: boolean }) {
  return `text-sm whitespace-nowrap transition-colors ${
    isActive
      ? "text-brand-600 dark:text-brand-400 font-medium"
      : "text-slate-600 dark:text-slate-300 hover:text-brand-600 dark:hover:text-brand-400"
  }`;
}

export function Navbar() {
  const { user, token, refreshToken, logout } = useAuth();
  const { theme, toggle } = useTheme();
  const navigate = useNavigate();
  const [search, setSearch] = useState("");
  const { data: cart } = useCart();
  const { data: categories } = useCategories();
  const cartCount = cart?.items.reduce((n, i) => n + i.quantity, 0) ?? 0;

  const handleLogout = () => {
    if (refreshToken) api.post("/auth/logout", { refreshToken }).catch(() => {});
    logout();
    navigate("/");
  };

  const submitSearch = (e: React.FormEvent) => {
    e.preventDefault();
    navigate(`/products${search.trim() ? `?search=${encodeURIComponent(search.trim())}` : ""}`);
  };

  const roleLinks = (() => {
    if (user?.role === "Admin") return [
      { to: "/admin", label: "Dashboard", end: true },
      { to: "/admin/products", label: "SP/Danh mục" },
      { to: "/admin/coupons", label: "Mã giảm giá" },
      { to: "/admin/sellers", label: "Duyệt seller" },
    ];
    if (user?.role === "Seller") return [
      { to: "/seller", label: "Dashboard", end: true },
      { to: "/seller/products", label: "SP của tôi" },
      { to: "/seller/orders", label: "Đơn hàng" },
    ];
    return [];
  })();

  return (
    <nav className="sticky top-0 z-20 backdrop-blur-lg bg-white/90 dark:bg-slate-950/90 border-b border-slate-200/70 dark:border-slate-800">
      {/* Tier 1: logo · search · actions */}
      <div className="max-w-7xl mx-auto px-4 h-16 flex items-center gap-4">
        <Link to="/" className="flex items-center gap-2 font-bold text-lg shrink-0">
          <span className="grid place-items-center w-8 h-8 rounded-xl bg-gradient-to-br from-brand-500 to-brand-700 text-white shadow-glow"><ShoppingBag className="w-4 h-4" strokeWidth={2.25} aria-hidden /></span>
          <span className="text-slate-900 dark:text-white tracking-tight hidden sm:block">MiniShop</span>
        </Link>

        <form onSubmit={submitSearch} className="flex-1 max-w-xl relative">
          <input
            value={search}
            onChange={(e) => setSearch(e.target.value)}
            placeholder="Tìm thương hiệu, sản phẩm..."
            aria-label="Tìm kiếm"
            className="w-full rounded-full border border-slate-300 dark:border-slate-700 bg-slate-50 dark:bg-slate-900 pl-4 pr-12 py-2.5 text-sm focus:outline-none focus:ring-2 focus:ring-brand-500 focus:border-brand-500 transition"
          />
          <button type="submit" aria-label="Tìm" className="absolute right-1.5 top-1/2 -translate-y-1/2 grid place-items-center w-9 h-9 rounded-full bg-brand-600 text-white hover:bg-brand-700 transition">
            <Search className="w-[18px] h-[18px]" aria-hidden />
          </button>
        </form>

        <div className="flex items-center gap-2 shrink-0">
          <span className="hidden lg:flex items-center gap-1.5 text-xs text-slate-500 dark:text-slate-400">
            <MapPin className="w-4 h-4" aria-hidden />
            <span className="leading-tight">Giao đến<br /><span className="font-medium text-slate-700 dark:text-slate-200">Việt Nam</span></span>
          </span>
          <button
            onClick={toggle} aria-label="Đổi giao diện sáng/tối"
            className="grid place-items-center w-9 h-9 rounded-xl border border-slate-200 dark:border-slate-700 text-slate-600 dark:text-slate-300 hover:bg-slate-100 dark:hover:bg-slate-800 transition"
          >{theme === "dark" ? <Sun className="w-[18px] h-[18px]" aria-hidden /> : <Moon className="w-[18px] h-[18px]" aria-hidden />}</button>
          {user?.role === "Customer" && (
            <Link to="/cart" aria-label="Giỏ hàng" className="relative grid place-items-center w-9 h-9 rounded-xl text-slate-600 dark:text-slate-300 hover:bg-slate-100 dark:hover:bg-slate-800 transition">
              <ShoppingCart className="w-[18px] h-[18px]" aria-hidden />
              {cartCount > 0 && (
                <span className="absolute -top-1 -right-1 bg-brand-600 text-white text-[10px] rounded-full min-w-[18px] h-[18px] px-1 flex items-center justify-center font-medium">
                  {cartCount}
                </span>
              )}
            </Link>
          )}
          {token ? (
            <div className="flex items-center gap-2 text-sm">
              <span className="muted hidden md:block max-w-[120px] truncate">{user?.fullName}</span>
              <button
                onClick={handleLogout}
                className="text-rose-500 hover:text-rose-600 dark:hover:text-rose-400 transition whitespace-nowrap"
              >Đăng xuất</button>
            </div>
          ) : (
            <Link to="/login" className="inline-flex items-center gap-1.5 text-sm font-medium text-slate-700 dark:text-slate-200 hover:text-brand-600 dark:hover:text-brand-400 transition whitespace-nowrap">
              <User className="w-[18px] h-[18px]" aria-hidden /> Đăng nhập
            </Link>
          )}
        </div>
      </div>

      {/* Tier 2: category / role nav */}
      <div className="border-t border-slate-100 dark:border-slate-800/70">
        <div className="max-w-7xl mx-auto px-4 h-11 flex items-center gap-5 overflow-x-auto">
          <NavLink to="/products" className={navClass} end>Tất cả</NavLink>
          {roleLinks.length > 0
            ? roleLinks.map((l) => <NavLink key={l.to} to={l.to} className={navClass} end={l.end}>{l.label}</NavLink>)
            : categories?.slice(0, 8).map((c) => (
                <NavLink key={c.id} to={`/products?categoryId=${c.id}`} className={navClass}>{c.name}</NavLink>
              ))}
          {user?.role === "Customer" && <NavLink to="/orders" className={navClass}>Đơn hàng</NavLink>}
          {user?.role === "Customer" && <NavLink to="/wishlist" className={navClass}>Yêu thích</NavLink>}
        </div>
      </div>
    </nav>
  );
}
