using Microsoft.AspNetCore.Mvc;
using OAI.Core.DTOs;

namespace OptimalyAI.Controllers;

[ApiController]
[Route("api/[controller]")]
public abstract class BaseApiController : ControllerBase
{
    protected IActionResult Ok<T>(T data, string message = "")
    {
        return base.Ok(ApiResponse<T>.SuccessResponse(data, message));
    }

    protected IActionResult Ok(string message = "")
    {
        return base.Ok(ApiResponse.SuccessResponse(message));
    }

    protected IActionResult BadRequest<T>(string message, List<string>? errors = null)
    {
        return base.BadRequest(ApiResponse<T>.ErrorResponse(message, errors));
    }

    protected IActionResult BadRequest(string message, List<string>? errors = null)
    {
        return base.BadRequest(ApiResponse.ErrorResponse(message, errors));
    }

    protected IActionResult NotFound<T>(string message = "Záznam nebyl nalezen")
    {
        return base.NotFound(ApiResponse<T>.ErrorResponse(message));
    }

    protected IActionResult NotFound(string message = "Záznam nebyl nalezen")
    {
        return base.NotFound(ApiResponse.ErrorResponse(message));
    }

    protected IActionResult InternalServerError<T>(string message = "Interní chyba serveru")
    {
        return StatusCode(500, ApiResponse<T>.ErrorResponse(message));
    }

    protected IActionResult InternalServerError(string message = "Interní chyba serveru")
    {
        return StatusCode(500, ApiResponse.ErrorResponse(message));
    }
}