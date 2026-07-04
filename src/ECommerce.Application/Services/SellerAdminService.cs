using ECommerce.Application.Common;
using ECommerce.Application.DTOs.Admin;
using ECommerce.Application.Interfaces;
using ECommerce.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace ECommerce.Application.Services;

public class SellerAdminService : ISellerAdminService
{
    private readonly IAppDbContext _db;
    private readonly IEmailSender _email;

    public SellerAdminService(IAppDbContext db, IEmailSender? email = null)
    {
        _db = db;
        _email = email ?? new NullEmailSender();
    }

    // No-op fallback giữ ctor cũ `new SellerAdminService(db)` compile được cho unit test hiện có.
    private sealed class NullEmailSender : IEmailSender
    {
        public Task SendAsync(string toEmail, string subject, string body, CancellationToken ct = default)
            => Task.CompletedTask;
    }

    public async Task<IReadOnlyList<SellerApplicationDto>> GetSellersAsync(string? status = null, CancellationToken ct = default)
    {
        var query = _db.Users.AsNoTracking().Where(u => u.Role == UserRole.Seller);
        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<SellerStatus>(status, true, out var s))
            query = query.Where(u => u.SellerStatus == s);

        return await query
            .OrderBy(u => u.SellerStatus)
            .ThenBy(u => u.CreatedAt)
            .Select(u => new SellerApplicationDto(
                u.Id, u.Email, u.FullName, u.ShopName,
                (u.SellerStatus ?? SellerStatus.Pending).ToString(), u.CreatedAt))
            .ToListAsync(ct);
    }

    public async Task<Result<SellerApplicationDto>> ApproveAsync(int sellerId, CancellationToken ct = default)
    {
        var seller = await _db.Users.FirstOrDefaultAsync(
            u => u.Id == sellerId && u.Role == UserRole.Seller, ct);
        if (seller is null)
            return Result.Fail<SellerApplicationDto>("Seller not found.", ErrorType.NotFound);
        if (seller.SellerStatus == SellerStatus.Approved)
            return Result.Fail<SellerApplicationDto>("Seller đã được duyệt.", ErrorType.Conflict);

        seller.SellerStatus = SellerStatus.Approved;
        seller.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        try
        {
            await _email.SendAsync(
                seller.Email,
                "Cửa hàng của bạn đã được duyệt",
                "Chúc mừng! Cửa hàng của bạn đã được duyệt. Bạn có thể bắt đầu đăng sản phẩm ngay bây giờ.",
                ct);
        }
        catch (Exception)
        {
            // Best-effort: gửi email thất bại không được làm fail việc duyệt seller.
        }

        return Result.Ok(new SellerApplicationDto(
            seller.Id, seller.Email, seller.FullName, seller.ShopName,
            seller.SellerStatus.ToString()!, seller.CreatedAt));
    }
}
