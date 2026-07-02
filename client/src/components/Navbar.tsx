import { Link, useNavigate } from "react-router-dom";
import { useAuth } from "../store/auth";
import { useCart } from "../api/hooks";

export function Navbar() {
  const { user, token, logout } = useAuth();
  const navigate = useNavigate();
  const { data: cart } = useCart();
  const cartCount = cart?.items.reduce((n, i) => n + i.quantity, 0) ?? 0;

  return (
    <nav className="bg-white border-b border-slate-200 sticky top-0 z-10">
      <div className="max-w-6xl mx-auto px-4 h-14 flex items-center gap-6">
        <Link to="/" className="font-bold text-brand-700 text-lg">🛍️ MiniShop</Link>
        <Link to="/" className="text-sm text-slate-600 hover:text-brand-600">Sản phẩm</Link>
        {token && <Link to="/orders" className="text-sm text-slate-600 hover:text-brand-600">Đơn hàng</Link>}
        {token && <Link to="/wishlist" className="text-sm text-slate-600 hover:text-brand-600">Yêu thích</Link>}
        {user?.role === "Admin" && (
          <>
            <Link to="/admin" className="text-sm text-slate-600 hover:text-brand-600">Dashboard</Link>
            <Link to="/admin/products" className="text-sm text-slate-600 hover:text-brand-600">SP/Danh mục</Link>
            <Link to="/admin/coupons" className="text-sm text-slate-600 hover:text-brand-600">Mã giảm giá</Link>
          </>
        )}
        <div className="ml-auto flex items-center gap-4">
          {token && (
            <Link to="/cart" className="relative text-slate-600 hover:text-brand-600">
              🛒
              {cartCount > 0 && (
                <span className="absolute -top-2 -right-2 bg-brand-600 text-white text-[10px] rounded-full w-4 h-4 flex items-center justify-center">
                  {cartCount}
                </span>
              )}
            </Link>
          )}
          {token ? (
            <div className="flex items-center gap-3 text-sm">
              <span className="text-slate-500">{user?.fullName}</span>
              <button
                onClick={() => { logout(); navigate("/"); }}
                className="text-rose-500 hover:underline"
              >Đăng xuất</button>
            </div>
          ) : (
            <Link to="/login" className="px-3 py-1.5 text-sm rounded-lg bg-brand-600 text-white hover:bg-brand-700">
              Đăng nhập
            </Link>
          )}
        </div>
      </div>
    </nav>
  );
}
