import { Link } from "react-router-dom";
import { Heart, ShoppingCart, Store } from "lucide-react";
import type { Product } from "../types";
import { useAddToCart, useToggleWishlist } from "../api/hooks";
import { useAuth } from "../store/auth";
import { Stars } from "./ui";

export function ProductCard({ product }: { product: Product }) {
  const addToCart = useAddToCart();
  const toggleWishlist = useToggleWishlist();
  const isCustomer = useAuth((s) => s.user?.role === "Customer");
  const outOfStock = product.stock === 0;

  return (
    <div className="group surface rounded-3xl p-2.5 hover:shadow-card hover:-translate-y-0.5 transition-all duration-200">
      <Link to={`/products/${product.id}`} className="block relative overflow-hidden rounded-2xl">
        <img
          src={product.imageUrl ?? "https://via.placeholder.com/400"}
          alt={product.name}
          className="w-full h-44 object-cover group-hover:scale-105 transition-transform duration-300"
        />
        {outOfStock && (
          <span className="absolute top-2 left-2 bg-slate-900/80 text-white text-xs px-2 py-0.5 rounded-full">Hết hàng</span>
        )}
        {isCustomer && (
          <button
            title="Yêu thích" aria-label="Thêm vào yêu thích"
            onClick={(e) => { e.preventDefault(); toggleWishlist.mutate({ productId: product.id, add: true }); }}
            className="absolute top-2.5 right-2.5 grid place-items-center w-9 h-9 rounded-full bg-white/90 dark:bg-slate-800/90 text-rose-500 shadow-sm opacity-0 group-hover:opacity-100 transition hover:scale-110 focus:opacity-100"
          ><Heart className="w-4 h-4" aria-hidden /></button>
        )}
      </Link>
      <div className="px-2 pt-3 pb-1">
        <Link to={`/products/${product.id}`} className="font-semibold hover:text-brand-600 dark:hover:text-brand-400 line-clamp-1 transition-colors">
          {product.name}
        </Link>
        <div className="flex items-center justify-between text-xs mt-1">
          <span className="muted">{product.categoryName}</span>
          <Link to={`/shop/${product.sellerId}`} className="inline-flex items-center gap-1 muted truncate max-w-[45%] hover:text-brand-600 dark:hover:text-brand-400 transition-colors" title={product.sellerShopName}><Store className="w-3.5 h-3.5 shrink-0" aria-hidden /> <span className="truncate">{product.sellerShopName}</span></Link>
        </div>
        <div className="mt-1.5"><Stars value={product.averageRating} count={product.reviewCount} /></div>
        <div className="flex items-center justify-between mt-3">
          <span className="text-lg font-bold">${product.price.toFixed(2)}</span>
          {isCustomer && (
            <button
              disabled={outOfStock || addToCart.isPending}
              aria-label="Thêm vào giỏ"
              onClick={() => addToCart.mutate({ productId: product.id, quantity: 1 })}
              className="grid place-items-center w-10 h-10 rounded-full bg-brand-600 text-white shadow-sm transition hover:bg-brand-700 hover:scale-105 active:scale-95 disabled:opacity-40 disabled:pointer-events-none"
            ><ShoppingCart className="w-[18px] h-[18px]" aria-hidden /></button>
          )}
        </div>
      </div>
    </div>
  );
}
