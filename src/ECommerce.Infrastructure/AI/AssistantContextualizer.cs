using System.Text.RegularExpressions;
using ECommerce.Application.DTOs.AI;

namespace ECommerce.Infrastructure.AI;

public static class AssistantContextualizer
{
    private static readonly Regex FollowUpPronouns = new(
        @"\b(?:no|cai nay|cai do|loai nay|mon nay|san pham nay|thu nay|em nay|con nay|chung|it|this|that|these|those)\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex RefinementPhrases = new(
        @"\b(?:mau khac|mau trang|mau den|mau do|mau xanh|mau vang|re hon|dat hon|size khac|co khac|hang khac|thuong hieu khac|loai khac|ban khac|con hang khong|con khong|co khong|cheaper|more expensive|another color|different brand)\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex EllipticalPrefix = new(
        @"^(?:con\b|co\b|the con\b|vay con\b|the\b|vay\b|how about\b|what about\b|is there\b|any other\b)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex SpecificationInquiry = new(
        @"\b(?:bao hanh|pin|trong luong|nang|xuat xu|san xuat o dau|chong on|bluetooth|chong nuoc|kich thuoc|warranty|battery|weight|waterproof)\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex ConversationalPrefix = new(
        @"^(?:(?:xin chao|chao ban|hello|hi|shop oi|ad oi|ban oi|cho minh hoi|cho toi hoi|minh muon tim|toi muon tim|tim giup|tim cho minh|tim cho toi|tim|mua)\s+)+",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    public static string ContextualizeQuery(string query, IReadOnlyList<AssistantConversationTurn>? recentTurns)
    {
        if (string.IsNullOrWhiteSpace(query) || recentTurns == null || recentTurns.Count == 0)
            return query;

        var normalized = RetrievalScoring.Normalize(query);
        var isFollowUp = FollowUpPronouns.IsMatch(normalized) ||
                         RefinementPhrases.IsMatch(normalized) ||
                         EllipticalPrefix.IsMatch(normalized) ||
                         SpecificationInquiry.IsMatch(normalized);

        if (!isFollowUp) return query;

        string? previousSubject = null;
        for (var i = recentTurns.Count - 1; i >= 0; i--)
        {
            var turn = recentTurns[i];
            if (string.IsNullOrWhiteSpace(turn.Content)) continue;

            if (turn.Role.Equals("user", StringComparison.OrdinalIgnoreCase))
            {
                var normTurn = RetrievalScoring.Normalize(turn.Content);
                var decision = AssistantIntentClassifier.Classify(turn.Content);
                if (decision.Intent == "product_search")
                {
                    var cleaned = ConversationalPrefix.Replace(normTurn, "").Trim();
                    if (!string.IsNullOrWhiteSpace(cleaned) && cleaned.Length >= 3)
                    {
                        previousSubject = cleaned;
                        break;
                    }
                }
            }
        }

        if (string.IsNullOrWhiteSpace(previousSubject)) return query;

        var strippedQuery = FollowUpPronouns.Replace(normalized, " ").Trim();
        strippedQuery = Regex.Replace(strippedQuery, @"\s+", " ").Trim();

        return $"{previousSubject} {strippedQuery}".Trim();
    }
}
