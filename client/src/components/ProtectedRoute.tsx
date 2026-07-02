import { Navigate, Outlet } from "react-router-dom";
import { useAuth } from "../store/auth";
import type { Role } from "../types";

export function ProtectedRoute({ roles }: { roles?: Role[] }) {
  const { token, user } = useAuth();
  if (!token) return <Navigate to="/login" replace />;
  if (roles && (!user || !roles.includes(user.role))) return <Navigate to="/" replace />;
  return <Outlet />;
}
