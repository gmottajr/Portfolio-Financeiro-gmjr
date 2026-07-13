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
    IPortfolioRepository portfolios,
    IAssetRepository assets,
    IMarketDataReader marketData,
    PortfolioRiskCalculator calculator,
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
            var result = calculator.Calculate(
                positions,
                portfolio.PortfolioCreatedAt,
                await marketData.GetSelicRateAsync(ct));

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
            positions.Add(new RiskPositionValue(position.AssetSymbol.Value, asset, position.InvestedAmount.Value, position.CurrentValue(asset.CurrentPrice).Value));
        }
        return positions;
    }
}
