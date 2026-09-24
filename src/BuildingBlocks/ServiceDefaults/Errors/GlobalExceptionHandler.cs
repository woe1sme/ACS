using System.Data.Common;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace BuildingBlocks.ServiceDefaults.Errors;

internal sealed class GlobalExceptionHandler(IProblemDetailsService problemDetails, ILogger<GlobalExceptionHandler> logger)
    : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is OperationCanceledException && httpContext.RequestAborted.IsCancellationRequested)
        {
            logger.LogInformation("Request was cancelled by the client");
            httpContext.Response.StatusCode = 499;
            return true;
        }

        var (status, title, detail) = exception switch
        {
            AppException app => (app.StatusCode, app.Title, app.Message),
            BadHttpRequestException => (StatusCodes.Status400BadRequest, "Bad request", "The request body or parameters are malformed."),
            DbException or TimeoutException => (StatusCodes.Status503ServiceUnavailable, "Service unavailable", "A dependency is temporarily unavailable."),
            _ => (StatusCodes.Status500InternalServerError, "Internal server error", null),
        };

        if (status >= 500)
        {
            logger.LogError(exception, "Request failed with status {StatusCode}", status);
        }
        else
        {
            logger.LogInformation("Request rejected with status {StatusCode}: {Reason}", status, detail);
        }

        httpContext.Response.StatusCode = status;
        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = new ProblemDetails { Status = status, Title = title, Detail = detail },
        });
    }
}
