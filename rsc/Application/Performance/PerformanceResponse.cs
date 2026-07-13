namespace Application.Performance;

public sealed record PortfolioPerformanceResponse(
    decimal TotalInvestment,
    decimal CurrentValue,
    decimal? TotalReturn,
    decimal TotalReturnAmount,
    decimal? AnnualizedReturn,
    decimal? Volatility,
    IReadOnlyList<PositionPerformanceResponse> PositionsPerformance);

public sealed record PositionPerformanceResponse(
    string Symbol,
    decimal InvestedAmount,
    decimal CurrentValue,
    decimal? Return,
    decimal? Weight);

internal sealed record PortfolioPerformanceResult(decimal TotalInvestment, decimal CurrentValue, decimal? TotalReturn, decimal TotalReturnAmount, decimal? AnnualizedReturn, decimal? Volatility, IReadOnlyList<PositionPerformanceResult> PositionsPerformance);
internal sealed record PositionPerformanceResult(string Symbol, decimal InvestedAmount, decimal CurrentValue, decimal? Return, decimal? Weight);
