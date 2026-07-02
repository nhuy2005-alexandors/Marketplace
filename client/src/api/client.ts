import axios from "axios";
import { useAuth } from "../store/auth";

export const api = axios.create({ baseURL: "/api" });

api.interceptors.request.use((config) => {
  const token = useAuth.getState().token;
  if (token) config.headers.Authorization = `Bearer ${token}`;
  return config;
});

api.interceptors.response.use(
  (res) => res,
  (error) => {
    if (error.response?.status === 401) {
      useAuth.getState().logout();
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
