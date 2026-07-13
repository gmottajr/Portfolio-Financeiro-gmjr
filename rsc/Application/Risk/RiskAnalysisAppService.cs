using Application.Contracts;
using Application.Exceptions;
using Application.Mappings;
using Microsoft.Extensions.Logging;
using Models;

namespace Application.Risk;

public sealed record LargestPositionRisk(string Symbol, decimal Percentage);
public sealed record ConcentrationRiskResponse(LargestPositionRisk? LargestPosition, decimal Top3Concentration);
public sealed record SectorDiversificationResponse(string Sector, decimal Percentage, string Risk);
public sealed record RiskAnalysisResponse(string OverallRisk, decimal? SharpeRatio, ConcentrationRiskResponse ConcentrationRisk, IReadOnlyList<SectorDiversificationResponse> SectorDiversification, IReadOnlyList<string> Recommendations);

public sealed class RiskAnalysisAppService(
    IPortfolioPositionsReader portfolios,
    IAssetPriceHistoryReader assets,
    IMarketDataReader marketData,
    PortfolioRiskCalculator calculator,
    ILogger<RiskAnalysisAppService> logger) : IRiskAnalysisAppService
{
    public async Task<RiskAnalysisResponse?> AnalyzeAsync(int portfolioId, CancellationToken ct = default)
    {
        using var scope = logger.BeginScope(new Dictionary<string, object>
        {
            ["Operation"] = "AnalyzePortfolioRisk",
            ["PortfolioId"] = portfolioId
        });
        logger.LogInformation("Starting risk analysis for portfolio {PortfolioId}.", portfolioId);
        try
        {
            var portfolio = await portfolios.GetWithPositionsAsync(portfolioId, ct);
            if (portfolio is null)
            {
                logger.LogWarning("Portfolio {PortfolioId} was not found for risk analysis.", portfolioId);
                return null;
            }

            logger.LogDebug("Risk input portfolio loaded with {PositionCount} positions.", portfolio.Positions.Count);
            var positions = await LoadPositionValuesAsync(portfolio, ct);
            var selicRate = await marketData.GetSelicRateAsync(ct);
            logger.LogDebug("Risk calculation inputs loaded. PositionCount: {PositionCount}; SelicRate: {SelicRate}; PortfolioCreatedAt: {PortfolioCreatedAt}.", positions.Count, selicRate, portfolio.PortfolioCreatedAt);
            var result = calculator.Calculate(
                positions,
                portfolio.PortfolioCreatedAt,
                selicRate);

            logger.LogDebug(
                "Risk calculation result. OverallRisk: {OverallRisk}; SharpeRatio: {SharpeRatio}; LargestPosition: {LargestPosition}; Top3Concentration: {Top3Concentration}; SectorCount: {SectorCount}; RecommendationCount: {RecommendationCount}.",
                result.OverallRisk,
                result.SharpeRatio,
                result.ConcentrationRisk.LargestPosition?.Symbol,
                result.ConcentrationRisk.Top3Concentration,
                result.SectorDiversification.Count,
                result.Recommendations.Count);

            logger.LogInformation("Risk analysis completed for portfolio {PortfolioId}. Risk: {Risk}; SharpeRatio: {SharpeRatio}.", portfolioId, result.OverallRisk, result.SharpeRatio);
            return AnalyticsResponseMapper.ToResponse(result);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Risk analysis failed for portfolio {PortfolioId}.", portfolioId);
            throw;
        }
    }

    private async Task<IReadOnlyList<RiskPositionValue>> LoadPositionValuesAsync(Portfolio portfolio, CancellationToken ct)
    {
        var positions = new List<RiskPositionValue>();
        foreach (var position in portfolio.Positions)
        {
            var asset = await assets.GetWithPriceHistoryAsync(position.AssetSymbol, ct)
                ?? throw new PortfolioDataIncompleteException($"Asset {position.AssetSymbol} was not found.");
            var currentValue = position.CurrentValue(asset.CurrentPrice).Value;
            logger.LogDebug(
                "Risk position loaded. AssetSymbol: {AssetSymbol}; InvestedAmount: {InvestedAmount}; CurrentValue: {CurrentValue}; PriceHistoryCount: {PriceHistoryCount}.",
                position.AssetSymbol.Value,
                position.InvestedAmount.Value,
                currentValue,
                asset.PriceHistory.Count);
            positions.Add(new RiskPositionValue(position.AssetSymbol.Value, asset, position.InvestedAmount.Value, currentValue));
        }
        return positions;
    }
}
