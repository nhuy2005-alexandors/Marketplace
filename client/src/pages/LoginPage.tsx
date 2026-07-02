import { useState } from "react";
import { Link, useNavigate } from "react-router-dom";
import { useLogin, useRegister, useRegisterSeller } from "../api/hooks";
import { apiError } from "../api/client";

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
    <div className="max-w-sm mx-auto mt-12 bg-white p-6 rounded-xl shadow-sm border border-slate-100">
      <h1 className="text-xl font-bold mb-1">{title}</h1>
      <p className="text-xs text-slate-400 mb-4">
        Demo: admin@shop.com/Admin@123 · user@shop.com/User@123 · seller1@shop.com/Seller@123
      </p>
      <form onSubmit={submit} className="space-y-3">
        {(mode === "register" || mode === "seller") && (
          <input
            className="w-full border rounded-lg px-3 py-2 text-sm"
            placeholder="Họ tên" required value={form.fullName}
            onChange={(e) => setForm({ ...form, fullName: e.target.value })}
          />
        )}
        {mode === "seller" && (
          <input
            className="w-full border rounded-lg px-3 py-2 text-sm"
            placeholder="Tên cửa hàng" required value={form.shopName}
            onChange={(e) => setForm({ ...form, shopName: e.target.value })}
          />
        )}
        <input
          type="email" className="w-full border rounded-lg px-3 py-2 text-sm"
          placeholder="Email" required value={form.email}
          onChange={(e) => setForm({ ...form, email: e.target.value })}
        />
        <input
          type="password" className="w-full border rounded-lg px-3 py-2 text-sm"
          placeholder="Mật khẩu" required value={form.password}
          onChange={(e) => setForm({ ...form, password: e.target.value })}
        />
        {error && <div className="text-rose-500 text-sm">{error}</div>}
        <button
          type="submit" disabled={pending}
          className="w-full py-2 rounded-lg bg-brand-600 text-white hover:bg-brand-700 disabled:opacity-50"
        >
          {mode === "login" ? "Đăng nhập" : "Tạo tài khoản"}
        </button>
      </form>
      <div className="mt-3 flex flex-col gap-1 text-sm">
        {mode !== "login" && (
          <button onClick={() => setMode("login")} className="text-brand-600 hover:underline text-left">
            Đã có tài khoản? Đăng nhập
          </button>
        )}
        {mode !== "register" && (
          <button onClick={() => setMode("register")} className="text-brand-600 hover:underline text-left">
            Đăng ký khách hàng
          </button>
        )}
        {mode !== "seller" && (
          <button onClick={() => setMode("seller")} className="text-brand-600 hover:underline text-left">
            Trở thành người bán →
          </button>
        )}
      </div>
      <div className="mt-2"><Link to="/" className="text-xs text-slate-400 hover:underline">← Về trang chủ</Link></div>
    </div>
  );
}
