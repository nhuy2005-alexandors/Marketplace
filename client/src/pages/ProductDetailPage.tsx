import { useState } from "react";
import { useParams } from "react-router-dom";
import {
  useAddToCart, useCreateReview, useProduct, useProductReviews, useToggleWishlist,
} from "../api/hooks";
import { useAuth } from "../store/auth";
import { apiError } from "../api/client";
import { Heart } from "lucide-react";
import { Button, Card, Input, Select, Spinner, Stars, Textarea } from "../components/ui";

export function ProductDetailPage() {
  const { id } = useParams();
  const productId = Number(id);
  const { data: product, isLoading } = useProduct(productId);
  const { data: reviews } = useProductReviews(productId);
  const addToCart = useAddToCart();
  const toggleWishlist = useToggleWishlist();
  const createReview = useCreateReview(productId);
  const isAuthed = useAuth((s) => s.user?.role === "Customer");

  const [qty, setQty] = useState(1);
  const [rating, setRating] = useState(5);
  const [comment, setComment] = useState("");
  const [reviewError, setReviewError] = useState("");

  if (isLoading || !product) return <Spinner />;

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
    <div className="max-w-5xl mx-auto px-4 py-6 animate-fade-in">
      <Card className="grid md:grid-cols-2 gap-8 p-6">
        <img
          src={product.imageUrl ?? "https://via.placeholder.com/500"}
          alt={product.name} className="w-full rounded-2xl object-cover aspect-square"
        />
        <div className="space-y-4">
          <div className="muted text-xs">{product.categoryName}</div>
          <h1 className="text-2xl font-bold tracking-tight">{product.name}</h1>
          <div className="flex items-center gap-2">
            <Stars value={product.averageRating} size={16} />
            <span className="muted text-sm">({product.reviewCount} đánh giá)</span>
          </div>
          <p className="muted">{product.description}</p>
          <div className="text-3xl font-bold text-slate-900 dark:text-white">
            ${product.price.toFixed(2)}
          </div>
          <div className="muted text-sm">Còn lại: {product.stock}</div>
          {isAuthed && (
            <div className="flex items-center gap-3">
              <Input
                type="number" min={1} max={product.stock} value={qty}
                onChange={(e) => setQty(Math.max(1, Number(e.target.value)))}
                className="w-20"
              />
              <Button
                disabled={product.stock === 0}
                onClick={() => addToCart.mutate({ productId: product.id, quantity: qty })}
              >Thêm vào giỏ</Button>
              <Button
                variant="ghost" aria-label="Thêm vào yêu thích"
                onClick={() => toggleWishlist.mutate({ productId: product.id, add: true })}
                className="text-rose-500"
              ><Heart className="w-4 h-4" aria-hidden /></Button>
            </div>
          )}
        </div>
      </Card>

      <Card className="mt-8 p-6">
        <h2 className="text-lg font-semibold mb-4">Đánh giá</h2>
        {isAuthed && (
          <form onSubmit={submitReview} className="mb-6 space-y-3 border-b border-slate-200 dark:border-slate-800 pb-6">
            <div className="flex items-center gap-2">
              <span className="text-sm">Số sao:</span>
              <Select value={rating} onChange={(e) => setRating(Number(e.target.value))} className="w-auto">
                {[5, 4, 3, 2, 1].map((n) => <option key={n} value={n}>{n} sao</option>)}
              </Select>
            </div>
            <Textarea
              value={comment} onChange={(e) => setComment(e.target.value)}
              placeholder="Nhận xét của bạn..."
            />
            {reviewError && <div className="text-rose-500 text-sm">{reviewError}</div>}
            <Button className="text-sm">Gửi đánh giá</Button>
            <p className="muted text-xs">Chỉ khách đã mua sản phẩm mới đánh giá được.</p>
          </form>
        )}
        <div className="space-y-4">
          {reviews?.length === 0 && <div className="muted text-sm">Chưa có đánh giá.</div>}
          {reviews?.map((r) => (
            <div key={r.id} className="rounded-xl bg-slate-50 dark:bg-slate-800/50 p-4">
              <div className="flex items-center gap-2">
                <span className="font-medium text-sm">{r.userName}</span>
                <Stars value={r.rating} size={12} />
              </div>
              <p className="muted text-sm mt-1">{r.comment}</p>
            </div>
          ))}
        </div>
      </Card>
    </div>
  );
}
