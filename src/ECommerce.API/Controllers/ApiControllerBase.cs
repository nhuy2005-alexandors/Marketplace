using ECommerce.Application.Common;
using ECommerce.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace ECommerce.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public abstract class ApiControllerBase : ControllerBase
{
    private ICurrentUser? _currentUser;
    protected ICurrentUser CurrentUser =>
        _currentUser ??= HttpContext.RequestServices.GetRequiredService<ICurrentUser>();

    protected int UserId => CurrentUser.UserId
        ?? throw new InvalidOperationException("User id not present on authenticated request.");

    protected bool IsAdmin => User.IsInRole("Admin");

    protected ActionResult<T> ToResponse<T>(Result<T> result) =>
        result.Success ? Ok(result.Value) : MapError(result);

    protected ActionResult ToResponse(Result result) =>
        result.Success ? Ok() : MapError(result);

    protected ActionResult MapError(Result result)
    {
        var problem = new { error = result.Error };
        return result.ErrorType switch
        {
            ErrorType.NotFound => NotFound(problem),
            ErrorType.Unauthorized => Unauthorized(problem),
            ErrorType.Forbidden => StatusCode(StatusCodes.Status403Forbidden, problem),
            ErrorType.Conflict => Conflict(problem),
            _ => BadRequest(problem)
        };
    }
}
