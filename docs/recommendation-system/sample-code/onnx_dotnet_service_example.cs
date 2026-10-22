// FILE THAM KHẢO (SAMPLE CODE): onnx_dotnet_service_example.cs
//
// Minh họa cách tạo IRecommendationService trong ASP.NET Core 9 (ECommerce.Application / Infrastructure)
// sử dụng Microsoft.ML.OnnxRuntime để nạp file ONNX từ Hugging Face.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ECommerce.Application.Services;

public record RecommendedProductDto(
    int Id,
    string Name,
    decimal Price,
    string CategoryName,
    float SimilarityScore
);

public interface IProductRecommendationService
{
    /// <summary>
    /// Gợi ý sản phẩm tương đồng cho 1 sản phẩm cụ thể ("Because you bought/viewed X")
    /// </summary>
    Task<List<RecommendedProductDto>> GetSimilarProductsAsync(int productId, int topK = 4);

    /// <summary>
    /// Gợi ý cá nhân hóa cho User dựa trên các OrderItem và Wishlist của họ
    /// </summary>
    Task<List<RecommendedProductDto>> GetRecommendationsForUserAsync(int userId, int topK = 5);
}

/// <summary>
/// Triển khai mẫu tính tương đồng Cosine trực tiếp trên C# (In-memory Vector Search)
/// </summary>
public class InMemoryProductRecommendationService : IProductRecommendationService
{
    // Lưu trữ bảng vector nhúng: ProductId -> float[] (384 hoặc 768 chiều từ Hugging Face model)
    private readonly Dictionary<int, float[]> _productVectors = new();

    public Task<List<RecommendedProductDto>> GetSimilarProductsAsync(int productId, int topK = 4)
    {
        if (!_productVectors.TryGetValue(productId, out var targetVector))
        {
            return Task.FromResult(new List<RecommendedProductDto>());
        }

        var results = _productVectors
            .Where(kvp => kvp.Key != productId)
            .Select(kvp => new
            {
                ProductId = kvp.Key,
                Score = ComputeCosineSimilarity(targetVector, kvp.Value)
            })
            .OrderByDescending(x => x.Score)
            .Take(topK)
            .Select(x => new RecommendedProductDto(
                x.ProductId,
                $"Product #{x.ProductId}",
                0m,
                "General",
                x.Score
            ))
            .ToList();

        return Task.FromResult(results);
    }

    public Task<List<RecommendedProductDto>> GetRecommendationsForUserAsync(int userId, int topK = 5)
    {
        // 1. Lấy danh sách ProductId mà user đã mua từ OrderItem trong DB
        // 2. Tính Vector trung bình sở thích của User (Centroid Vector)
        // 3. Tính Cosine Similarity giữa Centroid Vector và các sản phẩm chưa mua
        // 4. Trả về top K sản phẩm cao điểm nhất
        return Task.FromResult(new List<RecommendedProductDto>());
    }

    private static float ComputeCosineSimilarity(float[] vectorA, float[] vectorB)
    {
        if (vectorA.Length != vectorB.Length || vectorA.Length == 0) return 0f;

        float dot = 0f, normA = 0f, normB = 0f;
        for (int i = 0; i < vectorA.Length; i++)
        {
            dot += vectorA[i] * vectorB[i];
            normA += vectorA[i] * vectorA[i];
            normB += vectorB[i] * vectorB[i];
        }

        var denom = (float)(Math.Sqrt(normA) * Math.Sqrt(normB));
        return denom == 0f ? 0f : dot / denom;
    }
}
