using System.Text.RegularExpressions;
using System.Linq;

namespace ECommerce.Infrastructure.AI
{
public sealed class AssistantIntentDecision
{
    public AssistantIntentDecision(string intent, string lexicalGateQuery) { Intent = intent; LexicalGateQuery = lexicalGateQuery; }
    public string Intent { get; private set; }
    public string LexicalGateQuery { get; private set; }
    public bool IsScopeGuard { get { return Intent != "product_search"; } }
}

public static class AssistantIntentClassifier
{
    // TODO: owner should confirm the storefront name and support channel before release.
    public const string GreetingAnswer = "Mình là trợ lý mua sắm của MiniShop. Mình giúp bạn tìm sản phẩm theo nhu cầu, giá, danh mục hay còn hàng.";
    public const string OutOfScopeAnswer = "Mình chỉ hỗ trợ tìm sản phẩm trong cửa hàng nên chưa trả lời được câu này. Bạn muốn tìm sản phẩm nào không?";
    public const string BlockedAnswer = "Mình không thể chia sẻ thông tin cấu hình hay dữ liệu nội bộ. Mình có thể giúp bạn tìm sản phẩm.";
    public const string AccountAnswer = "Mình không thực hiện được hoàn tiền hay thay đổi đơn hàng. Bạn xem mục đơn hàng của mình hoặc liên hệ hỗ trợ của cửa hàng nhé.";
    public const string SubjectiveCaveat = "Lưu ý: mình chỉ có thông số và đánh giá trong cửa hàng, chưa thể xác nhận cảm nhận chủ quan.";

    private static readonly Regex SqlStatement = Rx(@"\b(?:select\b.+\bfrom\b|insert\s+into\b|update\b.+\bset\b|delete\s+from\b|drop\s+(?:table|database|index)\b|alter\s+table\b|create\s+(?:table|database|index)\b|truncate\s+table\b)");
    private static readonly string[] Sensitive = {"system prompt", "system instruction", "api key", "apikey", "khoa api", "khoa he thong", "secret", "token", "cau hinh", "configuration", "database", "co so du lieu", "ignore previous instructions", "ignore all previous instructions", "bo qua cac huong dan truoc"};
    private static readonly string[] Account = {"hoan tien", "refund", "cancel order", "cancel my order", "huy don", "huy don hang", "doi mat khau", "quen mat khau", "forgot password", "forgot my password", "i forgot password", "change password", "change my password", "change my account password", "update password", "update my password", "reset password", "reset password for me", "reset my password", "order status", "trang thai don", "don hang cua toi", "don nay dang o dau", "doi dia chi", "change address", "update address", "update my address", "change my address", "change my delivery address"};
    private static readonly Regex PasswordDisclosure = Rx(@"\b(?:show|reveal|tell\s+me|print|disclose|share|what\s+is|cho\s+(?:(?:toi|tao|ban)\s+)?xem|tiet\s+lo|dua\s+toi)\b.{0,32}\b(?:password|mat\s+khau)\b|\b(?:password|mat\s+khau)\b.{0,32}\b(?:show|reveal|tell|print|disclose|share|tiet\s+lo|la\s+gi|is\s+that)\b");
    private static readonly string[] OutOfScope = {"gia vang", "gold price", "gold prices", "thoi tiet", "weather", "forecast", "tin tuc", "news", "headlines", "chung khoan", "stock market", "exchange rate", "ty gia", "quan an", "nha hang", "restaurant", "restaurants", "cafe gan nhat", "quan gan nhat", "tiem gan nhat", "dia diem gan nhat", "near me", "nearby", "nearest", "nearest place", "nearest location", "places near me", "location near me", "duong di", "chi duong", "directions", "route to", "how do i get to"};
    private static readonly string[] Subjective = {"nghe co da tai khong", "nghe co hay khong", "co ben khong", "co thoai mai khong", "does it sound good", "does it sound great", "is it durable", "does it feel comfortable"};
    private static readonly Regex OutOfScopeRemover = Rx(@"\b(?:gia\s+vang|gold\s+prices?|thoi\s+tiet|weather|forecast|tin\s+tuc|news|headlines|chung\s+khoan|stock\s+market|exchange\s+rate|ty\s+gia|quan\s+an|nha\s+hang|restaurants?|cafe\s+gan\s+nhat|quan\s+gan\s+nhat|tiem\s+gan\s+nhat|dia\s+diem\s+gan\s+nhat|nearest\s+place|nearest\s+location|places\s+near\s+me|location\s+near\s+me|near\s+me|nearby|nearest|duong\s+di|chi\s+duong|directions|route\s+to|how\s+do\s+i\s+get\s+to)\b");

    public static AssistantIntentDecision Classify(string query)
    {
        var text = RetrievalScoring.Normalize(query);
        var accountAction = Account.Any(term => ContainsPhrase(text, term));
        var passwordMention = ContainsPhrase(text, "password") || ContainsPhrase(text, "mat khau");
        if (SqlStatement.IsMatch(text) || PasswordDisclosure.IsMatch(text) || Sensitive.Any(term => ContainsPhrase(text, term)) || (passwordMention && !accountAction)) return new AssistantIntentDecision("blocked", string.Empty);
        if (accountAction) return new AssistantIntentDecision("account_action", string.Empty);
        if (IsGreeting(text)) return new AssistantIntentDecision("greeting", string.Empty);
        if (OutOfScope.Any(term => ContainsPhrase(text, term)))
        {
            var remainder = Regex.Replace(OutOfScopeRemover.Replace(text, " "), @"\s+", " ").Trim();
            return new AssistantIntentDecision("out_of_scope", remainder);
        }
        return new AssistantIntentDecision("product_search", string.Empty);
    }

    public static bool HasSubjectiveQuestion(string query)
    {
        var text = RetrievalScoring.Normalize(query);
        return Subjective.Any(term => ContainsPhrase(text, term));
    }

    private static bool IsGreeting(string text)
    {
        if (new[] {"ban la ai", "may la ai", "em la ai", "who are you", "what can you do", "ban lam duoc gi"}.Any(term => ContainsPhrase(text, term))) return true;
        return Regex.IsMatch(text, @"^(?:(?:please\s+)?help(?:\s+me)?(?:\s+please)?|xin chao|chao ban|hello(?: there)?(?: assistant)?|hi(?: assistant)?|hey(?: assistant)?|alo tro ly|good morning)[.!? ]*$");
    }

    private static bool ContainsPhrase(string text, string phrase)
    {
        return Regex.IsMatch(text, @"(?<![\p{L}\p{N}])" + Regex.Escape(phrase) + @"(?![\p{L}\p{N}])");
    }

    private static Regex Rx(string pattern)
    {
        return new Regex(pattern, RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
    }
}
}
