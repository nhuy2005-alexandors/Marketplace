using ECommerce.Application.Interfaces;
using Microsoft.Extensions.Configuration;

namespace ECommerce.Infrastructure.Storage;

public class LocalFileStorage : IFileStorage
{
    private const int MaxBytes = 5_000_000;
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
        // Buffer to memory: incoming stream may be non-seekable, and we must inspect
        // the header before trusting the file. Extension alone is spoofable — a script
        // renamed to .jpg passes an ext check but not a magic-byte check.
        using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer, ct);
        if (buffer.Length == 0)
            throw new InvalidOperationException("Empty file.");
        if (buffer.Length > MaxBytes)
            throw new InvalidOperationException("File too large.");

        var bytes = buffer.GetBuffer().AsSpan(0, (int)buffer.Length);
        var ext = DetectImageExtension(bytes)
            ?? throw new InvalidOperationException("Unsupported or invalid image content.");

        var name = $"{Guid.NewGuid():N}{ext}";
        var fullPath = Path.Combine(_root, name);
        buffer.Position = 0;
        await using (var fs = File.Create(fullPath))
            await buffer.CopyToAsync(fs, ct);

        return $"{_publicPath}/{name}";
    }

    // Derive the canonical extension from actual magic bytes; returns null for non-images.
    private static string? DetectImageExtension(ReadOnlySpan<byte> b)
    {
        if (b.Length >= 3 && b[0] == 0xFF && b[1] == 0xD8 && b[2] == 0xFF)
            return ".jpg";
        if (b.Length >= 8 && b[0] == 0x89 && b[1] == 0x50 && b[2] == 0x4E && b[3] == 0x47 &&
            b[4] == 0x0D && b[5] == 0x0A && b[6] == 0x1A && b[7] == 0x0A)
            return ".png";
        if (b.Length >= 6 && b[0] == 0x47 && b[1] == 0x49 && b[2] == 0x46 && b[3] == 0x38 &&
            (b[4] == 0x37 || b[4] == 0x39) && b[5] == 0x61)
            return ".gif";
        if (b.Length >= 12 && b[0] == 0x52 && b[1] == 0x49 && b[2] == 0x46 && b[3] == 0x46 &&
            b[8] == 0x57 && b[9] == 0x45 && b[10] == 0x42 && b[11] == 0x50)
            return ".webp";
        return null;
    }
}
