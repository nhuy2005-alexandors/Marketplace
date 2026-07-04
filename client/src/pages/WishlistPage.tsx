import { Link } from "react-router-dom";
import { Heart } from "lucide-react";
import { useAddToCart, useToggleWishlist, useWishlist } from "../api/hooks";
import { Button, Card, EmptyState, PageHeader, Spinner } from "../components/ui";

export function WishlistPage() {
  const { data: items, isLoading } = useWishlist();
  const toggleWishlist = useToggleWishlist();
  const addToCart = useAddToCart();

  if (isLoading) return <Spinner />;

  return (
    <div className="max-w-4xl mx-auto px-4 py-6 animate-fade-in">
      <PageHeader title="Danh sách yêu thích" />
      {items?.length === 0 && <EmptyState icon={Heart} title="Chưa có sản phẩm yêu thích" />}
      <div className="grid sm:grid-cols-2 gap-4">
        {items?.map((item) => (
          <Card key={item.id} className="p-4 flex gap-4 items-center">
            <img src={item.imageUrl ?? "https://via.placeholder.com/80"} alt={item.productName} className="w-16 h-16 rounded-xl object-cover" />
            <div className="flex-1">
              <Link to={`/products/${item.productId}`} className="font-medium hover:text-brand-600 dark:hover:text-brand-400 transition-colors">{item.productName}</Link>
              <div className="text-brand-600 dark:text-brand-400 font-semibold">${item.price.toFixed(2)}</div>
            </div>
            <div className="flex flex-col gap-2">
              <Button className="text-sm py-1.5" onClick={() => addToCart.mutate({ productId: item.productId, quantity: 1 })}>Thêm giỏ</Button>
              <Button variant="danger" className="text-sm py-1.5" onClick={() => toggleWishlist.mutate({ productId: item.productId, add: false })}>Bỏ thích</Button>
            </div>
          </Card>
        ))}
      </div>
    </div>
  );
}
