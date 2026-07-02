import { Link } from "react-router-dom";
import type { Product } from "../types";
import { useAddToCart, useToggleWishlist } from "../api/hooks";
import { useAuth } from "../store/auth";

export function ProductCard({ product }: { product: Product }) {
  const addToCart = useAddToCart();
  const toggleWishlist = useToggleWishlist();
  const isAuthed = useAuth((s) => !!s.token);

  return (
    <div className="bg-white rounded-xl shadow-sm border border-slate-100 overflow-hidden hover:shadow-md transition">
      <Link to={`/products/${product.id}`}>
        <img
          src={product.imageUrl ?? "https://via.placeholder.com/400"}
          alt={product.name}
          className="w-full h-44 object-cover"
        />
      </Link>
      <div className="p-4 space-y-2">
        <Link to={`/products/${product.id}`} className="font-medium hover:text-brand-600 line-clamp-1">
          {product.name}
        </Link>
        <div className="text-xs text-slate-400">{product.categoryName}</div>
        <div className="flex items-center gap-1 text-xs text-amber-500">
          {"★".repeat(Math.round(product.averageRating))}
          <span className="text-slate-400">({product.reviewCount})</span>
        </div>
        <div className="flex items-center justify-between pt-1">
          <span className="text-lg font-semibold text-brand-700">${product.price.toFixed(2)}</span>
          {isAuthed && (
            <div className="flex gap-1">
              <button
                title="Yêu thích"
                onClick={() => toggleWishlist.mutate({ productId: product.id, add: true })}
                className="p-1.5 rounded-lg hover:bg-rose-50 text-rose-500"
              >♥</button>
              <button
                disabled={product.stock === 0 || addToCart.isPending}
                onClick={() => addToCart.mutate({ productId: product.id, quantity: 1 })}
                className="px-3 py-1.5 text-sm rounded-lg bg-brand-600 text-white hover:bg-brand-700 disabled:opacity-50"
              >
                {product.stock === 0 ? "Hết hàng" : "Thêm"}
              </button>
            </div>
          )}
        </div>
      </div>
    </div>
  );
}
