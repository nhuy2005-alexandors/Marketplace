import { BrowserRouter, Route, Routes } from "react-router-dom";
import { Navbar } from "./components/Navbar";
import { Footer } from "./components/Footer";
import { ProtectedRoute } from "./components/ProtectedRoute";
import { HomePage } from "./pages/HomePage";
import { ProductListPage } from "./pages/ProductListPage";
import { ProductDetailPage } from "./pages/ProductDetailPage";
import { SellerShopPage } from "./pages/SellerShopPage";
import { LoginPage } from "./pages/LoginPage";
import { CartPage } from "./pages/CartPage";
import { CheckoutPage } from "./pages/CheckoutPage";
import { OrdersPage } from "./pages/OrdersPage";
import { WishlistPage } from "./pages/WishlistPage";
import { AdminDashboardPage } from "./pages/AdminDashboardPage";
import { AdminProductsPage } from "./pages/AdminProductsPage";
import { AdminCouponsPage } from "./pages/AdminCouponsPage";
import { AdminSellersPage } from "./pages/AdminSellersPage";
import { SellerDashboardPage } from "./pages/SellerDashboardPage";
import { SellerProductsPage } from "./pages/SellerProductsPage";
import { SellerOrdersPage } from "./pages/SellerOrdersPage";

export default function App() {
  return (
    <BrowserRouter>
      <div className="min-h-screen flex flex-col">
      <Navbar />
      <main className="flex-1">
      <Routes>
        <Route path="/" element={<HomePage />} />
        <Route path="/products" element={<ProductListPage />} />
        <Route path="/products/:id" element={<ProductDetailPage />} />
        <Route path="/shop/:sellerId" element={<SellerShopPage />} />
        <Route path="/login" element={<LoginPage />} />
        <Route element={<ProtectedRoute />}>
          <Route path="/cart" element={<CartPage />} />
          <Route path="/checkout" element={<CheckoutPage />} />
          <Route path="/orders" element={<OrdersPage />} />
          <Route path="/wishlist" element={<WishlistPage />} />
        </Route>
        <Route element={<ProtectedRoute roles={["Admin"]} />}>
          <Route path="/admin" element={<AdminDashboardPage />} />
          <Route path="/admin/products" element={<AdminProductsPage />} />
          <Route path="/admin/coupons" element={<AdminCouponsPage />} />
          <Route path="/admin/sellers" element={<AdminSellersPage />} />
        </Route>
        <Route element={<ProtectedRoute roles={["Seller"]} />}>
          <Route path="/seller" element={<SellerDashboardPage />} />
          <Route path="/seller/products" element={<SellerProductsPage />} />
          <Route path="/seller/orders" element={<SellerOrdersPage />} />
        </Route>
      </Routes>
      </main>
      <Footer />
      </div>
    </BrowserRouter>
  );
}
