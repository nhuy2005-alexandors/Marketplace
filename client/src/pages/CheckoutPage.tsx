import { useState } from "react";
import { useNavigate } from "react-router-dom";
import { useCart, useCheckout, usePayOrder, useValidateCoupon } from "../api/hooks";
import { apiError } from "../api/client";
import { Button, Card, Input, Select, Textarea } from "../components/ui";

const RETURN_BASE = "http://localhost:5215/api/payments"; // API callback endpoint cho cổng redirect

export function CheckoutPage() {
  const { data: cart } = useCart();
  const checkout = useCheckout();
  const payOrder = usePayOrder();
  const validateCoupon = useValidateCoupon();
  const navigate = useNavigate();

  const [address, setAddress] = useState("");
  const [method, setMethod] = useState("mock");
  const [couponInput, setCouponInput] = useState("");
  const [couponApplied, setCouponApplied] = useState<{ code: string; discount: number } | null>(null);
  const [error, setError] = useState("");
  const [couponMsg, setCouponMsg] = useState("");

  const subtotal = cart?.total ?? 0;
  const discount = couponApplied?.discount ?? 0;
  const finalTotal = Math.max(0, subtotal - discount);

  const applyCoupon = async () => {
    setCouponMsg("");
    if (!couponInput.trim()) return;
    try {
      const preview = await validateCoupon.mutateAsync({ code: couponInput.trim(), subtotal });
      setCouponApplied({ code: preview.code, discount: preview.discountAmount });
      setCouponMsg(`Áp dụng "${preview.code}": -$${preview.discountAmount.toFixed(2)}`);
    } catch (err) {
      setCouponApplied(null);
      setCouponMsg(apiError(err));
    }
  };

  const submit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError("");
    try {
      const order = await checkout.mutateAsync({
        shippingAddress: address,
        couponCode: couponApplied?.code,
      });
      const returnUrl = `${RETURN_BASE}/${method}/callback`;
      const result = await payOrder.mutateAsync({ id: order.id, method, returnUrl });
      if (result.requiresRedirect && result.redirectUrl) {
        window.location.href = result.redirectUrl;
        return;
      }
      navigate("/orders");
    } catch (err) {
      setError(apiError(err));
    }
  };

  return (
    <div className="max-w-4xl mx-auto px-4 py-6 animate-fade-in">
      <h1 className="text-2xl font-bold tracking-tight mb-6">Thanh toán</h1>
      <div className="grid md:grid-cols-5 gap-6">
        <Card className="p-6 space-y-4 md:col-span-3 order-2 md:order-1">
          <form onSubmit={submit} className="space-y-4">
            <div>
              <label className="text-sm font-medium">Địa chỉ giao hàng</label>
              <Textarea
                required value={address} onChange={(e) => setAddress(e.target.value)}
                className="mt-1"
                placeholder="Số nhà, đường, phường/xã, tỉnh/thành"
              />
            </div>
            <div>
              <label className="text-sm font-medium">Phương thức thanh toán</label>
              <Select
                value={method} onChange={(e) => setMethod(e.target.value)}
                className="mt-1"
              >
                <option value="mock">Thẻ demo (hoàn tất ngay)</option>
                <option value="vnpay">VNPay</option>
                <option value="stripe">Stripe</option>
                <option value="cod">Thanh toán khi nhận hàng</option>
              </Select>
            </div>
            {error && <div className="text-rose-500 text-sm">{error}</div>}
            <Button
              type="submit"
              disabled={checkout.isPending || payOrder.isPending}
              className="w-full"
            >
              {checkout.isPending || payOrder.isPending ? "Đang xử lý..." : "Đặt hàng & Thanh toán"}
            </Button>
          </form>
        </Card>

        <Card className="p-6 space-y-4 md:col-span-2 order-1 md:order-2 h-fit">
          <h2 className="font-semibold">Tóm tắt đơn hàng</h2>
          <div className="flex gap-2">
            <Input
              value={couponInput} onChange={(e) => setCouponInput(e.target.value.toUpperCase())}
              placeholder="Mã giảm giá"
            />
            <Button
              type="button" variant="ghost" onClick={applyCoupon} disabled={validateCoupon.isPending}
            >Áp dụng</Button>
          </div>
          {couponMsg && (
            <div className={`text-sm ${couponApplied ? "text-emerald-600 dark:text-emerald-400" : "text-rose-500"}`}>{couponMsg}</div>
          )}

          <div className="text-sm muted space-y-1 border-t border-slate-200 dark:border-slate-800 pt-3">
            <div className="flex justify-between"><span>Tạm tính</span><span>${subtotal.toFixed(2)}</span></div>
            {discount > 0 && (
              <div className="flex justify-between text-emerald-600 dark:text-emerald-400"><span>Giảm giá</span><span>-${discount.toFixed(2)}</span></div>
            )}
            <div className="flex justify-between font-bold text-base pt-1 text-slate-800 dark:text-slate-100">
              <span>Tổng cộng</span><span className="text-brand-600 dark:text-brand-400">${finalTotal.toFixed(2)}</span>
            </div>
          </div>
        </Card>
      </div>
    </div>
  );
}
