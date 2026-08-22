namespace KodisApi.Exceptions
{
    /// <summary>
    /// Base for failures that map onto a specific HTTP status code. Anything
    /// else that escapes a controller is treated as a 500 and its detail is
    /// never sent to the client.
    /// </summary>
    public abstract class ApiException : Exception
    {
        protected ApiException(string message, Exception? innerException = null)
            : base(message, innerException)
        {
        }

        public abstract int StatusCode { get; }

        public abstract string Title { get; }
    }

    public sealed class NotFoundException : ApiException
    {
        public NotFoundException(string message = "Resource not found.") : base(message) { }

        public override int StatusCode => StatusCodes.Status404NotFound;

        public override string Title => "Not Found";
    }

    public sealed class UnauthorizedException : ApiException
    {
        public UnauthorizedException(string message = "Authentication is required.") : base(message) { }

        public override int StatusCode => StatusCodes.Status401Unauthorized;

        public override string Title => "Unauthorized";
    }

    public sealed class ForbiddenException : ApiException
    {
        public ForbiddenException(string message = "You are not allowed to do that.") : base(message) { }

        public override int StatusCode => StatusCodes.Status403Forbidden;

        public override string Title => "Forbidden";
    }

    public sealed class ConflictException : ApiException
    {
        public ConflictException(string message) : base(message) { }

        public override int StatusCode => StatusCodes.Status409Conflict;

        public override string Title => "Conflict";
    }

    public sealed class BadRequestException : ApiException
    {
        public BadRequestException(string message) : base(message) { }

        public override int StatusCode => StatusCodes.Status400BadRequest;

        public override string Title => "Bad Request";
    }
}
