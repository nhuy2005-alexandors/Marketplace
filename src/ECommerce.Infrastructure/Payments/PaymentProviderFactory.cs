using ECommerce.Application.Interfaces;

namespace ECommerce.Infrastructure.Payments;

public class PaymentProviderFactory : IPaymentProviderFactory
{
    private readonly IReadOnlyDictionary<string, IPaymentProvider> _providers;

    public PaymentProviderFactory(IEnumerable<IPaymentProvider> providers)
    {
        _providers = providers.ToDictionary(p => p.Key, StringComparer.OrdinalIgnoreCase);
    }

    public IPaymentProvider Resolve(string methodOrProvider)
    {
        var key = (methodOrProvider ?? "mock").Trim().ToLowerInvariant();
        if (_providers.TryGetValue(key, out var provider))
            return provider;
        return _providers["mock"];
    }
}
