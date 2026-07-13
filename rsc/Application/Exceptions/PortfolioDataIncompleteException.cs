namespace Application.Exceptions;

/// <summary>Raised when a seeded portfolio references data required for analysis that is unavailable.</summary>
public sealed class PortfolioDataIncompleteException(string message) : Exception(message);
