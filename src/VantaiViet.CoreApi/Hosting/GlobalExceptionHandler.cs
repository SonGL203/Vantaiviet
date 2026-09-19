using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace VantaiViet.CoreApi.Hosting;

internal sealed class GlobalExceptionHandler(
    IProblemDetailsService problemDetailsService,
    ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var (status, title, errorCode) = exception switch
        {
            UnauthorizedAccessException => (StatusCodes.Status401Unauthorized, "Authentication failed.", "auth.invalid_credentials"),
            InvalidOperationException => (StatusCodes.Status400BadRequest, "Request cannot be completed.", "business.rule_violated"),
            _ => (StatusCodes.Status500InternalServerError, "An unexpected error occurred.", "server.unexpected")
        };
        if (status >= StatusCodes.Status500InternalServerError)
        {
            logger.LogError(exception, "Unhandled request failure. TraceId: {TraceId}", httpContext.TraceIdentifier);
        }
        else
        {
            logger.LogInformation("Request rejected: {Message}. TraceId: {TraceId}", exception.Message, httpContext.TraceIdentifier);
        }
        httpContext.Response.StatusCode = status;

        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = new ProblemDetails
            {
                Status = status,
                Title = title,
                Extensions = { ["errorCode"] = errorCode }
            }
        });
    }
}
