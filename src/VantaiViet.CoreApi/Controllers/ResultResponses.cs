using Microsoft.AspNetCore.Mvc;
using VantaiViet.CoreApi.DTOs;

namespace VantaiViet.CoreApi.Controllers;

internal static class ResultResponses
{
    /// <summary>Maps a service result to the { success, data, traceId } envelope or a ProblemDetails carrying "scope.code".</summary>
    public static IActionResult FromResult<T>(this ControllerBase controller, ShipmentResult<T> result, string scope)
    {
        if (result.ErrorCode is null)
        {
            return controller.Ok(ApiResponse<T>.Ok(result.Data!, controller.HttpContext.TraceIdentifier));
        }

        var status = StatusFor(result.ErrorCode);
        var problem = controller.ProblemDetailsFactory.CreateProblemDetails(controller.HttpContext, statusCode: status, title: "Request rejected.");
        problem.Extensions["errorCode"] = scope + "." + result.ErrorCode;
        return new ObjectResult(problem) { StatusCode = status };
    }

    private static int StatusFor(string errorCode)
    {
        return errorCode switch
        {
            "not_found" => StatusCodes.Status404NotFound,
            "forbidden" or "driver_not_eligible" => StatusCodes.Status403Forbidden,
            "conflict" or "locked" or "identity_unavailable" or "has_takers" or "photo_limit" or "chat_closed" or "invalid_transition" or "already_reviewed" or "limit_reached" or "duplicate_label"
                or "already_saved" or "duplicate_name" or "resource_unavailable" or "duplicate_route" => StatusCodes.Status409Conflict,
            "rate_limited" => StatusCodes.Status429TooManyRequests,
            "not_configured" or "unavailable" => StatusCodes.Status503ServiceUnavailable,
            _ => StatusCodes.Status400BadRequest
        };
    }
}
