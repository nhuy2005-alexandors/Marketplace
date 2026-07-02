import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { api } from "./client";
import type {
  AuthResponse, Cart, Category, Coupon, CouponPreview, Dashboard, Order, Paged, PayResult,
  Product, Review, User, WishlistItem,
} from "../types";
import { useAuth } from "../store/auth";

// ---- Auth ----
export function useRegister() {
  const setAuth = useAuth((s) => s.setAuth);
  return useMutation({
    mutationFn: (body: { email: string; password: string; fullName: string }) =>
      api.post<AuthResponse>("/auth/register", body).then((r) => r.data),
    onSuccess: (d) => setAuth(d.token, d.user),
  });
}

export function useLogin() {
  const setAuth = useAuth((s) => s.setAuth);
  return useMutation({
    mutationFn: (body: { email: string; password: string }) =>
      api.post<AuthResponse>("/auth/login", body).then((r) => r.data),
    onSuccess: (d) => setAuth(d.token, d.user),
  });
}

export function useRegisterSeller() {
  const setAuth = useAuth((s) => s.setAuth);
  return useMutation({
    mutationFn: (body: { email: string; password: string; fullName: string; shopName: string }) =>
      api.post<AuthResponse>("/auth/register-seller", body).then((r) => r.data),
    onSuccess: (d) => setAuth(d.token, d.user),
  });
}

// ---- Catalog ----
export interface ProductFilters {
  search?: string;
  categoryId?: number;
  sellerId?: number;
  minPrice?: number;
  maxPrice?: number;
  sortBy?: string;
  desc?: boolean;
  page?: number;
  pageSize?: number;
}

export function useProducts(filters: ProductFilters) {
  return useQuery({
    queryKey: ["products", filters],
    queryFn: () =>
      api.get<Paged<Product>>("/products", { params: filters }).then((r) => r.data),
  });
}

export function useProduct(id: number) {
  return useQuery({
    queryKey: ["product", id],
    queryFn: () => api.get<Product>(`/products/${id}`).then((r) => r.data),
    enabled: !!id,
  });
}

export function useCategories() {
  return useQuery({
    queryKey: ["categories"],
    queryFn: () => api.get<Category[]>("/categories").then((r) => r.data),
  });
}

export function useCreateCategory() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (body: { name: string; description?: string }) =>
      api.post<Category>("/categories", body).then((r) => r.data),
    onSuccess: () => qc.invalidateQueries({ queryKey: ["categories"] }),
  });
}

export function useDeleteCategory() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (id: number) => api.delete(`/categories/${id}`),
    onSuccess: () => qc.invalidateQueries({ queryKey: ["categories"] }),
  });
}

export interface ProductInput {
  name: string;
  description?: string;
  price: number;
  stock: number;
  imageUrl?: string;
  categoryId: number;
}

export function useCreateProduct() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (body: ProductInput) => api.post<Product>("/products", body).then((r) => r.data),
    onSuccess: () => qc.invalidateQueries({ queryKey: ["products"] }),
  });
}

export function useUpdateProduct() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ id, body }: { id: number; body: ProductInput }) =>
      api.put<Product>(`/products/${id}`, body).then((r) => r.data),
    onSuccess: () => qc.invalidateQueries({ queryKey: ["products"] }),
  });
}

export function useDeleteProduct() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (id: number) => api.delete(`/products/${id}`),
    onSuccess: () => qc.invalidateQueries({ queryKey: ["products"] }),
  });
}

export function useUploadProductImage() {
  return useMutation({
    mutationFn: (file: File) => {
      const form = new FormData();
      form.append("file", file);
      return api
        .post<{ url: string }>("/products/upload-image", form, {
          headers: { "Content-Type": "multipart/form-data" },
        })
        .then((r) => r.data.url);
    },
  });
}

export function useProductReviews(id: number) {
  return useQuery({
    queryKey: ["reviews", id],
    queryFn: () => api.get<Review[]>(`/products/${id}/reviews`).then((r) => r.data),
    enabled: !!id,
  });
}

export function useCreateReview(productId: number) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (body: { rating: number; comment?: string }) =>
      api.post<Review>(`/products/${productId}/reviews`, body).then((r) => r.data),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ["reviews", productId] });
      qc.invalidateQueries({ queryKey: ["product", productId] });
    },
  });
}

// ---- Cart ----
export function useCart() {
  return useQuery({ queryKey: ["cart"], queryFn: () => api.get<Cart>("/cart").then((r) => r.data) });
}

export function useAddToCart() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (body: { productId: number; quantity: number }) =>
      api.post<Cart>("/cart/items", body).then((r) => r.data),
    onSuccess: () => qc.invalidateQueries({ queryKey: ["cart"] }),
  });
}

export function useUpdateCartItem() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ id, quantity }: { id: number; quantity: number }) =>
      api.put<Cart>(`/cart/items/${id}`, { quantity }).then((r) => r.data),
    onSuccess: () => qc.invalidateQueries({ queryKey: ["cart"] }),
  });
}

export function useRemoveCartItem() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (id: number) => api.delete<Cart>(`/cart/items/${id}`).then((r) => r.data),
    onSuccess: () => qc.invalidateQueries({ queryKey: ["cart"] }),
  });
}

// ---- Orders ----
export function useOrders(page = 1, pageSize = 10) {
  return useQuery({
    queryKey: ["orders", page, pageSize],
    queryFn: () => api.get<Paged<Order>>("/orders", { params: { page, pageSize } }).then((r) => r.data),
  });
}

export function useOrder(id: number) {
  return useQuery({
    queryKey: ["order", id],
    queryFn: () => api.get<Order>(`/orders/${id}`).then((r) => r.data),
    enabled: !!id,
  });
}

export function useCheckout() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (body: { shippingAddress: string; couponCode?: string }) =>
      api.post<Order>("/orders", body).then((r) => r.data),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ["cart"] });
      qc.invalidateQueries({ queryKey: ["orders"] });
    },
  });
}

// method: mock | cod | vnpay | stripe. Trả requiresRedirect=true kèm redirectUrl khi cần chuyển sang cổng.
export function usePayOrder() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ id, method, returnUrl }: { id: number; method: string; returnUrl?: string }) =>
      api.post<PayResult>(`/orders/${id}/pay`, { method, returnUrl }).then((r) => r.data),
    onSuccess: () => qc.invalidateQueries({ queryKey: ["orders"] }),
  });
}

export function useValidateCoupon() {
  return useMutation({
    mutationFn: (body: { code: string; subtotal: number }) =>
      api.post<CouponPreview>("/coupons/validate", body).then((r) => r.data),
  });
}

export function useCoupons() {
  return useQuery({
    queryKey: ["coupons"],
    queryFn: () => api.get<Coupon[]>("/coupons").then((r) => r.data),
  });
}

export function useCreateCoupon() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (body: {
      code: string; type: string; value: number; minOrderAmount: number;
      expiresAt?: string; maxUses?: number;
    }) => api.post<Coupon>("/coupons", body).then((r) => r.data),
    onSuccess: () => qc.invalidateQueries({ queryKey: ["coupons"] }),
  });
}

export function useDeleteCoupon() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (id: number) => api.delete(`/coupons/${id}`),
    onSuccess: () => qc.invalidateQueries({ queryKey: ["coupons"] }),
  });
}

export function useCancelOrder() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (id: number) => api.post<Order>(`/orders/${id}/cancel`).then((r) => r.data),
    onSuccess: () => qc.invalidateQueries({ queryKey: ["orders"] }),
  });
}

export function useUpdateOrderStatus() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ id, status }: { id: number; status: string }) =>
      api.put<Order>(`/orders/${id}/status`, { status }).then((r) => r.data),
    onSuccess: () => qc.invalidateQueries({ queryKey: ["orders"] }),
  });
}

// ---- Wishlist ----
export function useWishlist() {
  return useQuery({
    queryKey: ["wishlist"],
    queryFn: () => api.get<WishlistItem[]>("/wishlist").then((r) => r.data),
  });
}

export function useToggleWishlist() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ productId, add }: { productId: number; add: boolean }) =>
      add ? api.post(`/wishlist/${productId}`) : api.delete(`/wishlist/${productId}`),
    onSuccess: () => qc.invalidateQueries({ queryKey: ["wishlist"] }),
  });
}

// ---- Admin ----
export function useDashboard() {
  return useQuery({
    queryKey: ["dashboard"],
    queryFn: () => api.get<Dashboard>("/admin/dashboard").then((r) => r.data),
  });
}

// ---- Seller ----
export function useSellerDashboard() {
  return useQuery({
    queryKey: ["seller-dashboard"],
    queryFn: () => api.get<Dashboard>("/seller/dashboard").then((r) => r.data),
  });
}

export function useSellerOrders(page = 1, pageSize = 10) {
  return useQuery({
    queryKey: ["seller-orders", page, pageSize],
    queryFn: () => api.get<Paged<Order>>("/seller/orders", { params: { page, pageSize } }).then((r) => r.data),
  });
}

export function useMe() {
  const token = useAuth((s) => s.token);
  return useQuery({
    queryKey: ["me"],
    queryFn: () => api.get<User>("/auth/me").then((r) => r.data),
    enabled: !!token,
  });
}
