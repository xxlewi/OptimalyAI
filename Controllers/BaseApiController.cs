using Microsoft.AspNetCore.Mvc;
using OAI.Core.DTOs;

namespace OptimalyAI.Controllers;

[ApiController]
[Route("api/[controller]")]
public abstract class BaseApiController : ControllerBase
{
    protected ActionResult<ApiResponse<T>> Ok<T>(T data, string message = "")
    {
        return base.Ok(ApiResponse<T>.SuccessResponse(data, message));
    }

    protected ActionResult<ApiResponse> Ok(string message = "")
    {
        return base.Ok(ApiResponse.SuccessResponse(message));
    }

    protected ActionResult<ApiResponse<T>> BadRequest<T>(string message, List<string>? errors = null)
    {
        return base.BadRequest(ApiResponse<T>.ErrorResponse(message, errors));
    }

    protected ActionResult<ApiResponse> BadRequest(string message, List<string>? errors = null)
    {
        return base.BadRequest(ApiResponse.ErrorResponse(message, errors));
    }

    protected ActionResult<ApiResponse<T>> NotFound<T>(string message = "Záznam nebyl nalezen")
    {
        return base.NotFound(ApiResponse<T>.ErrorResponse(message));
    }

    protected ActionResult<ApiResponse> NotFound(string message = "Záznam nebyl nalezen")
    {
        return base.NotFound(ApiResponse.ErrorResponse(message));
    }

    protected ActionResult<ApiResponse<T>> InternalServerError<T>(string message = "Interní chyba serveru")
    {
        return StatusCode(500, ApiResponse<T>.ErrorResponse(message));
    }

    protected ActionResult<ApiResponse> InternalServerError(string message = "Interní chyba serveru")
    {
        return StatusCode(500, ApiResponse.ErrorResponse(message));
    }
}