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
        // Performance is calculated from the holdings that compose the endpoint
        // response. The seed's aggregate TotalInvestment can include money that
        // is not represented by a Position; there is no cash position in this
        // model, so using it here would make the portfolio result irreconcilable
        // with its positions.
        var totalInvestment = positions.Sum(position => position.InvestedAmount);
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

    private static decimal? CalculatePercentageChange(decimal baseValue, decimal currentValue) =>
        baseValue == 0m ? null : decimal.Round((currentValue - baseValue) / baseValue * 100m, 4);

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
                var points = asset.PriceHistory.OrderBy(point => point.Date).ToList();
                if (points.Count < 2)
                {
                    return null;
                }

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

        var portfolioDailyReturns = commonDates
            .Select(date => histories.Sum(history => history.Weight * history.DailyReturns[date]))
            .ToList();
        var average = portfolioDailyReturns.Average();
        var variance = portfolioDailyReturns.Average(value => (value - average) * (value - average));
        return decimal.Round((decimal)Math.Sqrt((double)variance) * 100m, 4);
    }

    private sealed record PositionHistory(decimal Weight, IReadOnlyDictionary<DateTime, decimal> DailyReturns);
}
