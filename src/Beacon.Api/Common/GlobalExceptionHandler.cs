using Microsoft.AspNetCore.Diagnostics;

namespace Beacon.Api.Common;

/// <summary>
/// The single last-resort net for unhandled exceptions. Maps every exception to an
/// RFC-9457 ProblemDetails body so the SPA always parses one error shape.
/// </summary>
internal sealed partial class GlobalExceptionHandler(
    IProblemDetailsService problemDetails,
    ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    private const string BadRequestTitle = "Request could not be processed.";

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var (status, title, detail) = exception switch
        {
            NotFoundException nf => (StatusCodes.Status404NotFound, "Not found.", nf.Message),
            ForbiddenException fb => (StatusCodes.Status403Forbidden, "Forbidden.", fb.Message),
            DomainException de => (StatusCodes.Status400BadRequest, BadRequestTitle, de.Message),
            _ => (StatusCodes.Status500InternalServerError, "An unexpected error occurred.", (string?)null),
        };

        if (status >= StatusCodes.Status500InternalServerError)
        {
            LogUnhandled(logger, exception, status, httpContext.Request.Method, httpContext.Request.Path);
        }
        else
        {
            LogHandledFault(logger, exception, status, httpContext.Request.Method, httpContext.Request.Path);
        }

        httpContext.Response.StatusCode = status;
        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = { Status = status, Title = title, Detail = detail },
        });
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Unhandled exception -> {Status} {Method} {Path}")]
    private static partial void LogUnhandled(ILogger logger, Exception exception, int status, string method, PathString path);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Handled fault -> {Status} {Method} {Path}")]
    private static partial void LogHandledFault(ILogger logger, Exception exception, int status, string method, PathString path);
}
