using System.Text.RegularExpressions;
using ECommerce.Application.DTOs.AI;

namespace ECommerce.Infrastructure.AI;

public static class AssistantContextualizer
{
    public sealed record ContextualizationResult(string Query, string Action);
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

    private static readonly Regex OrdinalReference = new(
        @"\b(?:(?:cai|mon|loai|san pham)\s+)?thu\s+(?:hai|ba|tu|nam|sau|bay|tam|chin|muoi|[0-9]+)\b|\b(?:the\s+)?(?:second|third|fourth|fifth)\s+(?:one|item|product)\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex ConversationalPrefix = new(
        @"^(?:(?:xin chao|chào bạn|chao ban|hello|hi|shop oi|ad oi|ban oi|cho minh hoi|cho toi hoi|mình muốn tìm|minh muon tim|tôi muốn tìm|toi muon tim|tìm giúp|tim giup|tìm cho mình|tim cho minh|tìm cho tôi|tim cho toi|tìm|tim|mua)\s+)+",
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

    public static ContextualizationResult ContextualizeQueryV2(string query,
        IReadOnlyList<AssistantConversationTurn>? recentTurns, bool hasNewProductTerm,
        bool hasTypedConstraint, IReadOnlyList<string> configuredFollowUpCues)
    {
        var turns = TakeLastThreeConversationPairs(recentTurns);
        var userTurns = turns.Where(turn => turn.Role.Equals("user", StringComparison.OrdinalIgnoreCase)).ToArray();
        var hasPriorProductTurn = userTurns.Any(turn =>
            AssistantIntentClassifier.Classify(turn.Content).Intent == "product_search");
        if (hasNewProductTerm)
            return new ContextualizationResult(query, hasPriorProductTurn ? "new_topic" : "none");
        var normalized = RetrievalScoring.Normalize(query);
        var cueMatch = configuredFollowUpCues.Any(cue => ContainsPhrase(normalized, cue));
        var existingRuleMatch = FollowUpPronouns.IsMatch(normalized) || RefinementPhrases.IsMatch(normalized) ||
                                EllipticalPrefix.IsMatch(normalized) || SpecificationInquiry.IsMatch(normalized);
        var isFollowUp = cueMatch || existingRuleMatch || hasTypedConstraint || OrdinalReference.IsMatch(normalized);
        if (!isFollowUp) return new ContextualizationResult(query, "none");
        if (OrdinalReference.IsMatch(normalized))
            return new ContextualizationResult(query, "needs_clarification");

        var priorSubject = userTurns.Reverse()
            .Where(turn => AssistantIntentClassifier.Classify(turn.Content).Intent == "product_search")
            .FirstOrDefault(turn => !IsFollowUpMessage(turn.Content, configuredFollowUpCues))?.Content;
        if (string.IsNullOrWhiteSpace(priorSubject))
        {
            var requiresPriorContext = cueMatch || FollowUpPronouns.IsMatch(normalized) ||
                                       RefinementPhrases.IsMatch(normalized) || EllipticalPrefix.IsMatch(normalized) ||
                                       OrdinalReference.IsMatch(normalized);
            return requiresPriorContext
                ? new ContextualizationResult(query, "needs_clarification")
                : new ContextualizationResult(query, "none");
        }

        var normSubject = RetrievalScoring.Normalize(priorSubject);
        var cleanedSubject = ConversationalPrefix.Replace(normSubject, string.Empty).Trim();
        var strippedQuery = FollowUpPronouns.Replace(normalized, " ").Trim();
        strippedQuery = Regex.Replace(strippedQuery, @"\s+", " ").Trim();
        return new ContextualizationResult($"{cleanedSubject} {strippedQuery}".Trim(), "followup");
    }

    public static IReadOnlyList<AssistantConversationTurn> TakeLastThreeConversationPairs(
        IReadOnlyList<AssistantConversationTurn>? recentTurns)
    {
        if (recentTurns is null || recentTurns.Count < 2) return [];
        var pairs = new List<AssistantConversationTurn[]>();
        for (var index = 0; index < recentTurns.Count - 1;)
        {
            var user = recentTurns[index];
            var assistant = recentTurns[index + 1];
            if (user.Role.Equals("user", StringComparison.OrdinalIgnoreCase) &&
                assistant.Role.Equals("assistant", StringComparison.OrdinalIgnoreCase))
            {
                pairs.Add([user, assistant]);
                index += 2;
            }
            else index++;
        }
        return pairs.TakeLast(3).SelectMany(pair => pair).ToArray();
    }

    private static bool IsFollowUpMessage(string message, IReadOnlyList<string> configuredFollowUpCues)
    {
        var normalized = RetrievalScoring.Normalize(message);
        return FollowUpPronouns.IsMatch(normalized) || RefinementPhrases.IsMatch(normalized) ||
               EllipticalPrefix.IsMatch(normalized) ||
               OrdinalReference.IsMatch(normalized) ||
               configuredFollowUpCues.Any(cue => ContainsPhrase(normalized, cue));
    }

    private static bool ContainsPhrase(string normalized, string phrase)
    {
        var token = RetrievalScoring.Normalize(phrase);
        return token.Length > 0 && Regex.IsMatch(normalized,
            @"(?<![\p{L}\p{N}])" + Regex.Escape(token) + @"(?![\p{L}\p{N}])");
    }
}
