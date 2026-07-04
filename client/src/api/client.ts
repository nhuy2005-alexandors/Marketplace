import axios from "axios";
import { useAuth } from "../store/auth";

export const api = axios.create({ baseURL: "/api" });

api.interceptors.request.use((config) => {
  const token = useAuth.getState().token;
  if (token) config.headers.Authorization = `Bearer ${token}`;
  return config;
});

// Auto-refresh: khi access token 401, đổi refresh token lấy cặp mới rồi retry 1 lần.
// Dùng axios trần (không qua interceptor) cho /auth/refresh để tránh vòng lặp.
let refreshing: Promise<string | null> | null = null;

async function tryRefresh(): Promise<string | null> {
  const { refreshToken, setToken, logout } = useAuth.getState();
  if (!refreshToken) { logout(); return null; }
  try {
    const res = await axios.post("/api/auth/refresh", { refreshToken });
    setToken(res.data.token, res.data.refreshToken);
    return res.data.token as string;
  } catch {
    logout();
    return null;
  }
}

api.interceptors.response.use(
  (res) => res,
  async (error) => {
    const original = error.config;
    if (error.response?.status === 401 && original && !original._retry) {
      original._retry = true;
      refreshing ??= tryRefresh().finally(() => { refreshing = null; });
      const newToken = await refreshing;
      if (newToken) {
        original.headers.Authorization = `Bearer ${newToken}`;
        return api(original);
      }
    }
    return Promise.reject(error);
  }
);

export function apiError(e: unknown, fallback = "Something went wrong"): string {
  if (axios.isAxiosError(e)) {
    return e.response?.data?.error ?? e.message ?? fallback;
  }
  return fallback;
}
