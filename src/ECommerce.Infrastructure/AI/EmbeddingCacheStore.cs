using System.Collections.Concurrent;

namespace ECommerce.Infrastructure.AI;

public sealed class EmbeddingCacheStore
{
    private readonly ConcurrentDictionary<string, float[]> _vectors = new(StringComparer.Ordinal);

    public bool TryGet(string contentHash, out float[] vector) =>
        _vectors.TryGetValue(contentHash, out vector!);

    public void Set(string contentHash, float[] vector)
    {
        if (string.IsNullOrWhiteSpace(contentHash) || vector.Length == 0)
            return;

        _vectors[contentHash] = vector;
    }

    public int Count => _vectors.Count;
}
