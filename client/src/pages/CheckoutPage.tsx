import { useState } from "react";
import { useNavigate } from "react-router-dom";
import { useCart, useCheckout, usePayOrder, useValidateCoupon } from "../api/hooks";
import { apiError } from "../api/client";

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
    <div className="max-w-lg mx-auto px-4 py-6">
      <h1 className="text-xl font-bold mb-4">Thanh toán</h1>
      <div className="bg-white rounded-xl shadow-sm border border-slate-100 p-6 space-y-4">
        <div className="flex gap-2">
          <input
            value={couponInput} onChange={(e) => setCouponInput(e.target.value.toUpperCase())}
            placeholder="Mã giảm giá (VD: WELCOME10)"
            className="flex-1 border rounded-lg px-3 py-2 text-sm"
          />
          <button
            type="button" onClick={applyCoupon} disabled={validateCoupon.isPending}
            className="px-4 py-2 rounded-lg border text-sm hover:bg-slate-50"
          >Áp dụng</button>
        </div>
        {couponMsg && (
          <div className={couponApplied ? "text-emerald-600 text-sm" : "text-rose-500 text-sm"}>{couponMsg}</div>
        )}

        <div className="text-sm text-slate-500 space-y-1 border-t pt-3">
          <div className="flex justify-between"><span>Tạm tính</span><span>${subtotal.toFixed(2)}</span></div>
          {discount > 0 && (
            <div className="flex justify-between text-emerald-600"><span>Giảm giá</span><span>-${discount.toFixed(2)}</span></div>
          )}
          <div className="flex justify-between font-bold text-brand-700 text-base">
            <span>Tổng cộng</span><span>${finalTotal.toFixed(2)}</span>
          </div>
        </div>

        <form onSubmit={submit} className="space-y-4">
          <div>
            <label className="text-sm font-medium">Địa chỉ giao hàng</label>
            <textarea
              required value={address} onChange={(e) => setAddress(e.target.value)}
              className="w-full border rounded-lg px-3 py-2 text-sm mt-1"
              placeholder="Số nhà, đường, phường/xã, tỉnh/thành"
            />
          </div>
          <div>
            <label className="text-sm font-medium">Phương thức thanh toán</label>
            <select
              value={method} onChange={(e) => setMethod(e.target.value)}
              className="w-full border rounded-lg px-3 py-2 text-sm mt-1"
            >
              <option value="mock">Thẻ demo (hoàn tất ngay)</option>
              <option value="vnpay">VNPay</option>
              <option value="stripe">Stripe</option>
              <option value="cod">Thanh toán khi nhận hàng</option>
            </select>
          </div>
          {error && <div className="text-rose-500 text-sm">{error}</div>}
          <button
            disabled={checkout.isPending || payOrder.isPending}
            className="w-full py-2.5 rounded-lg bg-brand-600 text-white hover:bg-brand-700 disabled:opacity-50"
          >
            {checkout.isPending || payOrder.isPending ? "Đang xử lý..." : "Đặt hàng & Thanh toán"}
          </button>
        </form>
      </div>
    </div>
  );
}
