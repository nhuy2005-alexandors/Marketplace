using ECommerce.Application.DTOs.Catalog;
using ECommerce.Application.Validators;

namespace ECommerce.Tests.Unit;

public class ProductQueryValidatorTests
{
    private static ProductQuery Query(
        decimal? minPrice = null,
        decimal? maxPrice = null,
        List<ProductAttributeFilter>? filters = null) =>
        new(null, null, null, minPrice, maxPrice, null, AttributeFilters: filters);

    [Fact]
    public void Rejects_price_filter_with_more_than_two_decimal_places()
    {
        var result = new ProductQueryValidator().Validate(Query(minPrice: 10.001m));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(ProductQuery.MinPrice));
    }

    [Fact]
    public void Rejects_price_filter_outside_decimal_18_2_range()
    {
        var result = new ProductQueryValidator().Validate(Query(maxPrice: 10000000000000000m));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(ProductQuery.MaxPrice));
    }

    [Fact]
    public void Rejects_numeric_attribute_filter_with_more_than_four_decimal_places()
    {
        var result = new ProductQueryValidator().Validate(Query(filters:
            [new ProductAttributeFilter(1, MinValue: 1.23456m)]));

        Assert.False(result.IsValid);
    }

    [Fact]
    public void Rejects_numeric_attribute_filter_outside_decimal_18_4_range()
    {
        var result = new ProductQueryValidator().Validate(Query(filters:
            [new ProductAttributeFilter(1, MaxValue: 100000000000000m)]));

        Assert.False(result.IsValid);
    }

    [Fact]
    public void Rejects_text_attribute_filter_longer_than_storage_limit()
    {
        var result = new ProductQueryValidator().Validate(Query(filters:
            [new ProductAttributeFilter(1, ValueText: new string('x', 2001))]));

        Assert.False(result.IsValid);
    }

    [Fact]
    public void Accepts_storage_boundaries_for_price_and_numeric_attributes()
    {
        var result = new ProductQueryValidator().Validate(Query(
            minPrice: 9999999999999999.99m,
            filters: [new ProductAttributeFilter(1, MinValue: 99999999999999.9999m)]));

        Assert.True(result.IsValid, string.Join("; ", result.Errors.Select(e => e.ErrorMessage)));
    }
}
