namespace Beacon.Api.Common;

/// <summary>
/// A broken business rule. Deliberately tiny — not a server fault.
/// Thrown from the domain; mapped once at the edge by <see cref="GlobalExceptionHandler"/>.
/// </summary>
public sealed class DomainException : Exception
{
    public DomainException(string message) : base(message) { }
    public DomainException(string message, Exception inner) : base(message, inner) { }
}

/// <summary>An entity the caller asked for does not exist. Handlers translate this to 404.</summary>
public sealed class NotFoundException : Exception
{
    public NotFoundException(string message) : base(message) { }
}

/// <summary>The caller is authenticated but lacks the required role. Handlers translate this to 403.</summary>
public sealed class ForbiddenException : Exception
{
    public ForbiddenException(string message) : base(message) { }
}
