namespace ECommerce.Application.DTOs.Reviews;

public record ReviewDto(int Id, int UserId, string UserName, int Rating, string? Comment, DateTime CreatedAt);

public record CreateReviewRequest(int Rating, string? Comment);
