using Microsoft.AspNetCore.Diagnostics;
using ShiftSync.Domain.Exceptions;

namespace ShiftSync.Api.Auth;

public class ProblemExceptionHandler : IExceptionHandler
{
    private readonly IHostEnvironment _environment;
    private readonly ILogger<ProblemExceptionHandler> _logger;

    public ProblemExceptionHandler(IHostEnvironment environment, ILogger<ProblemExceptionHandler> logger)
    {
        _environment = environment;
        _logger = logger;
    }

    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is ConflictException conflict)
        {
            await Results.Problem(
                title: conflict.Title,
                detail: conflict.Message,
                statusCode: conflict.StatusCode,
                extensions: new Dictionary<string, object?>
                {
                    ["conflicts"] = conflict.Conflicts
                        .Select(item => new { kind = item.Kind.ToString(), message = item.Message })
                        .ToList()
                }).ExecuteAsync(httpContext);
            return true;
        }

        if (exception is AppException app)
        {
            await Results.Problem(
                title: app.Title,
                detail: app.Message,
                statusCode: app.StatusCode).ExecuteAsync(httpContext);
            return true;
        }

        _logger.LogError(exception, "Unhandled exception");
        var detail = _environment.IsDevelopment() || _environment.IsEnvironment("Testing")
            ? exception.Message
            : "Something went wrong on the server.";
        await Results.Problem(
            title: "Server error",
            detail: detail,
            statusCode: StatusCodes.Status500InternalServerError).ExecuteAsync(httpContext);
        return true;
    }
}
