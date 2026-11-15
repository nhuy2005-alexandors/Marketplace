using ECommerce.Application.DTOs.AI;

namespace ECommerce.Infrastructure.AI;

public sealed record ContextualizedAssistantQuery(
    string Query,
    IReadOnlyList<AssistantConversationTurn> RecentTurns);

public static class AssistantContextualizer
{
    private const int MaximumContextualizedQueryLength = 1000;

    private static readonly string[] FollowUpCues =
    [
        "it", "this", "that", "those", "mau", "màu", "re hon", "cheaper",
        "other", "còn hàng", "con hang", "stock", "nó", "cái này", "cai nay",
        "loại đó", "loai do", "có không", "co khong", "what about"
    ];

    private static readonly string[] NewTopicPrefixes =
    [
        "tìm ", "tim ", "search ", "find ", "show me ", "i need ",
        "i am looking for ", "i'm looking for ", "looking for ",
        "gợi ý ", "goi y ", "mua ", "recommend "
    ];

    public static ContextualizedAssistantQuery Contextualize(
        string query,
        IReadOnlyList<AssistantConversationTurn>? recentTurns)
    {
        var boundedTurns = (recentTurns ?? [])
            .Where(turn =>
                (string.Equals(turn.Role, "user", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(turn.Role, "assistant", StringComparison.OrdinalIgnoreCase)) &&
                !string.IsNullOrWhiteSpace(turn.Content) &&
                turn.Content.Length <= 500)
            .TakeLast(6)
            .ToArray();

        var trimmedQuery = query.Trim();
        if (boundedTurns.Length == 0 || StartsNewTopic(trimmedQuery) || !LooksLikeFollowUp(trimmedQuery))
            return new ContextualizedAssistantQuery(trimmedQuery, boundedTurns);

        var subject = boundedTurns
            .Where(turn => string.Equals(turn.Role, "user", StringComparison.OrdinalIgnoreCase))
            .Reverse()
            .Select(turn => turn.Content.Trim())
.FirstOrDefault(content => !LooksLikeFollowUp(content));

        if (string.IsNullOrWhiteSpace(subject))
            return new ContextualizedAssistantQuery(trimmedQuery, boundedTurns);

        var availableSubjectLength = MaximumContextualizedQueryLength - trimmedQuery.Length - 1;
        if (availableSubjectLength <= 0)
            return new ContextualizedAssistantQuery(trimmedQuery, boundedTurns);

        var boundedSubject = subject.Length > availableSubjectLength
            ? subject[..availableSubjectLength].TrimEnd()
            : subject;

        return string.IsNullOrWhiteSpace(boundedSubject)
            ? new ContextualizedAssistantQuery(trimmedQuery, boundedTurns)
            : new ContextualizedAssistantQuery($"{boundedSubject} {trimmedQuery}", boundedTurns);
    }

    private static bool LooksLikeFollowUp(string query)
    {
        var normalized = Normalize(query);
        return FollowUpCues.Any(cue => normalized.Contains(Normalize(cue), StringComparison.Ordinal));
    }

    private static bool StartsNewTopic(string query)
    {
        var normalized = Normalize(query);
        return NewTopicPrefixes.Any(prefix =>
            normalized.StartsWith(Normalize(prefix), StringComparison.Ordinal));
    }

    private static string Normalize(string value)
    {
        var decomposed = value.ToLowerInvariant().Normalize(System.Text.NormalizationForm.FormD);
        var builder = new System.Text.StringBuilder(decomposed.Length);
        foreach (var character in decomposed)
        {
            if (System.Globalization.CharUnicodeInfo.GetUnicodeCategory(character) !=
                System.Globalization.UnicodeCategory.NonSpacingMark)
                builder.Append(character);
        }

        return builder.ToString()
            .Normalize(System.Text.NormalizationForm.FormC)
            .Replace('đ', 'd')
            .Replace('Đ', 'd');
    }
}
