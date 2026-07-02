import { useState } from "react";
import { useParams } from "react-router-dom";
import {
  useAddToCart, useCreateReview, useProduct, useProductReviews, useToggleWishlist,
} from "../api/hooks";
import { useAuth } from "../store/auth";
import { apiError } from "../api/client";

export function ProductDetailPage() {
  const { id } = useParams();
  const productId = Number(id);
  const { data: product, isLoading } = useProduct(productId);
  const { data: reviews } = useProductReviews(productId);
  const addToCart = useAddToCart();
  const toggleWishlist = useToggleWishlist();
  const createReview = useCreateReview(productId);
  const isAuthed = useAuth((s) => !!s.token);

  const [qty, setQty] = useState(1);
  const [rating, setRating] = useState(5);
  const [comment, setComment] = useState("");
  const [reviewError, setReviewError] = useState("");

  if (isLoading || !product) return <div className="text-center py-12 text-slate-400">Đang tải...</div>;

  const submitReview = async (e: React.FormEvent) => {
    e.preventDefault();
    setReviewError("");
    try {
      await createReview.mutateAsync({ rating, comment });
      setComment("");
    } catch (err) {
      setReviewError(apiError(err));
    }
  };

  return (
    <div className="max-w-5xl mx-auto px-4 py-6">
      <div className="grid md:grid-cols-2 gap-8 bg-white p-6 rounded-xl shadow-sm border border-slate-100">
        <img
          src={product.imageUrl ?? "https://via.placeholder.com/500"}
          alt={product.name} className="w-full rounded-xl object-cover aspect-square"
        />
        <div className="space-y-4">
          <div className="text-xs text-slate-400">{product.categoryName}</div>
          <h1 className="text-2xl font-bold">{product.name}</h1>
          <div className="flex items-center gap-1 text-amber-500">
            {"★".repeat(Math.round(product.averageRating))}
            <span className="text-slate-400 text-sm">({product.reviewCount} đánh giá)</span>
          </div>
          <p className="text-slate-600">{product.description}</p>
          <div className="text-3xl font-bold text-brand-700">${product.price.toFixed(2)}</div>
          <div className="text-sm text-slate-500">Còn lại: {product.stock}</div>
          {isAuthed && (
            <div className="flex items-center gap-3">
              <input
                type="number" min={1} max={product.stock} value={qty}
                onChange={(e) => setQty(Math.max(1, Number(e.target.value)))}
                className="w-20 border rounded-lg px-3 py-2"
              />
              <button
                disabled={product.stock === 0}
                onClick={() => addToCart.mutate({ productId: product.id, quantity: qty })}
                className="px-5 py-2 rounded-lg bg-brand-600 text-white hover:bg-brand-700 disabled:opacity-50"
              >Thêm vào giỏ</button>
              <button
                onClick={() => toggleWishlist.mutate({ productId: product.id, add: true })}
                className="px-4 py-2 rounded-lg border text-rose-500"
              >♥</button>
            </div>
          )}
        </div>
      </div>

      <div className="mt-8 bg-white p-6 rounded-xl shadow-sm border border-slate-100">
        <h2 className="text-lg font-semibold mb-4">Đánh giá</h2>
        {isAuthed && (
          <form onSubmit={submitReview} className="mb-6 space-y-2 border-b pb-6">
            <div className="flex items-center gap-2">
              <span className="text-sm">Số sao:</span>
              <select value={rating} onChange={(e) => setRating(Number(e.target.value))} className="border rounded px-2 py-1">
                {[5, 4, 3, 2, 1].map((n) => <option key={n} value={n}>{n} ★</option>)}
              </select>
            </div>
            <textarea
              value={comment} onChange={(e) => setComment(e.target.value)}
              placeholder="Nhận xét của bạn..." className="w-full border rounded-lg px-3 py-2 text-sm"
            />
            {reviewError && <div className="text-rose-500 text-sm">{reviewError}</div>}
            <button className="px-4 py-2 rounded-lg bg-brand-600 text-white text-sm">Gửi đánh giá</button>
            <p className="text-xs text-slate-400">Chỉ khách đã mua sản phẩm mới đánh giá được.</p>
          </form>
        )}
        <div className="space-y-4">
          {reviews?.length === 0 && <div className="text-slate-400 text-sm">Chưa có đánh giá.</div>}
          {reviews?.map((r) => (
            <div key={r.id} className="border-b pb-3 last:border-0">
              <div className="flex items-center gap-2">
                <span className="font-medium text-sm">{r.userName}</span>
                <span className="text-amber-500 text-sm">{"★".repeat(r.rating)}</span>
              </div>
              <p className="text-slate-600 text-sm mt-1">{r.comment}</p>
            </div>
          ))}
        </div>
      </div>
    </div>
  );
}
