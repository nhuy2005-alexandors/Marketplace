import { useState } from "react";
import { Link, useNavigate } from "react-router-dom";
import { useLogin, useRegister } from "../api/hooks";
import { apiError } from "../api/client";

export function LoginPage() {
  const [mode, setMode] = useState<"login" | "register">("login");
  const [form, setForm] = useState({ email: "", password: "", fullName: "" });
  const [error, setError] = useState("");
  const login = useLogin();
  const register = useRegister();
  const navigate = useNavigate();

  const submit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError("");
    try {
      if (mode === "login") {
        await login.mutateAsync({ email: form.email, password: form.password });
      } else {
        await register.mutateAsync(form);
      }
      navigate("/");
    } catch (err) {
      setError(apiError(err));
    }
  };

  return (
    <div className="max-w-sm mx-auto mt-12 bg-white p-6 rounded-xl shadow-sm border border-slate-100">
      <h1 className="text-xl font-bold mb-1">{mode === "login" ? "Đăng nhập" : "Đăng ký"}</h1>
      <p className="text-xs text-slate-400 mb-4">
        Demo: admin@shop.com / Admin@123 — user@shop.com / User@123
      </p>
      <form onSubmit={submit} className="space-y-3">
        {mode === "register" && (
          <input
            className="w-full border rounded-lg px-3 py-2 text-sm"
            placeholder="Họ tên" required value={form.fullName}
            onChange={(e) => setForm({ ...form, fullName: e.target.value })}
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
          type="submit" disabled={login.isPending || register.isPending}
          className="w-full py-2 rounded-lg bg-brand-600 text-white hover:bg-brand-700 disabled:opacity-50"
        >
          {mode === "login" ? "Đăng nhập" : "Tạo tài khoản"}
        </button>
      </form>
      <button
        onClick={() => setMode(mode === "login" ? "register" : "login")}
        className="mt-3 text-sm text-brand-600 hover:underline"
      >
        {mode === "login" ? "Chưa có tài khoản? Đăng ký" : "Đã có tài khoản? Đăng nhập"}
      </button>
      <div className="mt-2"><Link to="/" className="text-xs text-slate-400 hover:underline">← Về trang chủ</Link></div>
    </div>
  );
}
