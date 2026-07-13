using System.Globalization;
using Models;

namespace Application.Risk;

public sealed record LargestPositionRiskResult(string Symbol, decimal Percentage);
public sealed record ConcentrationRiskResult(LargestPositionRiskResult? LargestPosition, decimal Top3Concentration);
public sealed record SectorDiversificationResult(string Sector, decimal Percentage, string Risk);
public sealed record RiskAnalysisResult(string OverallRisk, decimal? SharpeRatio, ConcentrationRiskResult ConcentrationRisk, IReadOnlyList<SectorDiversificationResult> SectorDiversification, IReadOnlyList<string> Recommendations);
public sealed record RiskPositionValue(string Symbol, Asset Asset, decimal Value);
public sealed record RiskPositionHistory(decimal Weight, IReadOnlyDictionary<DateTime, decimal> DailyReturns);

/// <summary>Pure calculation and policy logic for a portfolio risk analysis.</summary>
public sealed class PortfolioRiskCalculator
{
    public RiskAnalysisResult Calculate(
        IReadOnlyList<RiskPositionValue> positions,
        decimal totalInvestment,
        DateTime? portfolioCreatedAt,
        decimal? selicRate)
    {
        var totalValue = positions.Sum(position => position.Value);
        var concentration = CalculateConcentration(positions, totalValue);
        var sectors = CalculateSectors(positions, totalValue);
        var calculationDate = positions.Count == 0 ? (DateTime?)null : positions.Max(position => position.Asset.LastUpdated);
        var annualizedReturn = CalculateAnnualizedReturn(
            CalculatePortfolioReturn(totalInvestment, totalValue),
            portfolioCreatedAt,
            calculationDate);
        var sharpeRatio = CalculateSharpeRatio(annualizedReturn, selicRate, CalculateAnnualizedVolatility(positions, totalValue));
        return new RiskAnalysisResult(CalculateOverallRisk(concentration.LargestPosition?.Percentage ?? 0m, sectors), sharpeRatio, concentration, sectors, BuildRecommendations(concentration, sectors));
    }

    // Position concentration (%) = position market value / portfolio market value × 100.
    // Top-3 concentration is the sum of the three largest position percentages.
    private static ConcentrationRiskResult CalculateConcentration(IReadOnlyList<RiskPositionValue> positions, decimal totalValue)
    {
        var ordered = positions.OrderByDescending(position => position.Value).ToList();
        var largest = ordered.FirstOrDefault();
        var largestPosition = largest is null ? null : new LargestPositionRiskResult(largest.Symbol, CalculatePercentage(largest.Value, totalValue));
        return new ConcentrationRiskResult(largestPosition, decimal.Round(ordered.Take(3).Sum(position => CalculatePercentage(position.Value, totalValue)), 4));
    }

    // Sector concentration uses the same formula, grouping position values by sector.
    private static IReadOnlyList<SectorDiversificationResult> CalculateSectors(IReadOnlyList<RiskPositionValue> positions, decimal totalValue) => positions.GroupBy(position => position.Asset.Sector).Select(group => { var percentage = CalculatePercentage(group.Sum(position => position.Value), totalValue); return new SectorDiversificationResult(group.Key, percentage, CalculateSectorRisk(percentage)); }).OrderByDescending(sector => sector.Percentage).ThenBy(sector => sector.Sector).ToList();
    // Sharpe ratio = (annualized portfolio return - annual Selic rate) / annualized volatility.
    private static decimal? CalculateSharpeRatio(decimal? portfolioReturn, decimal? selicRate, decimal? volatility) => portfolioReturn is null || selicRate is null || volatility is null || volatility == 0m ? null : decimal.Round((portfolioReturn.Value - selicRate.Value) / volatility.Value, 4);
    // Portfolio return (%) = (current market value - invested amount) / invested amount × 100.
    private static decimal? CalculatePortfolioReturn(decimal investedAmount, decimal currentValue) => investedAmount == 0m ? null : decimal.Round((currentValue - investedAmount) / investedAmount * 100m, 4);
    // Annualized return (%) = ((1 + total return)^(365 / elapsed days) - 1) × 100.
    private static decimal? CalculateAnnualizedReturn(decimal? totalReturn, DateTime? createdAt, DateTime? calculationDate) { if (totalReturn is null || createdAt is null || calculationDate is null) return null; var elapsedDays = (calculationDate.Value.Date - createdAt.Value.Date).TotalDays; var growthFactor = 1d + (double)(totalReturn.Value / 100m); if (elapsedDays <= 0d || growthFactor <= 0d) return null; var value = (Math.Pow(growthFactor, 365d / elapsedDays) - 1d) * 100d; return double.IsNaN(value) || double.IsInfinity(value) || value > (double)decimal.MaxValue || value < (double)decimal.MinValue ? null : decimal.Round((decimal)value, 4); }
    private static decimal? CalculateAnnualizedVolatility(IReadOnlyList<RiskPositionValue> positions, decimal totalValue)
    {
        if (totalValue <= 0m || positions.Count == 0) return null;
        // r[i,t] = (close[i,t] - close[i,t-1]) / close[i,t-1]; portfolio
        // r[p,t] = Σ(weight[i] × r[i,t]); annual volatility = σ[daily] × √252 × 100.
        var histories = positions.Select(position => { var points = position.Asset.PriceHistory.GroupBy(point => point.Date.Date).Select(group => group.OrderBy(point => point.Date).Last()).OrderBy(point => point.Date).ToList(); if (points.Count < 2) return null; var dailyReturns = points.Zip(points.Skip(1), (previous, current) => new { current.Date, Return = previous.Price.Value == 0m ? (decimal?)null : (current.Price.Value - previous.Price.Value) / previous.Price.Value }).Where(item => item.Return.HasValue).ToDictionary(item => item.Date.Date, item => item.Return!.Value); return dailyReturns.Count == 0 ? null : new RiskPositionHistory(position.Value / totalValue, dailyReturns); }).ToList();
        if (histories.Any(history => history is null)) return null;
        var complete = histories.Select(history => history!).ToList(); var dates = complete.Select(history => history.DailyReturns.Keys).Aggregate((dates, next) => dates.Intersect(next).ToList()); if (!dates.Any()) return null;
        var returns = dates.Select(date => complete.Sum(history => history.Weight * history.DailyReturns[date])).ToList(); var average = returns.Average(); var variance = returns.Average(value => (value - average) * (value - average)); return decimal.Round((decimal)Math.Sqrt((double)variance) * (decimal)Math.Sqrt(252d) * 100m, 4);
    }
    private static string CalculateOverallRisk(decimal largestPosition, IReadOnlyList<SectorDiversificationResult> sectors) { var largestSector = sectors.Count == 0 ? 0m : sectors.Max(sector => sector.Percentage); return largestPosition > 25m || largestSector > 40m ? "High" : largestPosition >= 15m || largestSector >= 25m ? "Medium" : "Low"; }
    private static string CalculateSectorRisk(decimal percentage) => percentage > 40m ? "High" : percentage >= 25m ? "Medium" : "Low";
    private static IReadOnlyList<string> BuildRecommendations(ConcentrationRiskResult concentration, IReadOnlyList<SectorDiversificationResult> sectors) { var recommendations = new List<string>(); foreach (var sector in sectors.Where(sector => sector.Percentage >= 25m)) { var verb = sector.Percentage > 40m ? "Reduzir" : "Monitorar"; recommendations.Add($"{verb} exposição ao setor {sector.Sector} ({FormatPercentage(sector.Percentage)}%)."); } if (concentration.LargestPosition is { Percentage: >= 15m } largest) { var verb = largest.Percentage > 25m ? "Reduzir" : "Monitorar"; recommendations.Add($"{verb} concentração na posição {largest.Symbol} ({FormatPercentage(largest.Percentage)}% do portfólio; ideal < 20%)."); } if (concentration.Top3Concentration > 60m) recommendations.Add($"Diversificar o portfólio: as três maiores posições representam {FormatPercentage(concentration.Top3Concentration)}%."); return recommendations; }
    private static decimal CalculatePercentage(decimal value, decimal total) => total == 0m ? 0m : decimal.Round(value / total * 100m, 4);
    private static string FormatPercentage(decimal value) => value.ToString("F1", CultureInfo.InvariantCulture);
}
