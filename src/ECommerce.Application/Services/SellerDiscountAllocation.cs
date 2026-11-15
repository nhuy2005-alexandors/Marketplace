using ECommerce.Domain.Entities;

namespace ECommerce.Application.Services;

internal static class SellerDiscountAllocation
{
    public static IReadOnlyDictionary<int, decimal> ForOrder(Order order)
    {
        var groups = order.Items.GroupBy(i => i.SellerId).OrderBy(g => g.Key)
            .Select(g => (SellerId: g.Key, Subtotal: g.Sum(i => i.Subtotal))).ToArray();
        var remainingSubtotal = order.Subtotal;
        var discount = Math.Clamp(order.DiscountAmount, 0m, remainingSubtotal);
        var remainingDiscount = discount;
        var shares = new Dictionary<int, decimal>();
        foreach (var group in groups)
        {
            remainingSubtotal -= group.Subtotal;
            var rounded = order.Subtotal > 0 ? Math.Round(discount * (group.Subtotal / order.Subtotal), 2) : 0m;
            // Reserve enough capacity in later sellers, including penny-priced items.
            var share = Math.Clamp(rounded, Math.Max(0m, remainingDiscount - remainingSubtotal), Math.Min(group.Subtotal, remainingDiscount));
            shares[group.SellerId] = share;
            remainingDiscount -= share;
        }
        return shares;
    }
}
