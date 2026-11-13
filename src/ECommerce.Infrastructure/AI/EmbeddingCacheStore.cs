using System.Text.Json;

namespace ECommerce.Infrastructure.AI;

public sealed record EmbeddingCacheEntry(
    string CacheKey,
    string ContentHash,
    string ModelName,
    int Dimensions,
    int ProductId,
    string ChunkType,
    float[] Vector);

public interface IEmbeddingCacheStore
{
    Task<IReadOnlyList<EmbeddingCacheEntry>> ReadAsync(CancellationToken ct = default);
    Task WriteAsync(IReadOnlyCollection<EmbeddingCacheEntry> entries, CancellationToken ct = default);
}

public sealed class LocalEmbeddingCacheStore(string path) : IEmbeddingCacheStore
{
    private static readonly SemaphoreSlim FileGate = new(1, 1);
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly string _path = Path.GetFullPath(path);

    public string CachePath => _path;

    public static string ResolveDefaultPath()
    {
        for (DirectoryInfo? directory = new(Directory.GetCurrentDirectory()); directory is not null; directory = directory.Parent)
        {
            var gitPath = Path.Combine(directory.FullName, ".git");
            if (Directory.Exists(gitPath) || File.Exists(gitPath))
                return Path.Combine(directory.FullName, "obj", "rag-embedding-cache.json");
        }

        return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Marketplace", "rag-cache", "rag-embedding-cache.json");
    }

    public async Task<IReadOnlyList<EmbeddingCacheEntry>> ReadAsync(CancellationToken ct = default)
    {
        await FileGate.WaitAsync(ct);
        try
        {
            if (!File.Exists(_path)) return [];
            await using var stream = new FileStream(_path, FileMode.Open, FileAccess.Read, FileShare.Read,
                65536, FileOptions.Asynchronous | FileOptions.SequentialScan);
            var entries = await JsonSerializer.DeserializeAsync<List<EmbeddingCacheEntry>>(stream, JsonOptions, ct);
            return entries?.Where(IsValid).ToArray() ?? [];
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or NotSupportedException)
        {
            return [];
        }
        finally { FileGate.Release(); }
    }

    public async Task WriteAsync(IReadOnlyCollection<EmbeddingCacheEntry> entries, CancellationToken ct = default)
    {
        if (entries.Count == 0) return;
        await FileGate.WaitAsync(ct);
        string? tempPath = null;
        try
        {
            var merged = new Dictionary<string, EmbeddingCacheEntry>(StringComparer.Ordinal);
            foreach (var old in await ReadUnlockedAsync(ct)) merged[old.CacheKey] = old;
            foreach (var entry in entries.Where(IsValid)) merged[entry.CacheKey] = entry;

            var directory = Path.GetDirectoryName(_path)!;
            Directory.CreateDirectory(directory);
            tempPath = Path.Combine(directory, $".{Path.GetFileName(_path)}.{Guid.NewGuid():N}.tmp");
            await using (var stream = new FileStream(tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.None,
                65536, FileOptions.Asynchronous | FileOptions.WriteThrough))
            {
                await JsonSerializer.SerializeAsync(stream, merged.Values, JsonOptions, ct);
                await stream.FlushAsync(ct);
            }
            File.Move(tempPath, _path, overwrite: true);
            tempPath = null;
        }
        finally
        {
            if (tempPath is not null)
            {
                try { File.Delete(tempPath); }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }
            FileGate.Release();
        }
    }

    public static string CreateKey(string contentHash, string modelName, int dimensions) =>
        $"{contentHash}:{modelName}:{dimensions}";

    private async Task<IReadOnlyList<EmbeddingCacheEntry>> ReadUnlockedAsync(CancellationToken ct)
    {
        if (!File.Exists(_path)) return [];
        try
        {
            await using var stream = new FileStream(_path, FileMode.Open, FileAccess.Read, FileShare.Read,
                65536, FileOptions.Asynchronous | FileOptions.SequentialScan);
            var entries = await JsonSerializer.DeserializeAsync<List<EmbeddingCacheEntry>>(stream, JsonOptions, ct);
            return entries?.Where(IsValid).ToArray() ?? [];
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or NotSupportedException)
        {
            return [];
        }
    }

    private static bool IsValid(EmbeddingCacheEntry entry) =>
        !string.IsNullOrWhiteSpace(entry.ContentHash) &&
        !string.IsNullOrWhiteSpace(entry.ModelName) &&
        entry.Dimensions > 0 &&
        entry.ProductId > 0 &&
        !string.IsNullOrWhiteSpace(entry.ChunkType) &&
        entry.Vector is { Length: > 0 } && entry.Vector.Length == entry.Dimensions &&
        entry.CacheKey == CreateKey(entry.ContentHash, entry.ModelName, entry.Dimensions);
}
