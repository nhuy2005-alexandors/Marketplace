using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace ECommerce.Infrastructure.AI
{
    public sealed class RetrievalScoringChunk
    {
        public int ProductId { get; set; }
        public string Type { get; set; }
        public string Content { get; set; }
        public string ContentHash { get; set; }
        public float[] Vector { get; set; }
    }

    public sealed class RetrievalProductScore
    {
        public int ProductId { get; set; }
        public double LexicalScore { get; set; }
        public double SemanticScore { get; set; }
        public double CombinedScore { get; set; }
        public bool Included { get; set; }
        public int[] IncludedChunkIndexes { get; set; }
    }

    public sealed class RetrievalRankingHit
    {
        public int ProductId { get; set; }
        public double Score { get; set; }
        public double LexicalScore { get; set; }
        public double SemanticScore { get; set; }
    }

    public static class RetrievalScoring
    {
        public const double MinimumCombinedScore = 0.22;
        private static readonly Regex ExplicitSkuPattern = new Regex(@"\b[A-Z0-9]{2,}(?:-[A-Z0-9]{2,}){2,}\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);
        private static readonly HashSet<string> StopWords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "toi", "minh", "ban", "cho", "voi", "la", "co", "khong", "nao", "gi", "nhe", "nha", "va", "cua", "mot",
            "cac", "nhung", "hay", "duoc", "can", "muon", "tim", "mua", "loai", "san", "pham", "hang", "shop", "gia",
            "re", "de", "duoi", "tren", "tam", "khoang", "the", "please", "find", "show", "me", "some", "for", "with", "under", "over"
        };

        public static string Normalize(string value)
        {
            if (String.IsNullOrEmpty(value)) return String.Empty;
            var decomposed = value.ToLowerInvariant().Normalize(NormalizationForm.FormD);
            var builder = new StringBuilder(decomposed.Length);
            foreach (var ch in decomposed)
                if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark)
                    builder.Append(ch == 'đ' ? 'd' : ch);
            var normalized = Regex.Replace(Regex.Replace(builder.ToString(), @"[^\p{L}\p{N}]", " "), @"\s+", " ").Trim();
            return normalized;
        }

        public static double LexicalScore(string query, string content)
        {
            var normalizedQuery = Normalize(query);
            var normalizedContent = Normalize(content);
            if (normalizedQuery.Length == 0 || normalizedContent.Length == 0) return 0;
            var terms = normalizedQuery.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)
                .Where(term => term.Length > 1 && !StopWords.Contains(term))
                .Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            var score = normalizedContent.IndexOf(normalizedQuery, StringComparison.OrdinalIgnoreCase) >= 0 ? 0.8 : 0;
            var contentWords = new HashSet<string>(normalizedContent.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries), StringComparer.OrdinalIgnoreCase);
            if (terms.Length > 0) score += terms.Count(contentWords.Contains) / (double)terms.Length * 0.7;
            return Math.Min(1, score);
        }

        public static double Cosine(float[] left, float[] right)
        {
            if (left == null || right == null || left.Length == 0 || left.Length != right.Length) return 0;
            double dot = 0, leftNorm = 0, rightNorm = 0;
            for (var i = 0; i < left.Length; i++)
            {
                dot += left[i] * right[i];
                leftNorm += left[i] * left[i];
                rightNorm += right[i] * right[i];
            }
            return leftNorm == 0 || rightNorm == 0 ? 0 : dot / (Math.Sqrt(leftNorm) * Math.Sqrt(rightNorm));
        }

        public static RetrievalProductScore ScoreProduct(string query, float[] queryVector,
            IReadOnlyList<RetrievalScoringChunk> chunks, bool hybrid, double semanticThreshold, double semanticWeight)
        {
            var productId = chunks.Count == 0 ? 0 : chunks[0].ProductId;
            var sku = ExplicitSkuPattern.Match(query ?? String.Empty);
            if (sku.Success)
            {
                for (var i = 0; i < chunks.Count; i++)
                {
                    if (String.Equals(chunks[i].Type, "summary", StringComparison.OrdinalIgnoreCase) &&
                        Regex.IsMatch(chunks[i].Content ?? String.Empty, @"\bSKU:\s*" + Regex.Escape(sku.Value) + @"\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
                    {
                        return new RetrievalProductScore
                        {
                            ProductId = productId,
                            LexicalScore = 1,
                            SemanticScore = 1,
                            CombinedScore = 1,
                            Included = true,
                            IncludedChunkIndexes = new[] { i }
                        };
                    }
                }
                return new RetrievalProductScore { ProductId = productId, Included = false, IncludedChunkIndexes = new int[0] };
            }

            var lexical = 0.0;
            var semantic = 0.0;
            var chunkLexical = new double[chunks.Count];
            var chunkSemantic = new double[chunks.Count];
            for (var i = 0; i < chunks.Count; i++)
            {
                chunkLexical[i] = LexicalScore(query, chunks[i].Content);
                lexical = Math.Max(lexical, chunkLexical[i]);
                if (hybrid)
                {
                    chunkSemantic[i] = Cosine(queryVector, chunks[i].Vector);
                    semantic = Math.Max(semantic, chunkSemantic[i]);
                }
            }

            var combined = hybrid
                ? ((1 - semanticWeight) * lexical) + (semanticWeight * Math.Max(0, semantic))
                : lexical;
            var included = lexical > 0 || semantic >= semanticThreshold || combined >= MinimumCombinedScore;
            var sourceIndexes = new List<int>();
            if (included)
                for (var i = 0; i < chunks.Count; i++)
                    if (chunkLexical[i] > 0 || (hybrid && chunkSemantic[i] >= semanticThreshold)) sourceIndexes.Add(i);

            return new RetrievalProductScore
            {
                ProductId = productId,
                LexicalScore = lexical,
                SemanticScore = semantic,
                CombinedScore = combined,
                Included = included,
                IncludedChunkIndexes = sourceIndexes.ToArray()
            };
        }

        public static List<RetrievalRankingHit> RankForEvaluation(string query, float[] queryVector,
            IEnumerable<RetrievalScoringChunk> chunks, bool hybrid, double semanticThreshold, double semanticWeight, int limit)
        {
            var ranked = new List<RetrievalRankingHit>();
            foreach (var group in chunks.GroupBy(chunk => chunk.ProductId))
            {
                var productChunks = group.ToArray();
                var score = ScoreProduct(query, queryVector, productChunks, hybrid, semanticThreshold, semanticWeight);
                if (score.Included)
                    ranked.Add(new RetrievalRankingHit
                    {
                        ProductId = score.ProductId,
                        Score = score.CombinedScore,
                        LexicalScore = score.LexicalScore,
                        SemanticScore = score.SemanticScore
                    });
            }
            return ranked.OrderByDescending(hit => hit.Score).ThenBy(hit => hit.ProductId).Take(Math.Max(1, limit)).ToList();
        }
    }
}
