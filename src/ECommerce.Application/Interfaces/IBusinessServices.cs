using ECommerce.Application.Common;
using ECommerce.Application.DTOs.Admin;
using ECommerce.Application.DTOs.Auth;
using ECommerce.Application.DTOs.Cart;
using ECommerce.Application.DTOs.Catalog;
using ECommerce.Application.DTOs.Coupons;
using ECommerce.Application.DTOs.Orders;
using ECommerce.Application.DTOs.Reviews;
using ECommerce.Application.DTOs.Wishlist;

namespace ECommerce.Application.Interfaces;

public interface IAuthService
{
    Task<Result<AuthResponse>> RegisterAsync(RegisterRequest request, CancellationToken ct = default);
    Task<Result<AuthResponse>> RegisterSellerAsync(RegisterSellerRequest request, CancellationToken ct = default);
    Task<Result<AuthResponse>> LoginAsync(LoginRequest request, CancellationToken ct = default);
    Task<Result<UserDto>> GetCurrentAsync(int userId, CancellationToken ct = default);
    // Đổi refresh token lấy cặp token mới (rotate: token cũ bị thu hồi).
    Task<Result<AuthResponse>> RefreshAsync(string refreshToken, CancellationToken ct = default);
    // Thu hồi refresh token (logout).
    Task<Result> LogoutAsync(string refreshToken, CancellationToken ct = default);
}

public interface IProductService
{
    Task<PagedResult<ProductDto>> SearchAsync(ProductQuery query, CancellationToken ct = default);
    Task<Result<ProductDto>> GetByIdAsync(int id, CancellationToken ct = default);
    Task<Result<ProductDto>> CreateAsync(int sellerId, CreateProductRequest request, CancellationToken ct = default);
    Task<Result<ProductDto>> UpdateAsync(int actorId, bool isAdmin, int id, UpdateProductRequest request, CancellationToken ct = default);
    Task<Result> DeleteAsync(int actorId, bool isAdmin, int id, CancellationToken ct = default);
    Task<Result<SellerShopDto>> GetSellerShopAsync(int sellerId, CancellationToken ct = default);
}

public interface ICategoryService
{
    Task<IReadOnlyList<CategoryDto>> GetAllAsync(CancellationToken ct = default);
    Task<Result<CategoryDto>> CreateAsync(CreateCategoryRequest request, CancellationToken ct = default);
    Task<Result> DeleteAsync(int id, CancellationToken ct = default);
}

public interface ICartService
{
    Task<CartDto> GetAsync(int userId, CancellationToken ct = default);
    Task<Result<CartDto>> AddItemAsync(int userId, AddCartItemRequest request, CancellationToken ct = default);
    Task<Result<CartDto>> UpdateItemAsync(int userId, int itemId, UpdateCartItemRequest request, CancellationToken ct = default);
    Task<Result<CartDto>> RemoveItemAsync(int userId, int itemId, CancellationToken ct = default);
    Task<Result> ClearAsync(int userId, CancellationToken ct = default);
}

public interface IOrderService
{
    Task<Result<OrderDto>> CheckoutAsync(int userId, CheckoutRequest request, CancellationToken ct = default);
    Task<PagedResult<OrderDto>> GetForUserAsync(int userId, int page, int pageSize, CancellationToken ct = default);
    Task<PagedResult<OrderDto>> GetAllAsync(int page, int pageSize, CancellationToken ct = default);
    Task<Result<OrderDto>> GetByIdAsync(int userId, bool isAdmin, int orderId, CancellationToken ct = default);
    Task<Result<OrderDto>> UpdateStatusAsync(int orderId, UpdateOrderStatusRequest request, CancellationToken ct = default);
    Task<Result<OrderDto>> CancelAsync(int userId, int orderId, CancellationToken ct = default);
    Task<Result<OrderSplitDto>> GetSplitAsync(int userId, bool isAdmin, int orderId, CancellationToken ct = default);
}

public interface IPaymentService
{
    // Khởi tạo thanh toán: redirect (momo) hoặc hoàn tất ngay (mock/cod).
    Task<Result<PayResultDto>> InitiateAsync(int userId, int orderId, PayOrderRequest request, CancellationToken ct = default);
    // Xác minh callback từ cổng, chốt trạng thái Order.
    Task<Result<OrderDto>> ConfirmAsync(string provider, IReadOnlyDictionary<string, string> callbackData, CancellationToken ct = default);
}

public interface ICouponService
{
    Task<Result<CouponPreviewDto>> ValidateAsync(ValidateCouponRequest request, CancellationToken ct = default);
    Task<IReadOnlyList<CouponDto>> GetAllAsync(CancellationToken ct = default);
    Task<IReadOnlyList<CouponDto>> GetActiveAsync(CancellationToken ct = default);
    Task<Result<CouponDto>> CreateAsync(CreateCouponRequest request, CancellationToken ct = default);
    Task<Result> DeleteAsync(int id, CancellationToken ct = default);
}

public interface IReviewService
{
    Task<IReadOnlyList<ReviewDto>> GetForProductAsync(int productId, CancellationToken ct = default);
    Task<Result<ReviewDto>> CreateAsync(int userId, int productId, CreateReviewRequest request, CancellationToken ct = default);
}

public interface IWishlistService
{
    Task<IReadOnlyList<WishlistItemDto>> GetAsync(int userId, CancellationToken ct = default);
    Task<Result> AddAsync(int userId, int productId, CancellationToken ct = default);
    Task<Result> RemoveAsync(int userId, int productId, CancellationToken ct = default);
}

public interface IDashboardService
{
    // sellerId = null -> toàn hệ thống (Admin); có giá trị -> chỉ dữ liệu của seller đó.
    Task<DashboardDto> GetAsync(int? sellerId = null, CancellationToken ct = default);
}

public interface ISellerAdminService
{
    // Danh sách seller theo trạng thái duyệt; status = null -> tất cả seller.
    Task<IReadOnlyList<SellerApplicationDto>> GetSellersAsync(string? status = null, CancellationToken ct = default);
    // Admin duyệt seller đang Pending.
    Task<Result<SellerApplicationDto>> ApproveAsync(int sellerId, CancellationToken ct = default);
}

public interface ISellerOrderService
{
    // Đơn hàng chứa item của seller — chỉ trả về các item thuộc seller đó.
    Task<PagedResult<OrderDto>> GetForSellerAsync(int sellerId, int page, int pageSize, CancellationToken ct = default);
    // Seller cập nhật trạng thái giao hàng cho item của chính mình.
    Task<Result<OrderItemDto>> UpdateFulfillmentAsync(int sellerId, int orderItemId, UpdateFulfillmentStatusRequest request, CancellationToken ct = default);
}
