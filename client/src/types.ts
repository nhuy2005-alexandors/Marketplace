export type Role = "Customer" | "Admin" | "Seller";

export interface User {
  id: number;
  email: string;
  fullName: string;
  role: Role;
  shopName?: string;
}

export interface AuthResponse {
  token: string;
  user: User;
}

export interface Product {
  id: number;
  name: string;
  description?: string;
  price: number;
  stock: number;
  imageUrl?: string;
  categoryId: number;
  categoryName: string;
  sellerId: number;
  sellerShopName: string;
  averageRating: number;
  reviewCount: number;
}

export interface Category {
  id: number;
  name: string;
  description?: string;
}

export interface Paged<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
}

export interface CartItem {
  id: number;
  productId: number;
  productName: string;
  unitPrice: number;
  quantity: number;
  subtotal: number;
  imageUrl?: string;
}

export interface Cart {
  id: number;
  items: CartItem[];
  total: number;
}

export type OrderStatus = "Pending" | "Paid" | "Shipped" | "Delivered" | "Cancelled";

export interface OrderItem {
  productId: number;
  productName: string;
  unitPrice: number;
  quantity: number;
  subtotal: number;
}

export interface Payment {
  amount: number;
  method: string;
  status: string;
  transactionId?: string;
  paidAt?: string;
}

export interface Order {
  id: number;
  status: OrderStatus;
  shippingAddress: string;
  subtotal: number;
  discountAmount: number;
  couponCode?: string;
  total: number;
  createdAt: string;
  items: OrderItem[];
  payment?: Payment;
}

export interface PayResult {
  requiresRedirect: boolean;
  redirectUrl?: string;
  order?: Order;
}

export interface Coupon {
  id: number;
  code: string;
  type: "Percentage" | "FixedAmount";
  value: number;
  minOrderAmount: number;
  expiresAt?: string;
  maxUses?: number;
  timesUsed: number;
  isActive: boolean;
}

export interface CouponPreview {
  code: string;
  discountAmount: number;
  newTotal: number;
}

export interface Review {
  id: number;
  userId: number;
  userName: string;
  rating: number;
  comment?: string;
  createdAt: string;
}

export interface WishlistItem {
  id: number;
  productId: number;
  productName: string;
  price: number;
  imageUrl?: string;
}

export interface StatusCount {
  status: string;
  count: number;
}

export interface TopProduct {
  productId: number;
  productName: string;
  unitsSold: number;
  revenue: number;
}

export interface Dashboard {
  totalRevenue: number;
  totalOrders: number;
  totalProducts: number;
  totalCustomers: number;
  ordersByStatus: StatusCount[];
  topProducts: TopProduct[];
}
