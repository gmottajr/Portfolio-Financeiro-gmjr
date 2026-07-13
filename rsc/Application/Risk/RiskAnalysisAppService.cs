using Application.Contracts;
using Application.Exceptions;
using Application.Mappings;
using System.Globalization;
using Microsoft.Extensions.Logging;
using Models;

namespace Application.Risk;

public sealed record LargestPositionRisk(string Symbol, decimal Percentage);

public sealed record ConcentrationRiskResponse(
    LargestPositionRisk? LargestPosition,
    decimal Top3Concentration);

public sealed record SectorDiversificationResponse(string Sector, decimal Percentage, string Risk);

public sealed record RiskAnalysisResponse(
    string OverallRisk,
    decimal? SharpeRatio,
    ConcentrationRiskResponse ConcentrationRisk,
    IReadOnlyList<SectorDiversificationResponse> SectorDiversification,
    IReadOnlyList<string> Recommendations);

internal sealed record LargestPositionRiskResult(string Symbol, decimal Percentage);
internal sealed record ConcentrationRiskResult(LargestPositionRiskResult? LargestPosition, decimal Top3Concentration);
internal sealed record SectorDiversificationResult(string Sector, decimal Percentage, string Risk);
internal sealed record RiskAnalysisResult(string OverallRisk, decimal? SharpeRatio, ConcentrationRiskResult ConcentrationRisk, IReadOnlyList<SectorDiversificationResult> SectorDiversification, IReadOnlyList<string> Recommendations);

public sealed class RiskAnalysisAppService(
    IPortfolioRepository portfolios,
    IAssetRepository assets,
    IMarketDataReader marketData,
    ILogger<RiskAnalysisAppService> logger)
{
    public async Task<RiskAnalysisResponse?> AnalyzeAsync(int portfolioId, CancellationToken ct = default)
    {
        logger.LogInformation("Starting risk analysis for portfolio {PortfolioId}.", portfolioId);

        try
        {
            var portfolio = await portfolios.GetWithPositionsAsync(portfolioId, ct);
            if (portfolio is null)
            {
                logger.LogWarning("Portfolio {PortfolioId} was not found for risk analysis.", portfolioId);
                return null;
            }

            var positions = await LoadPositionValuesAsync(portfolio, ct);
            var totalValue = positions.Sum(position => position.Value);
            var concentration = CalculateConcentration(positions, totalValue);
            var sectors = CalculateSectors(positions, totalValue);
            var overallRisk = CalculateOverallRisk(concentration.LargestPosition?.Percentage ?? 0m, sectors);
            var calculationDate = positions.Count == 0
                ? (DateTime?)null
                : positions.Max(position => position.Asset.LastUpdated);
            var sharpeRatio = CalculateSharpeRatio(
                CalculateAnnualizedReturn(
                    CalculatePortfolioReturn(positions.Sum(position => position.InvestedAmount), totalValue),
                    portfolio.PortfolioCreatedAt,
                    calculationDate),
                await marketData.GetSelicRateAsync(ct),
                CalculateAnnualizedVolatility(positions, totalValue));
            var recommendations = BuildRecommendations(concentration, sectors);

            logger.LogInformation(
                "Risk analysis completed for portfolio {PortfolioId}. Risk: {Risk}; SharpeRatio: {SharpeRatio}.",
                portfolioId,
                overallRisk,
                sharpeRatio);

            return AnalyticsResponseMapper.ToResponse(
                new RiskAnalysisResult(overallRisk, sharpeRatio, concentration, sectors, recommendations));
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Risk analysis failed for portfolio {PortfolioId}.", portfolioId);
            throw;
        }
    }

    private async Task<IReadOnlyList<PositionValue>> LoadPositionValuesAsync(Portfolio portfolio, CancellationToken ct)
    {
        var positions = new List<PositionValue>();
        foreach (var position in portfolio.Positions)
        {
            var asset = await assets.GetWithPriceHistoryAsync(position.AssetSymbol, ct)
                ?? throw new PortfolioDataIncompleteException($"Asset {position.AssetSymbol} was not found.");
            positions.Add(new PositionValue(
                position.AssetSymbol.Value,
                asset,
                position.InvestedAmount.Value,
                position.CurrentValue(asset.CurrentPrice).Value));
        }

        return positions;
    }

    private static ConcentrationRiskResult CalculateConcentration(IReadOnlyList<PositionValue> positions, decimal totalValue)
    {
        var ordered = positions.OrderByDescending(position => position.Value).ToList();
        var largest = ordered.FirstOrDefault();
        var largestPosition = largest is null
            ? null
            : new LargestPositionRiskResult(largest.Symbol, CalculatePercentage(largest.Value, totalValue));
        var top3 = ordered.Take(3).Sum(position => CalculatePercentage(position.Value, totalValue));

        return new ConcentrationRiskResult(largestPosition, decimal.Round(top3, 4));
    }

    private static IReadOnlyList<SectorDiversificationResult> CalculateSectors(
        IReadOnlyList<PositionValue> positions,
        decimal totalValue) =>
        positions
            .GroupBy(position => position.Asset.Sector)
            .Select(group =>
            {
                var percentage = CalculatePercentage(group.Sum(position => position.Value), totalValue);
                return new SectorDiversificationResult(group.Key, percentage, CalculateSectorRisk(percentage));
            })
            .OrderByDescending(sector => sector.Percentage)
            .ThenBy(sector => sector.Sector)
            .ToList();

    private static decimal? CalculateSharpeRatio(decimal? portfolioReturn, decimal? selicRate, decimal? volatility) =>
        portfolioReturn is null || selicRate is null || volatility is null || volatility == 0m
            ? null
            : decimal.Round((portfolioReturn.Value - selicRate.Value) / volatility.Value, 4);

    private static decimal? CalculatePortfolioReturn(decimal investedAmount, decimal currentValue) =>
        investedAmount == 0m
            ? null
            : decimal.Round((currentValue - investedAmount) / investedAmount * 100m, 4);

    private static decimal? CalculateAnnualizedReturn(
        decimal? totalReturn,
        DateTime? portfolioCreatedAt,
        DateTime? calculationDate)
    {
        if (totalReturn is null || portfolioCreatedAt is null || calculationDate is null)
        {
            return null;
        }

        var elapsedDays = (calculationDate.Value.Date - portfolioCreatedAt.Value.Date).TotalDays;
        var growthFactor = 1d + (double)(totalReturn.Value / 100m);
        if (elapsedDays <= 0d || growthFactor <= 0d)
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

    private static decimal? CalculateAnnualizedVolatility(IReadOnlyList<PositionValue> positions, decimal totalValue)
    {
        if (totalValue <= 0m || positions.Count == 0)
        {
            return null;
        }

        var histories = positions.Select(position =>
        {
            var points = position.Asset.PriceHistory.OrderBy(point => point.Date).ToList();
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
                : new PositionHistory(position.Value / totalValue, dailyReturns);
        }).ToList();

        if (histories.Any(history => history is null))
        {
            return null;
        }

        var completeHistories = histories.Select(history => history!).ToList();
        var commonDates = completeHistories
            .Select(history => history.DailyReturns.Keys)
            .Aggregate((dates, next) => dates.Intersect(next).ToList());
        if (!commonDates.Any())
        {
            return null;
        }

        var portfolioDailyReturns = commonDates
            .Select(date => completeHistories.Sum(history => history.Weight * history.DailyReturns[date]))
            .ToList();
        var average = portfolioDailyReturns.Average();
        var variance = portfolioDailyReturns.Average(value => (value - average) * (value - average));
        return decimal.Round((decimal)Math.Sqrt((double)variance) * (decimal)Math.Sqrt(252d) * 100m, 4);
    }

    private static string CalculateOverallRisk(decimal largestPosition, IReadOnlyList<SectorDiversificationResult> sectors)
    {
        var largestSector = sectors.Count == 0 ? 0m : sectors.Max(sector => sector.Percentage);
        return largestPosition > 25m || largestSector > 40m
            ? "High"
            : largestPosition >= 15m || largestSector >= 25m
                ? "Medium"
                : "Low";
    }

    private static string CalculateSectorRisk(decimal percentage) => percentage > 40m ? "High" : percentage >= 25m ? "Medium" : "Low";

    private static IReadOnlyList<string> BuildRecommendations(
        ConcentrationRiskResult concentration,
        IReadOnlyList<SectorDiversificationResult> sectors)
    {
        var recommendations = new List<string>();
        foreach (var sector in sectors.Where(sector => sector.Percentage >= 25m))
        {
            var verb = sector.Percentage > 40m ? "Reduzir" : "Monitorar";
            recommendations.Add($"{verb} exposição ao setor {sector.Sector} ({FormatPercentage(sector.Percentage)}%).");
        }

        if (concentration.LargestPosition is { Percentage: >= 15m } largest)
        {
            var verb = largest.Percentage > 25m ? "Reduzir" : "Monitorar";
            recommendations.Add($"{verb} concentração na posição {largest.Symbol} ({FormatPercentage(largest.Percentage)}% do portfólio; ideal < 20%).");
        }

        if (concentration.Top3Concentration > 60m)
        {
            recommendations.Add($"Diversificar o portfólio: as três maiores posições representam {FormatPercentage(concentration.Top3Concentration)}%.");
        }

        return recommendations;
    }

    private static decimal CalculatePercentage(decimal value, decimal total) =>
        total == 0m ? 0m : decimal.Round(value / total * 100m, 4);

    private static string FormatPercentage(decimal value) => value.ToString("F1", CultureInfo.InvariantCulture);

    private sealed record PositionValue(string Symbol, Asset Asset, decimal InvestedAmount, decimal Value);
    private sealed record PositionHistory(decimal Weight, IReadOnlyDictionary<DateTime, decimal> DailyReturns);
}
