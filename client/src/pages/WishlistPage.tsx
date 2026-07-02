import { Link } from "react-router-dom";
import { useAddToCart, useToggleWishlist, useWishlist } from "../api/hooks";

export function WishlistPage() {
  const { data: items, isLoading } = useWishlist();
  const toggleWishlist = useToggleWishlist();
  const addToCart = useAddToCart();

  if (isLoading) return <div className="text-center py-12 text-slate-400">Đang tải...</div>;

  return (
    <div className="max-w-4xl mx-auto px-4 py-6">
      <h1 className="text-xl font-bold mb-4">Danh sách yêu thích</h1>
      {items?.length === 0 && <div className="text-slate-400">Chưa có sản phẩm yêu thích.</div>}
      <div className="grid sm:grid-cols-2 gap-4">
        {items?.map((item) => (
          <div key={item.id} className="bg-white rounded-xl shadow-sm border border-slate-100 p-4 flex gap-4 items-center">
            <img src={item.imageUrl ?? "https://via.placeholder.com/80"} alt={item.productName} className="w-16 h-16 rounded-lg object-cover" />
            <div className="flex-1">
              <Link to={`/products/${item.productId}`} className="font-medium hover:text-brand-600">{item.productName}</Link>
              <div className="text-brand-700 font-semibold">${item.price.toFixed(2)}</div>
            </div>
            <div className="flex flex-col gap-2">
              <button
                onClick={() => addToCart.mutate({ productId: item.productId, quantity: 1 })}
                className="px-3 py-1.5 text-sm rounded-lg bg-brand-600 text-white hover:bg-brand-700"
              >Thêm giỏ</button>
              <button
                onClick={() => toggleWishlist.mutate({ productId: item.productId, add: false })}
                className="px-3 py-1.5 text-sm rounded-lg border text-rose-500"
              >Bỏ thích</button>
            </div>
          </div>
        ))}
      </div>
    </div>
  );
}
