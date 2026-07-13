namespace SharedKernel.Exceptions;

/// <summary>
/// Represents a violated business rule. It derives from DomainException so
/// existing domain-level error handling remains compatible.
/// </summary>
public sealed class BusinessViolationException : DomainException
{
    public BusinessViolationException(string message) : base(message)
    {
    }

    public BusinessViolationException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
