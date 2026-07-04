import { useState } from "react";
import { Link, useNavigate } from "react-router-dom";
import { ShoppingBag } from "lucide-react";
import { useLogin, useRegister, useRegisterSeller } from "../api/hooks";
import { apiError } from "../api/client";
import { Button, Card, Input } from "../components/ui";

type Mode = "login" | "register" | "seller";

export function LoginPage() {
  const [mode, setMode] = useState<Mode>("login");
  const [form, setForm] = useState({ email: "", password: "", fullName: "", shopName: "" });
  const [error, setError] = useState("");
  const login = useLogin();
  const register = useRegister();
  const registerSeller = useRegisterSeller();
  const navigate = useNavigate();

  const pending = login.isPending || register.isPending || registerSeller.isPending;

  const submit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError("");
    try {
      if (mode === "login") {
        await login.mutateAsync({ email: form.email, password: form.password });
        navigate("/");
      } else if (mode === "register") {
        await register.mutateAsync({ email: form.email, password: form.password, fullName: form.fullName });
        navigate("/");
      } else {
        await registerSeller.mutateAsync(form);
        navigate("/seller");
      }
    } catch (err) {
      setError(apiError(err));
    }
  };

  const title = mode === "login" ? "Đăng nhập" : mode === "register" ? "Đăng ký khách hàng" : "Đăng ký người bán";

  return (
    <div className="min-h-[80vh] flex items-center justify-center px-4 animate-fade-in">
      <Card className="w-full max-w-sm p-6">
        <div className="text-center mb-2">
          <span className="inline-grid place-items-center w-10 h-10 rounded-xl bg-gradient-to-br from-brand-500 to-brand-700 text-white shadow-glow mb-3"><ShoppingBag className="w-5 h-5" strokeWidth={2.25} aria-hidden /></span>
          <h1 className="text-xl font-bold tracking-tight">{title}</h1>
        </div>
        <p className="muted text-xs text-center mb-5">
          Demo: admin@shop.com/Admin@123 · user@shop.com/User@123 · seller1@shop.com/Seller@123
        </p>
        <form onSubmit={submit} className="space-y-3">
          {(mode === "register" || mode === "seller") && (
            <Input
              placeholder="Họ tên" required value={form.fullName}
              onChange={(e) => setForm({ ...form, fullName: e.target.value })}
            />
          )}
          {mode === "seller" && (
            <Input
              placeholder="Tên cửa hàng" required value={form.shopName}
              onChange={(e) => setForm({ ...form, shopName: e.target.value })}
            />
          )}
          <Input
            type="email"
            placeholder="Email" required value={form.email}
            onChange={(e) => setForm({ ...form, email: e.target.value })}
          />
          <Input
            type="password"
            placeholder="Mật khẩu" required value={form.password}
            onChange={(e) => setForm({ ...form, password: e.target.value })}
          />
          {error && <div className="text-rose-500 text-sm">{error}</div>}
          <Button type="submit" disabled={pending} className="w-full">
            {mode === "login" ? "Đăng nhập" : "Tạo tài khoản"}
          </Button>
        </form>
        <div className="mt-4 flex flex-col gap-1.5 text-sm">
          {mode !== "login" && (
            <button onClick={() => setMode("login")} className="text-brand-600 dark:text-brand-400 hover:underline text-left">
              Đã có tài khoản? Đăng nhập
            </button>
          )}
          {mode !== "register" && (
            <button onClick={() => setMode("register")} className="text-brand-600 dark:text-brand-400 hover:underline text-left">
              Đăng ký khách hàng
            </button>
          )}
          {mode !== "seller" && (
            <button onClick={() => setMode("seller")} className="text-brand-600 dark:text-brand-400 hover:underline text-left">
              Trở thành người bán →
            </button>
          )}
        </div>
        <div className="mt-3"><Link to="/" className="muted text-xs hover:underline">← Về trang chủ</Link></div>
      </Card>
    </div>
  );
}
