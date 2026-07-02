using ECommerce.Application.Interfaces;
using Microsoft.Extensions.Configuration;

namespace ECommerce.Infrastructure.Storage;

public class LocalFileStorage : IFileStorage
{
    private readonly string _root;
    private readonly string _publicPath;

    public LocalFileStorage(IConfiguration config)
    {
        _publicPath = config["Storage:PublicPath"] ?? "/uploads";
        _root = config["Storage:RootPath"] ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads");
        Directory.CreateDirectory(_root);
    }

    public async Task<string> SaveImageAsync(Stream content, string fileName, CancellationToken ct = default)
    {
        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        var allowed = new[] { ".jpg", ".jpeg", ".png", ".webp", ".gif" };
        if (!allowed.Contains(ext))
            throw new InvalidOperationException("Unsupported image format.");

        var name = $"{Guid.NewGuid():N}{ext}";
        var fullPath = Path.Combine(_root, name);
        await using (var fs = File.Create(fullPath))
            await content.CopyToAsync(fs, ct);

        return $"{_publicPath}/{name}";
    }
}
