import { Link } from "react-router-dom";
import type { Product } from "../types";
import { useAddToCart, useToggleWishlist } from "../api/hooks";
import { useAuth } from "../store/auth";

export function ProductCard({ product }: { product: Product }) {
  const addToCart = useAddToCart();
  const toggleWishlist = useToggleWishlist();
  const isCustomer = useAuth((s) => s.user?.role === "Customer");
  const outOfStock = product.stock === 0;

  return (
    <div className="group surface rounded-2xl overflow-hidden hover:shadow-card hover:-translate-y-0.5 transition-all duration-200">
      <Link to={`/products/${product.id}`} className="block relative overflow-hidden">
        <img
          src={product.imageUrl ?? "https://via.placeholder.com/400"}
          alt={product.name}
          className="w-full h-48 object-cover group-hover:scale-105 transition-transform duration-300"
        />
        {outOfStock && (
          <span className="absolute top-2 left-2 bg-slate-900/80 text-white text-xs px-2 py-0.5 rounded-full">Hết hàng</span>
        )}
        {isCustomer && (
          <button
            title="Yêu thích"
            onClick={(e) => { e.preventDefault(); toggleWishlist.mutate({ productId: product.id, add: true }); }}
            className="absolute top-2 right-2 grid place-items-center w-8 h-8 rounded-full bg-white/90 dark:bg-slate-800/90 text-rose-500 opacity-0 group-hover:opacity-100 transition hover:scale-110"
          >♥</button>
        )}
      </Link>
      <div className="p-4 space-y-2">
        <Link to={`/products/${product.id}`} className="font-medium hover:text-brand-600 dark:hover:text-brand-400 line-clamp-1 transition-colors">
          {product.name}
        </Link>
        <div className="flex items-center justify-between text-xs">
          <span className="muted">{product.categoryName}</span>
          <span className="muted truncate max-w-[45%]" title={product.sellerShopName}>🏪 {product.sellerShopName}</span>
        </div>
        <div className="flex items-center gap-1 text-xs text-amber-500">
          {"★".repeat(Math.round(product.averageRating)) || "☆"}
          <span className="muted">({product.reviewCount})</span>
        </div>
        <div className="flex items-center justify-between pt-1">
          <span className="text-lg font-bold text-brand-600 dark:text-brand-400">${product.price.toFixed(2)}</span>
          {isCustomer && (
            <button
              disabled={outOfStock || addToCart.isPending}
              onClick={() => addToCart.mutate({ productId: product.id, quantity: 1 })}
              className="btn-primary px-3 py-1.5 text-sm"
            >
              {outOfStock ? "Hết hàng" : "+ Giỏ"}
            </button>
          )}
        </div>
      </div>
    </div>
  );
}
