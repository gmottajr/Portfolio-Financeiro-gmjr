using Models;
using Application.Exceptions;
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

            return new PositionPerformanceResponse(
                position.AssetSymbol.Value,
                investedAmount,
                currentValue,
                positionReturn,
                Weight: null);
        }).ToList();

        var currentValue = positions.Sum(position => position.CurrentValue);
        var totalInvestment = portfolio.TotalInvestment.Value;
        var totalReturnAmount = currentValue - totalInvestment;
        var totalReturn = CalculatePercentageChange(totalInvestment, currentValue);

        var positionsWithWeight = positions
            .Select(position => position with
            {
                Weight = CalculateWeight(currentValue, position.CurrentValue)
            })
            .ToList();

        return new PortfolioPerformanceResponse(
            totalInvestment,
            currentValue,
            totalReturn,
            totalReturnAmount,
            CalculateAnnualizedReturn(totalReturn, portfolio.PortfolioCreatedAt, calculationDate),
            CalculateVolatility(assets.Values),
            positionsWithWeight);
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
        return growthFactor <= 0d
            ? null
            : decimal.Round(((decimal)Math.Pow(growthFactor, 365d / elapsedDays) - 1m) * 100m, 4);
    }

    private static decimal? CalculateVolatility(IEnumerable<Asset> assets)
    {
        var dailyReturns = assets
            .SelectMany(asset => asset.PriceHistory
                .OrderBy(point => point.Date)
                .Zip(asset.PriceHistory.OrderBy(point => point.Date).Skip(1), (previous, current) =>
                    previous.Price.Value == 0m
                        ? (decimal?)null
                        : (current.Price.Value - previous.Price.Value) / previous.Price.Value))
            .Where(value => value.HasValue)
            .Select(value => value!.Value)
            .ToList();

        if (dailyReturns.Count == 0)
        {
            return null;
        }

        var average = dailyReturns.Average();
        var variance = dailyReturns.Average(value => (value - average) * (value - average));
        return decimal.Round((decimal)Math.Sqrt((double)variance) * 100m, 4);
    }
}
