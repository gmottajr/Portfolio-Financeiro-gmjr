using Application.Contracts;
using Application.Exceptions;
using Application.Mappings;
using Microsoft.Extensions.Logging;

namespace Application.Rebalancing;

public sealed record CurrentAllocation(string Symbol, decimal CurrentWeight, decimal TargetWeight, decimal Deviation);
public sealed record SuggestedTrade(string Symbol, string Action, decimal Quantity, decimal EstimatedValue, decimal TransactionCost, string Reason);
public sealed record RebalancingResponse(bool NeedsRebalancing, IReadOnlyList<CurrentAllocation> CurrentAllocation, IReadOnlyList<SuggestedTrade> SuggestedTrades, decimal TotalTransactionCost, string ExpectedImprovement);

public sealed record CurrentAllocationResult(string Symbol, decimal CurrentWeight, decimal TargetWeight, decimal Deviation);
public sealed record SuggestedTradeResult(string Symbol, string Action, decimal Quantity, decimal EstimatedValue, decimal TransactionCost, string Reason);
public sealed record RebalancingResult(bool NeedsRebalancing, IReadOnlyList<CurrentAllocationResult> CurrentAllocation, IReadOnlyList<SuggestedTradeResult> SuggestedTrades, decimal TotalTransactionCost, string ExpectedImprovement);

/// <summary>Loads portfolio data and delegates the financial plan to the optimizer.</summary>
public sealed class GenerateRebalancingSuggestionsUseCase(
    IPortfolioPositionsReader portfolios,
    IAssetReader assets,
    IRebalancingOptimizer optimizer,
    ILogger<GenerateRebalancingSuggestionsUseCase> logger) : IGenerateRebalancingSuggestionsUseCase
{
    public async Task<RebalancingResponse?> ExecuteAsync(int portfolioId, CancellationToken ct = default)
    {
        logger.LogInformation("Generating rebalancing suggestions for portfolio {PortfolioId}.", portfolioId);

        try
        {
            var portfolio = await portfolios.GetWithPositionsAsync(portfolioId, ct);
            if (portfolio is null)
            {
                logger.LogWarning("Portfolio {PortfolioId} was not found for rebalancing.", portfolioId);
                return null;
            }

            var positions = await LoadPositionsAsync(portfolio, ct);
            var result = optimizer.Optimize(positions);
            logger.LogInformation(
                "Generated {TradeCount} self-financed rebalancing suggestions for portfolio {PortfolioId}. Total transaction cost: {TotalTransactionCost}.",
                result.SuggestedTrades.Count, portfolioId, result.TotalTransactionCost);

            return AnalyticsResponseMapper.ToResponse(result);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Rebalancing suggestion generation failed for portfolio {PortfolioId}.", portfolioId);
            throw;
        }
    }

    private async Task<IReadOnlyList<RebalancingPosition>> LoadPositionsAsync(Models.Portfolio portfolio, CancellationToken ct)
    {
        var result = new List<RebalancingPosition>();
        foreach (var position in portfolio.Positions)
        {
            var asset = await assets.GetByIdAsync(position.AssetSymbol, ct)
                ?? throw new PortfolioDataIncompleteException($"Asset {position.AssetSymbol} was not found.");
            result.Add(new RebalancingPosition(position.AssetSymbol.Value, position.CurrentValue(asset.CurrentPrice).Value, asset.CurrentPrice.Value, position.TargetAllocation.Value));
        }

        return result;
    }
}
