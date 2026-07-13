namespace Application.Contracts;

/// <summary>Provides the market reference rates needed by analytics calculations.</summary>
public interface IMarketDataReader
{
    /// <summary>Returns the annual Selic rate as a percentage (for example, 12.00).</summary>
    Task<decimal?> GetSelicRateAsync(CancellationToken ct = default);
}
