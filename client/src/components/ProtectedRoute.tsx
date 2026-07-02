import { Navigate, Outlet } from "react-router-dom";
import { useAuth } from "../store/auth";

export function ProtectedRoute({ adminOnly = false }: { adminOnly?: boolean }) {
  const { token, user } = useAuth();
  if (!token) return <Navigate to="/login" replace />;
  if (adminOnly && user?.role !== "Admin") return <Navigate to="/" replace />;
  return <Outlet />;
}
