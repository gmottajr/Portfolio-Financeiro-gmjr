using Application.Contracts;
using Application.Exceptions;
using Application.Mappings;
using Microsoft.Extensions.Logging;

namespace Application.Rebalancing;

/// <summary>Loads portfolio data and delegates the financial plan to the optimizer.</summary>
public sealed class GenerateRebalancingSuggestionsUseCase(
    IPortfolioPositionsReader portfolios,
    IAssetReader assets,
    IRebalancingOptimizer optimizer,
    ILogger<GenerateRebalancingSuggestionsUseCase> logger) : IGenerateRebalancingSuggestionsUseCase
{
    public async Task<RebalancingResponse?> ExecuteAsync(
        int portfolioId,
        RebalancingOptimizationMode mode = RebalancingOptimizationMode.CompareAll,
        CancellationToken ct = default)
    {
        using var scope = logger.BeginScope(new Dictionary<string, object>
        {
            ["Operation"] = "GenerateRebalancingSuggestions",
            ["PortfolioId"] = portfolioId
        });
        logger.LogInformation("Generating rebalancing suggestions for portfolio {PortfolioId}.", portfolioId);

        try
        {
            var portfolio = await portfolios.GetWithPositionsAsync(portfolioId, ct);
            if (portfolio is null)
            {
                logger.LogWarning("Portfolio {PortfolioId} was not found for rebalancing.", portfolioId);
                return null;
            }

            logger.LogDebug("Rebalancing input portfolio loaded with {PositionCount} positions.", portfolio.Positions.Count);
            var positions = await LoadPositionsAsync(portfolio, ct);
            logger.LogDebug("Rebalancing optimizer invoked. CurrentPortfolioValue: {CurrentPortfolioValue}; PositionCount: {PositionCount}.", positions.Sum(position => position.CurrentValue), positions.Count);
            var result = optimizer.Optimize(positions, mode);
            logger.LogDebug(
                "Rebalancing optimizer result. NeedsRebalancing: {NeedsRebalancing}; TradeCount: {TradeCount}; TotalTransactionCost: {TotalTransactionCost}; ExpectedImprovement: {ExpectedImprovement}.",
                result.NeedsRebalancing,
                result.SuggestedTrades.Count,
                result.TotalTransactionCost,
                result.ExpectedImprovement);
            foreach (var trade in result.SuggestedTrades)
            {
                logger.LogDebug(
                    "Suggested trade. AssetSymbol: {AssetSymbol}; Action: {Action}; Quantity: {Quantity}; EstimatedValue: {EstimatedValue}; TransactionCost: {TransactionCost}.",
                    trade.Symbol,
                    trade.Action,
                    trade.Quantity,
                    trade.EstimatedValue,
                    trade.TransactionCost);
            }
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
            var currentValue = position.CurrentValue(asset.CurrentPrice).Value;
            logger.LogDebug(
                "Rebalancing position loaded. AssetSymbol: {AssetSymbol}; CurrentValue: {CurrentValue}; CurrentPrice: {CurrentPrice}; TargetWeight: {TargetWeight}.",
                position.AssetSymbol.Value,
                currentValue,
                asset.CurrentPrice.Value,
                position.TargetAllocation.Value);
            result.Add(new RebalancingPosition(position.AssetSymbol.Value, currentValue, asset.CurrentPrice.Value, position.TargetAllocation.Value));
        }

        return result;
    }
}
