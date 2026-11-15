using System.Collections.Concurrent;

namespace ECommerce.Infrastructure.AI;

public sealed class EmbeddingCacheStore
{
    private const int MaximumEntries = 1000;
    private readonly ConcurrentDictionary<string, float[]> _vectors = new(StringComparer.Ordinal);
    private readonly ConcurrentQueue<string> _insertionOrder = new();

    public bool TryGet(string contentHash, out float[] vector) =>
        _vectors.TryGetValue(contentHash, out vector!);

    public void Set(string contentHash, float[] vector)
    {
        if (string.IsNullOrWhiteSpace(contentHash) || vector.Length == 0)
            return;

        if (_vectors.TryAdd(contentHash, vector))
            _insertionOrder.Enqueue(contentHash);
        else
            _vectors[contentHash] = vector;

        while (_vectors.Count > MaximumEntries && _insertionOrder.TryDequeue(out var oldest))
            _vectors.TryRemove(oldest, out _);
    }

    public int Count => _vectors.Count;
}
