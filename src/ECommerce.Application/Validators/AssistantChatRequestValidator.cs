using ECommerce.Application.DTOs.AI;
using FluentValidation;

namespace ECommerce.Application.Validators;

public sealed class AssistantChatRequestValidator : AbstractValidator<AssistantChatRequest>
{
    public AssistantChatRequestValidator()
    {
        RuleFor(request => request.Query)
            .NotEmpty()
            .MaximumLength(1000);

        RuleFor(request => request.RecentTurns)
            .Must(turns => turns is null || turns.Count <= 6)
            .WithMessage("At most six recent conversation turns are allowed.");

        RuleForEach(request => request.RecentTurns)
            .ChildRules(turn =>
            {
                turn.RuleFor(item => item.Role)
                    .Must(role => string.Equals(role, "user", StringComparison.OrdinalIgnoreCase) ||
                                  string.Equals(role, "assistant", StringComparison.OrdinalIgnoreCase))
                    .WithMessage("Conversation roles must be user or assistant.");
                turn.RuleFor(item => item.Content)
                    .NotEmpty()
                    .MaximumLength(500);
            });

        RuleFor(request => request.Filters)
            .Must(filters => filters is null ||
                (filters.CategoryId is null or > 0 &&
                 filters.BrandId is null or > 0 &&
                 filters.SellerId is null or > 0 &&
                 filters.MinPrice is null or >= 0 &&
                 filters.MaxPrice is null or >= 0 &&
                 (!filters.MinPrice.HasValue || !filters.MaxPrice.HasValue ||
                  filters.MinPrice.Value <= filters.MaxPrice.Value)))
            .WithMessage("Assistant catalog filters are invalid.");
    }
}
