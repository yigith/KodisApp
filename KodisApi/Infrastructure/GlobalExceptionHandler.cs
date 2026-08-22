using KodisApi.Exceptions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace KodisApi.Infrastructure
{
    /// <summary>
    /// Turns every unhandled exception into a ProblemDetails response.
    /// Only <see cref="ApiException"/> messages are echoed back; anything else
    /// is logged server-side and reported as a generic 500 so that internal
    /// details never leak to callers.
    /// </summary>
    public sealed class GlobalExceptionHandler : IExceptionHandler
    {
        private readonly ILogger<GlobalExceptionHandler> _logger;
        private readonly IProblemDetailsService _problemDetailsService;

        public GlobalExceptionHandler(
            ILogger<GlobalExceptionHandler> logger,
            IProblemDetailsService problemDetailsService)
        {
            _logger = logger;
            _problemDetailsService = problemDetailsService;
        }

        public async ValueTask<bool> TryHandleAsync(
            HttpContext httpContext,
            Exception exception,
            CancellationToken cancellationToken)
        {
            var problemDetails = exception is ApiException apiException
                ? new ProblemDetails
                {
                    Status = apiException.StatusCode,
                    Title = apiException.Title,
                    Detail = apiException.Message
                }
                : new ProblemDetails
                {
                    Status = StatusCodes.Status500InternalServerError,
                    Title = "Internal Server Error",
                    Detail = "An unexpected error occurred while processing the request."
                };

            var statusCode = problemDetails.Status ?? StatusCodes.Status500InternalServerError;

            if (statusCode >= StatusCodes.Status500InternalServerError)
            {
                _logger.LogError(exception, "Unhandled exception for {Method} {Path}",
                    httpContext.Request.Method, httpContext.Request.Path);
            }
            else
            {
                _logger.LogInformation("Request for {Method} {Path} failed with {Status}: {Message}",
                    httpContext.Request.Method, httpContext.Request.Path,
                    statusCode, exception.Message);
            }

            httpContext.Response.StatusCode = statusCode;

            return await _problemDetailsService.TryWriteAsync(new ProblemDetailsContext
            {
                HttpContext = httpContext,
                Exception = exception,
                ProblemDetails = problemDetails
            });
        }
    }
}
