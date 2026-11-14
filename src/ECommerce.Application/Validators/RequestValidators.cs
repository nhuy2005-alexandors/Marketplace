using ECommerce.Application.DTOs.Auth;
using ECommerce.Application.DTOs.Cart;
using ECommerce.Application.DTOs.Catalog;
using ECommerce.Application.DTOs.Coupons;
using ECommerce.Application.DTOs.Orders;
using ECommerce.Application.DTOs.Reviews;
using FluentValidation;

namespace ECommerce.Application.Validators;

public class RegisterRequestValidator : AbstractValidator<RegisterRequest>
{
    public RegisterRequestValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.Password).NotEmpty().MinimumLength(6);
        RuleFor(x => x.FullName).NotEmpty().MaximumLength(100);
    }
}

public class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.Password).NotEmpty();
    }
}

public class RegisterSellerRequestValidator : AbstractValidator<RegisterSellerRequest>
{
    public RegisterSellerRequestValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.Password).NotEmpty().MinimumLength(6);
        RuleFor(x => x.FullName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.ShopName).NotEmpty().MaximumLength(150);
    }
}

public class CreateProductRequestValidator : AbstractValidator<CreateProductRequest>
{
    public CreateProductRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Description).MaximumLength(2000);
        RuleFor(x => x.Price).GreaterThan(0);
        RuleFor(x => x.Stock).GreaterThanOrEqualTo(0);
        RuleFor(x => x.CategoryId).GreaterThan(0);
        RuleFor(x => x.Sku).MaximumLength(100);
        RuleFor(x => x.Attributes).Must(x => x is null || x.Count <= 100)
            .WithMessage("A product can contain at most 100 structured attributes.");
    }
}

public class UpdateProductRequestValidator : AbstractValidator<UpdateProductRequest>
{
    public UpdateProductRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Description).MaximumLength(2000);
        RuleFor(x => x.Price).GreaterThan(0);
        RuleFor(x => x.Stock).GreaterThanOrEqualTo(0);
        RuleFor(x => x.CategoryId).GreaterThan(0);
        RuleFor(x => x.Sku).MaximumLength(100);
        RuleFor(x => x.Attributes).Must(x => x is null || x.Count <= 100)
            .WithMessage("A product can contain at most 100 structured attributes.");
        RuleFor(x => x).Must(x => !(x.BrandId.HasValue && x.ClearBrand))
            .WithMessage("BrandId and ClearBrand cannot be supplied together.");
    }
}

public class ProductQueryValidator : AbstractValidator<ProductQuery>
{
    private const decimal MaxPrice = 9999999999999999.99m; // decimal(18,2)
    private const decimal MaxAttributeNumber = 99999999999999.9999m; // decimal(18,4)

    private static bool IsValidPrice(decimal? value) =>
        !value.HasValue || (value.Value <= MaxPrice &&
            decimal.Round(value.Value, 2, MidpointRounding.AwayFromZero) == value.Value);

    private static bool IsValidAttributeNumber(decimal? value) =>
        !value.HasValue || (value.Value >= -MaxAttributeNumber && value.Value <= MaxAttributeNumber &&
            decimal.Round(value.Value, 4, MidpointRounding.AwayFromZero) == value.Value);

    public ProductQueryValidator()
    {
        RuleFor(x => x.Search).MaximumLength(200);
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
        RuleFor(x => x.MinPrice).GreaterThanOrEqualTo(0).When(x => x.MinPrice.HasValue);
        RuleFor(x => x.MaxPrice).GreaterThanOrEqualTo(0).When(x => x.MaxPrice.HasValue);
        RuleFor(x => x.MinPrice).Must(IsValidPrice)
            .WithMessage("MinPrice must fit decimal(18,2) without rounding.");
        RuleFor(x => x.MaxPrice).Must(IsValidPrice)
            .WithMessage("MaxPrice must fit decimal(18,2) without rounding.");
        RuleFor(x => x).Must(x => !x.MinPrice.HasValue || !x.MaxPrice.HasValue || x.MinPrice <= x.MaxPrice)
            .WithMessage("MinPrice cannot exceed MaxPrice.");
        RuleFor(x => x.AttributeFilters).Must(x => x is null || x.Count <= 20)
            .WithMessage("At most 20 structured attribute filters are allowed.");
        RuleForEach(x => x.AttributeFilters).ChildRules(filter =>
        {
            filter.RuleFor(x => x.AttributeDefinitionId).GreaterThan(0);
            filter.RuleFor(x => x.ValueText).MaximumLength(2000);
            filter.RuleFor(x => x.MinValue).Must(IsValidAttributeNumber)
                .WithMessage("Attribute MinValue must fit decimal(18,4) without rounding.");
            filter.RuleFor(x => x.MaxValue).Must(IsValidAttributeNumber)
                .WithMessage("Attribute MaxValue must fit decimal(18,4) without rounding.");
            filter.RuleFor(x => x).Must(x =>
                (string.IsNullOrWhiteSpace(x.ValueText) ? 0 : 1) +
                ((x.MinValue.HasValue || x.MaxValue.HasValue) ? 1 : 0) +
                (x.ValueBoolean.HasValue ? 1 : 0) == 1)
                .WithMessage("Each attribute filter must specify exactly one typed filter value.");
            filter.RuleFor(x => x).Must(x =>
                !x.MinValue.HasValue || !x.MaxValue.HasValue || x.MinValue <= x.MaxValue)
                .WithMessage("Attribute MinValue cannot exceed MaxValue.");
        });
    }
}

public class CreateBrandRequestValidator : AbstractValidator<CreateBrandRequest>
{
    public CreateBrandRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Slug).MaximumLength(200);
        RuleFor(x => x.Description).MaximumLength(2000);
    }
}

public class UpdateBrandRequestValidator : AbstractValidator<UpdateBrandRequest>
{
    public UpdateBrandRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Slug).MaximumLength(200);
        RuleFor(x => x.Description).MaximumLength(2000);
    }
}

public class CreateAttributeDefinitionRequestValidator : AbstractValidator<CreateAttributeDefinitionRequest>
{
    public CreateAttributeDefinitionRequestValidator()
    {
        RuleFor(x => x.CategoryId).GreaterThan(0);
        RuleFor(x => x.Code).NotEmpty().MaximumLength(100);
        RuleFor(x => x.DisplayName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Unit).MaximumLength(40);
        RuleFor(x => x.DataType).IsInEnum();
    }
}

public class UpdateAttributeDefinitionRequestValidator : AbstractValidator<UpdateAttributeDefinitionRequest>
{
    public UpdateAttributeDefinitionRequestValidator()
    {
        RuleFor(x => x.CategoryId).GreaterThan(0);
        RuleFor(x => x.Code).NotEmpty().MaximumLength(100);
        RuleFor(x => x.DisplayName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Unit).MaximumLength(40);
        RuleFor(x => x.DataType).IsInEnum();
    }
}

public class CheckoutRequestValidator : AbstractValidator<CheckoutRequest>
{
    public CheckoutRequestValidator()
    {
        RuleFor(x => x.ShippingAddress).NotEmpty().MaximumLength(500);
    }
}

public class CreateCategoryRequestValidator : AbstractValidator<CreateCategoryRequest>
{
    public CreateCategoryRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Description).MaximumLength(500);
    }
}

public class AddCartItemRequestValidator : AbstractValidator<AddCartItemRequest>
{
    public AddCartItemRequestValidator()
    {
        RuleFor(x => x.ProductId).GreaterThan(0);
        RuleFor(x => x.Quantity).GreaterThan(0);
    }
}

public class UpdateCartItemRequestValidator : AbstractValidator<UpdateCartItemRequest>
{
    public UpdateCartItemRequestValidator()
    {
        RuleFor(x => x.Quantity).GreaterThanOrEqualTo(0);
    }
}

public class CreateReviewRequestValidator : AbstractValidator<CreateReviewRequest>
{
    public CreateReviewRequestValidator()
    {
        RuleFor(x => x.Rating).InclusiveBetween(1, 5);
        RuleFor(x => x.Comment).MaximumLength(1000);
    }
}

public class CreateCouponRequestValidator : AbstractValidator<CreateCouponRequest>
{
    public CreateCouponRequestValidator()
    {
        RuleFor(x => x.Code).NotEmpty().MaximumLength(40);
        RuleFor(x => x.Type).Must(t => t is "Percentage" or "FixedAmount")
            .WithMessage("Type must be Percentage or FixedAmount.");
        RuleFor(x => x.Value).GreaterThan(0);
        RuleFor(x => x.Value).LessThanOrEqualTo(100)
            .When(x => x.Type == "Percentage")
            .WithMessage("Percentage value must be <= 100.");
        RuleFor(x => x.MinOrderAmount).GreaterThanOrEqualTo(0);
        RuleFor(x => x.MaxUses).GreaterThan(0).When(x => x.MaxUses.HasValue);
    }
}

public class ValidateCouponRequestValidator : AbstractValidator<ValidateCouponRequest>
{
    public ValidateCouponRequestValidator()
    {
        RuleFor(x => x.Code).NotEmpty().MaximumLength(40);
        RuleFor(x => x.Subtotal).GreaterThanOrEqualTo(0);
    }
}

public class PayOrderRequestValidator : AbstractValidator<PayOrderRequest>
{
    public PayOrderRequestValidator()
    {
        RuleFor(x => x.Method).NotEmpty()
            .Must(m => m is "mock" or "cod" or "momo")
            .WithMessage("Method must be one of: mock, cod, momo.");
    }
}

public class UpdateOrderStatusRequestValidator : AbstractValidator<UpdateOrderStatusRequest>
{
    public UpdateOrderStatusRequestValidator()
    {
        RuleFor(x => x.Status).NotEmpty();
    }
}

public class UpdateFulfillmentStatusRequestValidator : AbstractValidator<UpdateFulfillmentStatusRequest>
{
    public UpdateFulfillmentStatusRequestValidator()
    {
        RuleFor(x => x.Status).NotEmpty()
            .Must(s => s is "Shipped" or "Delivered")
            .WithMessage("Status must be Shipped or Delivered.");
    }
}
