import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { api } from "./client";
import type {
  AuthResponse, Cart, Category, Coupon, CouponPreview, Dashboard, Order, Paged, PayResult,
  OrderSplit, Product, RecommendationResponse, Review, SellerApplication, SellerShop, User, WishlistItem,
} from "../types";
import { useAuth } from "../store/auth";
import { privateQueryKeys } from "./sessionIsolation";

// ---- Auth ----
export function useRegister() {
  const setAuth = useAuth((s) => s.setAuth);
  return useMutation({
    mutationFn: (body: { email: string; password: string; fullName: string }) =>
      api.post<AuthResponse>("/auth/register", body).then((r) => r.data),
    onSuccess: (d) => setAuth(d.token, d.refreshToken, d.user),
  });
}

export function useLogin() {
  const setAuth = useAuth((s) => s.setAuth);
  return useMutation({
    mutationFn: (body: { email: string; password: string }) =>
      api.post<AuthResponse>("/auth/login", body).then((r) => r.data),
    onSuccess: (d) => setAuth(d.token, d.refreshToken, d.user),
  });
}

export function useRegisterSeller() {
  const setAuth = useAuth((s) => s.setAuth);
  return useMutation({
    mutationFn: (body: { email: string; password: string; fullName: string; shopName: string }) =>
      api.post<AuthResponse>("/auth/register-seller", body).then((r) => r.data),
    onSuccess: (d) => setAuth(d.token, d.refreshToken, d.user),
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

export function useSellerShop(id: number) {
  return useQuery({
    queryKey: ["seller-shop", id],
    queryFn: () => api.get<SellerShop>(`/sellers/${id}/shop`).then((r) => r.data),
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
  const userId = useAuth((state) => state.user?.id ?? null);
  const isCustomer = useAuth((state) => state.user?.role === "Customer");
  return useQuery({
    queryKey: privateQueryKeys.cart(userId),
    queryFn: () => api.get<Cart>("/cart").then((r) => r.data),
    enabled: userId !== null && isCustomer,
  });
}

export function useAddToCart() {
  const qc = useQueryClient();
  const userId = useAuth((state) => state.user?.id ?? null);
  return useMutation({
    mutationFn: (body: { productId: number; quantity: number }) =>
      api.post<Cart>("/cart/items", body).then((r) => r.data),
    onSuccess: () => qc.invalidateQueries({ queryKey: privateQueryKeys.cart(userId) }),
  });
}

export function useUpdateCartItem() {
  const qc = useQueryClient();
  const userId = useAuth((state) => state.user?.id ?? null);
  return useMutation({
    mutationFn: ({ id, quantity }: { id: number; quantity: number }) =>
      api.put<Cart>(`/cart/items/${id}`, { quantity }).then((r) => r.data),
    onSuccess: () => qc.invalidateQueries({ queryKey: privateQueryKeys.cart(userId) }),
  });
}

export function useRemoveCartItem() {
  const qc = useQueryClient();
  const userId = useAuth((state) => state.user?.id ?? null);
  return useMutation({
    mutationFn: (id: number) => api.delete<Cart>(`/cart/items/${id}`).then((r) => r.data),
    onSuccess: () => qc.invalidateQueries({ queryKey: privateQueryKeys.cart(userId) }),
  });
}

// ---- Orders ----
export function useOrders(page = 1, pageSize = 10) {
  const userId = useAuth((state) => state.user?.id ?? null);
  return useQuery({
    queryKey: privateQueryKeys.orders(userId, page, pageSize),
    queryFn: () => api.get<Paged<Order>>("/orders", { params: { page, pageSize } }).then((r) => r.data),
    enabled: userId !== null,
  });
}

export function useOrder(id: number) {
  const userId = useAuth((state) => state.user?.id ?? null);
  return useQuery({
    queryKey: privateQueryKeys.order(userId, id),
    queryFn: () => api.get<Order>(`/orders/${id}`).then((r) => r.data),
    enabled: !!id && userId !== null,
  });
}

export function useCheckout() {
  const qc = useQueryClient();
  const userId = useAuth((state) => state.user?.id ?? null);
  return useMutation({
    mutationFn: (body: { shippingAddress: string; couponCode?: string }) =>
      api.post<Order>("/orders", body).then((r) => r.data),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: privateQueryKeys.cart(userId) });
      qc.invalidateQueries({ queryKey: privateQueryKeys.orders(userId) });
    },
  });
}

// method: mock | cod | momo. Trả requiresRedirect=true kèm redirectUrl khi cần chuyển sang cổng.
export function usePayOrder() {
  const qc = useQueryClient();
  const userId = useAuth((state) => state.user?.id ?? null);
  return useMutation({
    mutationFn: ({ id, method, returnUrl }: { id: number; method: string; returnUrl?: string }) =>
      api.post<PayResult>(`/orders/${id}/pay`, { method, returnUrl }).then((r) => r.data),
    onSuccess: () => qc.invalidateQueries({ queryKey: privateQueryKeys.orders(userId) }),
  });
}

export function useOrderSplit(id: number, enabled = true) {
  const userId = useAuth((state) => state.user?.id ?? null);
  return useQuery({
    queryKey: privateQueryKeys.orderSplit(userId, id),
    queryFn: () => api.get<OrderSplit>(`/orders/${id}/split`).then((r) => r.data),
    enabled: !!id && enabled && userId !== null,
  });
}

export function usePersonalizedRecommendations(limit = 8) {
  const userId = useAuth((state) => state.user?.id ?? null);
  return useQuery({
    queryKey: privateQueryKeys.recommendations(userId, limit),
    queryFn: () => api.get<RecommendationResponse>("/recommendations/personalized", { params: { limit } }).then((r) => r.data),
    staleTime: 60_000,
  });
}

export function useSimilarProducts(productId: number, limit = 8) {
  const userId = useAuth((state) => state.user?.id ?? null);
  return useQuery({
    queryKey: privateQueryKeys.similarProducts(userId, productId, limit),
    queryFn: () => api.get<RecommendationResponse>(`/recommendations/similar-products/${productId}`, { params: { limit } }).then((r) => r.data),
    enabled: productId > 0,
    staleTime: 60_000,
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

export function useActiveCoupons() {
  return useQuery({
    queryKey: ["coupons", "active"],
    queryFn: () => api.get<Coupon[]>("/coupons/active").then((r) => r.data),
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
  const userId = useAuth((state) => state.user?.id ?? null);
  const isCustomer = useAuth((state) => state.user?.role === "Customer");
  return useQuery({
    queryKey: privateQueryKeys.wishlist(userId),
    queryFn: () => api.get<WishlistItem[]>("/wishlist").then((r) => r.data),
    enabled: userId !== null && isCustomer,
  });
}

export function useToggleWishlist() {
  const qc = useQueryClient();
  const userId = useAuth((state) => state.user?.id ?? null);
  return useMutation({
    mutationFn: ({ productId, add }: { productId: number; add: boolean }) =>
      add ? api.post(`/wishlist/${productId}`) : api.delete(`/wishlist/${productId}`),
    onSuccess: () => qc.invalidateQueries({ queryKey: privateQueryKeys.wishlist(userId) }),
  });
}

// ---- Admin ----
export function useDashboard() {
  return useQuery({
    queryKey: ["dashboard"],
    queryFn: () => api.get<Dashboard>("/admin/dashboard").then((r) => r.data),
  });
}

export function useSellerApplications(status?: string) {
  return useQuery({
    queryKey: ["admin-sellers", status ?? "all"],
    queryFn: () =>
      api.get<SellerApplication[]>("/admin/sellers", { params: status ? { status } : {} }).then((r) => r.data),
  });
}

export function useApproveSeller() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (id: number) => api.post<SellerApplication>(`/admin/sellers/${id}/approve`).then((r) => r.data),
    onSuccess: () => qc.invalidateQueries({ queryKey: ["admin-sellers"] }),
  });
}

// ---- Seller ----
export function useSellerDashboard() {
  const userId = useAuth((state) => state.user?.id ?? null);
  return useQuery({
    queryKey: privateQueryKeys.sellerDashboard(userId),
    queryFn: () => api.get<Dashboard>("/seller/dashboard").then((r) => r.data),
    enabled: userId !== null,
  });
}

export function useSellerOrders(page = 1, pageSize = 10) {
  const userId = useAuth((state) => state.user?.id ?? null);
  return useQuery({
    queryKey: privateQueryKeys.sellerOrders(userId, page, pageSize),
    queryFn: () => api.get<Paged<Order>>("/seller/orders", { params: { page, pageSize } }).then((r) => r.data),
    enabled: userId !== null,
  });
}

export function useUpdateItemStatus() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ itemId, status }: { itemId: number; status: string }) =>
      api.put(`/seller/orders/items/${itemId}/status`, { status }),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ["seller-orders"] });
      qc.invalidateQueries({ queryKey: ["seller-dashboard"] });
    },
  });
}

// Refetch /auth/me và đồng bộ vào auth store — để sellerStatus (vd sau khi Admin duyệt)
// cập nhật mà không cần logout/login lại.
export function useMe() {
  const token = useAuth((s) => s.token);
  const userId = useAuth((s) => s.user?.id ?? null);
  const setUser = useAuth((s) => s.setUser);
  return useQuery({
    queryKey: privateQueryKeys.me(userId),
    queryFn: async () => {
      const user = await api.get<User>("/auth/me").then((r) => r.data);
      // A late response from a previous identity must not switch the active account back.
      if (useAuth.getState().user?.id === userId) setUser(user);
      return user;
    },
    enabled: !!token && userId !== null,
  });
}
