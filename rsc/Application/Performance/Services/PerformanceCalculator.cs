using Models;
using Application.Exceptions;
using Application.Mappings;
using SharedKernel.ValueObjects;

namespace Application.Performance.Services;

/// <summary>Calculates portfolio performance without accessing infrastructure.</summary>
public sealed class PerformanceCalculator : IPerformanceCalculator
{
    public PortfolioPerformanceResponse Calculate(
        Portfolio portfolio,
        IReadOnlyDictionary<AssetSymbol, Asset> assets,
        DateTime calculationDate)
    {
        ArgumentNullException.ThrowIfNull(portfolio);
        ArgumentNullException.ThrowIfNull(assets);

        var positions = portfolio.Positions.Select(position =>
        {
            if (!assets.TryGetValue(position.AssetSymbol, out var asset))
            {
                throw new PortfolioDataIncompleteException(
                    $"Asset '{position.AssetSymbol}' was not found for portfolio {portfolio.Id}.");
            }

            var investedAmount = position.InvestedAmount.Value;
            var currentValue = position.CurrentValue(asset.CurrentPrice).Value;
            var positionReturn = CalculatePercentageChange(investedAmount, currentValue);

            return new PositionPerformanceResult(
                position.AssetSymbol.Value,
                investedAmount,
                currentValue,
                positionReturn,
                Weight: null);
        }).ToList();

        var currentValue = positions.Sum(position => position.CurrentValue);
        // TotalInvestment is the portfolio's initial capital, as defined by the
        // API contract. It can include cash or investments not represented by a
        // position, so it must not be reconstructed from the listed positions.
        var totalInvestment = portfolio.TotalInvestment.Value;
        var totalReturnAmount = currentValue - totalInvestment;
        var totalReturn = CalculatePercentageChange(totalInvestment, currentValue);

        var positionsWithWeight = positions
            .Select(position => position with
            {
                Weight = CalculateWeight(currentValue, position.CurrentValue)
            })
            .ToList();

        return AnalyticsResponseMapper.ToResponse(new PortfolioPerformanceResult(
            totalInvestment,
            currentValue,
            totalReturn,
            totalReturnAmount,
            CalculateAnnualizedReturn(totalReturn, portfolio.PortfolioCreatedAt, calculationDate),
            CalculateVolatility(portfolio, assets, currentValue),
            positionsWithWeight));
    }

    // Return (%) = ((current value - base value) / base value) × 100.
    // A zero base has no defined percentage return, so the API returns null.
    private static decimal? CalculatePercentageChange(decimal baseValue, decimal currentValue) =>
        baseValue == 0m ? null : decimal.Round((currentValue - baseValue) / baseValue * 100m, 4);

    // Weight (%) = position market value / total portfolio market value × 100.
    private static decimal? CalculateWeight(decimal totalValue, decimal positionValue) =>
        totalValue == 0m ? null : decimal.Round(positionValue / totalValue * 100m, 4);

    private static decimal? CalculateAnnualizedReturn(
        decimal? totalReturn,
        DateTime? portfolioCreatedAt,
        DateTime calculationDate)
    {
        if (totalReturn is null || portfolioCreatedAt is null)
        {
            return null;
        }

        var elapsedDays = (calculationDate.Date - portfolioCreatedAt.Value.Date).TotalDays;
        if (elapsedDays <= 0d)
        {
            return null;
        }

        var growthFactor = 1d + (double)(totalReturn.Value / 100m);
        if (growthFactor <= 0d)
        {
            return null;
        }

        // Annualized return (%) = ((1 + total return)^(365 / elapsed days) - 1) × 100.
        var annualizedReturn = (Math.Pow(growthFactor, 365d / elapsedDays) - 1d) * 100d;
        return double.IsNaN(annualizedReturn)
               || double.IsInfinity(annualizedReturn)
               || annualizedReturn > (double)decimal.MaxValue
               || annualizedReturn < (double)decimal.MinValue
            ? null
            : decimal.Round((decimal)annualizedReturn, 4);
    }

    private static decimal? CalculateVolatility(
        Portfolio portfolio,
        IReadOnlyDictionary<AssetSymbol, Asset> assets,
        decimal totalCurrentValue)
    {
        if (totalCurrentValue <= 0m)
        {
            return null;
        }

        var positionHistories = portfolio.Positions
            .Select(position =>
            {
                var asset = assets[position.AssetSymbol];
                // EF Core can materialize legacy data without going through
                // Asset.SetPriceHistory, so protect the calculation as well.
                var points = asset.PriceHistory
                    .GroupBy(point => point.Date.Date)
                    .Select(group => group.OrderBy(point => point.Date).Last())
                    .OrderBy(point => point.Date)
                    .ToList();
                if (points.Count < 2)
                {
                    return null;
                }

                // Daily return r[t] = (close[t] - close[t-1]) / close[t-1].
                var dailyReturns = points
                    .Zip(points.Skip(1), (previous, current) => new
                    {
                        current.Date,
                        Return = previous.Price.Value == 0m
                            ? (decimal?)null
                            : (current.Price.Value - previous.Price.Value) / previous.Price.Value
                    })
                    .Where(item => item.Return.HasValue)
                    .ToDictionary(item => item.Date.Date, item => item.Return!.Value);

                return dailyReturns.Count == 0
                    ? null
                    : new PositionHistory(
                        position.CurrentValue(asset.CurrentPrice).Value / totalCurrentValue,
                        dailyReturns);
            })
            .ToList();

        // The README requires null when price history is unavailable. A partial
        // history would otherwise turn this into the volatility of only a subset
        // of the portfolio.
        if (positionHistories.Any(history => history is null))
        {
            return null;
        }

        var histories = positionHistories.Select(history => history!).ToList();
        var commonDates = histories
            .Select(history => history.DailyReturns.Keys)
            .Aggregate((dates, next) => dates.Intersect(next).ToList());
        if (!commonDates.Any())
        {
            return null;
        }

        // Portfolio daily return r[p,t] = Σ(weight[i] × r[i,t]).
        var portfolioDailyReturns = commonDates
            .Select(date => histories.Sum(history => history.Weight * history.DailyReturns[date]))
            .ToList();
        var average = portfolioDailyReturns.Average();
        // Daily volatility (%) = √(mean((r[p,t] - mean(r[p]))²)) × 100.
        // The challenge requests daily volatility, so this result is not annualized.
        var variance = portfolioDailyReturns.Average(value => (value - average) * (value - average));
        return decimal.Round((decimal)Math.Sqrt((double)variance) * 100m, 4);
    }

    private sealed record PositionHistory(decimal Weight, IReadOnlyDictionary<DateTime, decimal> DailyReturns);
}
