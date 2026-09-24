namespace BuildingBlocks.ServiceDefaults.Errors;

/// <summary>Base type for expected failures that map to a specific client-facing status.</summary>
public abstract class AppException(string message) : Exception(message)
{
    public abstract int StatusCode { get; }

    public abstract string Title { get; }
}

public sealed class ValidationException(string message) : AppException(message)
{
    public override int StatusCode => 400;

    public override string Title => "Validation failed";
}

public sealed class NotFoundException(string message) : AppException(message)
{
    public override int StatusCode => 404;

    public override string Title => "Not found";
}

public sealed class ConflictException(string message) : AppException(message)
{
    public override int StatusCode => 409;

    public override string Title => "Conflict";
}

public sealed class DependencyUnavailableException(string message, Exception? inner = null) : AppException(message)
{
    public Exception? Cause { get; } = inner;

    public override int StatusCode => 503;

    public override string Title => "Service unavailable";
}

/// <summary>A message-processing failure that retrying cannot fix; the message goes straight to the error queue.</summary>
public sealed class NonRetryableException(string message, Exception? inner = null) : Exception(message, inner);
