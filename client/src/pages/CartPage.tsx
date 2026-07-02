import { useNavigate } from "react-router-dom";
import { useCart, useRemoveCartItem, useUpdateCartItem } from "../api/hooks";

export function CartPage() {
  const { data: cart, isLoading } = useCart();
  const updateItem = useUpdateCartItem();
  const removeItem = useRemoveCartItem();
  const navigate = useNavigate();

  if (isLoading) return <div className="text-center py-12 text-slate-400">Đang tải...</div>;

  if (!cart || cart.items.length === 0)
    return (
      <div className="max-w-3xl mx-auto px-4 py-16 text-center text-slate-400">
        Giỏ hàng trống.
        <button onClick={() => navigate("/")} className="block mx-auto mt-4 text-brand-600 hover:underline">
          Tiếp tục mua sắm →
        </button>
      </div>
    );

  return (
    <div className="max-w-3xl mx-auto px-4 py-6">
      <h1 className="text-xl font-bold mb-4">Giỏ hàng</h1>
      <div className="bg-white rounded-xl shadow-sm border border-slate-100 divide-y">
        {cart.items.map((item) => (
          <div key={item.id} className="flex items-center gap-4 p-4">
            <img
              src={item.imageUrl ?? "https://via.placeholder.com/80"}
              alt={item.productName} className="w-16 h-16 rounded-lg object-cover"
            />
            <div className="flex-1">
              <div className="font-medium">{item.productName}</div>
              <div className="text-sm text-slate-400">${item.unitPrice.toFixed(2)}</div>
            </div>
            <input
              type="number" min={1} value={item.quantity}
              onChange={(e) => updateItem.mutate({ id: item.id, quantity: Number(e.target.value) })}
              className="w-16 border rounded-lg px-2 py-1"
            />
            <div className="w-20 text-right font-medium">${item.subtotal.toFixed(2)}</div>
            <button onClick={() => removeItem.mutate(item.id)} className="text-rose-500 hover:underline text-sm">
              Xóa
            </button>
          </div>
        ))}
      </div>
      <div className="flex items-center justify-between mt-4">
        <span className="text-lg">Tổng: <span className="font-bold text-brand-700">${cart.total.toFixed(2)}</span></span>
        <button
          onClick={() => navigate("/checkout")}
          className="px-6 py-2.5 rounded-lg bg-brand-600 text-white hover:bg-brand-700"
        >Thanh toán</button>
      </div>
    </div>
  );
}
