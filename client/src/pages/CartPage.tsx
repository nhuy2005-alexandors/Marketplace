import { useNavigate } from "react-router-dom";
import { useCart, useRemoveCartItem, useUpdateCartItem } from "../api/hooks";
import { Button, Card, EmptyState, Input, Spinner } from "../components/ui";

export function CartPage() {
  const { data: cart, isLoading } = useCart();
  const updateItem = useUpdateCartItem();
  const removeItem = useRemoveCartItem();
  const navigate = useNavigate();

  if (isLoading) return <Spinner />;

  if (!cart || cart.items.length === 0)
    return (
      <div className="max-w-3xl mx-auto px-4 py-16 animate-fade-in">
        <EmptyState icon="🛒" title="Giỏ hàng trống" hint="Hãy khám phá các sản phẩm và thêm vào giỏ." />
        <button onClick={() => navigate("/")} className="block mx-auto mt-4 text-brand-600 dark:text-brand-400 hover:underline">
          Tiếp tục mua sắm →
        </button>
      </div>
    );

  return (
    <div className="max-w-3xl mx-auto px-4 py-6 animate-fade-in">
      <h1 className="text-2xl font-bold tracking-tight mb-6">Giỏ hàng</h1>
      <Card className="divide-y divide-slate-200 dark:divide-slate-800">
        {cart.items.map((item) => (
          <div key={item.id} className="flex items-center gap-4 p-4">
            <img
              src={item.imageUrl ?? "https://via.placeholder.com/80"}
              alt={item.productName} className="w-16 h-16 rounded-xl object-cover"
            />
            <div className="flex-1">
              <div className="font-medium">{item.productName}</div>
              <div className="muted text-sm">${item.unitPrice.toFixed(2)}</div>
            </div>
            <Input
              type="number" min={1} value={item.quantity}
              onChange={(e) => updateItem.mutate({ id: item.id, quantity: Number(e.target.value) })}
              className="w-16"
            />
            <div className="w-20 text-right font-medium">${item.subtotal.toFixed(2)}</div>
            <button onClick={() => removeItem.mutate(item.id)} className="text-rose-500 hover:underline text-sm">
              Xóa
            </button>
          </div>
        ))}
      </Card>
      <div className="flex items-center justify-between mt-6">
        <span className="text-lg">
          Tổng: <span className="font-bold text-brand-600 dark:text-brand-400">${cart.total.toFixed(2)}</span>
        </span>
        <Button onClick={() => navigate("/checkout")} className="px-6">Thanh toán</Button>
      </div>
    </div>
  );
}
