using System.Globalization;
using Application.Contracts;
using Application.Exceptions;
using Application.Mappings;
using Microsoft.Extensions.Logging;

namespace Application.Rebalancing;

public sealed record CurrentAllocation(string Symbol, decimal CurrentWeight, decimal TargetWeight, decimal Deviation);

public sealed record SuggestedTrade(
    string Symbol,
    string Action,
    decimal Quantity,
    decimal EstimatedValue,
    decimal TransactionCost,
    string Reason);

public sealed record RebalancingResponse(
    bool NeedsRebalancing,
    IReadOnlyList<CurrentAllocation> CurrentAllocation,
    IReadOnlyList<SuggestedTrade> SuggestedTrades,
    decimal TotalTransactionCost,
    string ExpectedImprovement);

internal sealed record CurrentAllocationResult(string Symbol, decimal CurrentWeight, decimal TargetWeight, decimal Deviation);
internal sealed record SuggestedTradeResult(string Symbol, string Action, decimal Quantity, decimal EstimatedValue, decimal TransactionCost, string Reason);
internal sealed record RebalancingResult(bool NeedsRebalancing, IReadOnlyList<CurrentAllocationResult> CurrentAllocation, IReadOnlyList<SuggestedTradeResult> SuggestedTrades, decimal TotalTransactionCost, string ExpectedImprovement);

public sealed class GenerateRebalancingSuggestionsUseCase(
    IPortfolioPositionsReader portfolios,
    IAssetReader assets,
    ILogger<GenerateRebalancingSuggestionsUseCase> logger) : IGenerateRebalancingSuggestionsUseCase
{
    private const decimal DeviationThreshold = 2m;
    private const decimal MinimumTradeValue = 100m;
    private const decimal TransactionCostRate = 0.003m;

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
            var totalValue = positions.Sum(position => position.CurrentValue);
            var allocations = positions
                .Select(position => new AllocationRow(
                    position.Symbol,
                    position.CurrentValue,
                    position.Price,
                    CalculateWeight(position.CurrentValue, totalValue),
                    position.TargetWeight))
                .ToList();
            var currentAllocation = allocations
                .Select(allocation => new CurrentAllocationResult(
                    allocation.Symbol,
                    allocation.CurrentWeight,
                    allocation.TargetWeight,
                    decimal.Round(allocation.CurrentWeight - allocation.TargetWeight, 4)))
                .OrderByDescending(allocation => Math.Abs(allocation.Deviation))
                .ThenBy(allocation => allocation.Symbol)
                .ToList();
            var trades = BuildTrades(allocations, totalValue);
            var totalTransactionCost = trades.Sum(trade => trade.TransactionCost);
            var expectedImprovement = BuildExpectedImprovement(allocations, trades, totalValue);

            logger.LogInformation(
                "Generated {TradeCount} rebalancing suggestions for portfolio {PortfolioId}. Total transaction cost: {TotalTransactionCost}.",
                trades.Count,
                portfolioId,
                totalTransactionCost);

            return AnalyticsResponseMapper.ToResponse(new RebalancingResult(
                trades.Count > 0,
                currentAllocation,
                trades,
                totalTransactionCost,
                expectedImprovement));
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Rebalancing suggestion generation failed for portfolio {PortfolioId}.", portfolioId);
            throw;
        }
    }

    private async Task<IReadOnlyList<PositionRow>> LoadPositionsAsync(Models.Portfolio portfolio, CancellationToken ct)
    {
        var result = new List<PositionRow>();
        foreach (var position in portfolio.Positions)
        {
            var asset = await assets.GetByIdAsync(position.AssetSymbol, ct)
                ?? throw new PortfolioDataIncompleteException($"Asset {position.AssetSymbol} was not found.");
            result.Add(new PositionRow(
                position.AssetSymbol.Value,
                position.CurrentValue(asset.CurrentPrice).Value,
                asset.CurrentPrice.Value,
                position.TargetAllocation.Value));
        }

        return result;
    }

    private IReadOnlyList<SuggestedTradeResult> BuildTrades(IReadOnlyList<AllocationRow> allocations, decimal totalValue)
    {
        if (totalValue <= 0m)
        {
            return [];
        }

        var trades = new List<SuggestedTradeResult>();
        foreach (var allocation in allocations
                     .Where(allocation => Math.Abs(allocation.CurrentWeight - allocation.TargetWeight) > DeviationThreshold)
                     .OrderByDescending(allocation => Math.Abs(allocation.CurrentWeight - allocation.TargetWeight))
                     .ThenBy(allocation => allocation.Symbol))
        {
            if (allocation.Price <= 0m)
            {
                logger.LogWarning("Skipping {AssetSymbol}: current price is zero.", allocation.Symbol);
                continue;
            }

            var targetValue = totalValue * allocation.TargetWeight / 100m;
            var theoreticalValue = Math.Abs(targetValue - allocation.CurrentValue);
            // Quantity supports fractional units. Keeping four decimal places
            // produces the closest executable adjustment instead of always
            // under-correcting the target allocation.
            var quantity = decimal.Round(theoreticalValue / allocation.Price, 4, MidpointRounding.AwayFromZero);
            var estimatedValue = quantity * allocation.Price;

            // One trade per deviating position is the minimum possible number of
            // operations. Skipping sub-R$100 trades avoids negligible adjustments
            // whose operational cost outweighs the allocation benefit.
            if (quantity <= 0m || estimatedValue < MinimumTradeValue)
            {
                continue;
            }

            var action = allocation.CurrentWeight > allocation.TargetWeight ? "SELL" : "BUY";
            var transactionCost = decimal.Round(
                estimatedValue * TransactionCostRate,
                2,
                MidpointRounding.AwayFromZero);
            trades.Add(new SuggestedTradeResult(
                allocation.Symbol,
                action,
                quantity,
                estimatedValue,
                transactionCost,
                $"{(action == "SELL" ? "Reduzir" : "Aumentar")} de {Format(allocation.CurrentWeight)}% para {Format(allocation.TargetWeight)}%."));
        }

        return trades;
    }

    private static string BuildExpectedImprovement(
        IReadOnlyList<AllocationRow> allocations,
        IReadOnlyList<SuggestedTradeResult> trades,
        decimal totalValue)
    {
        if (trades.Count == 0 || totalValue <= 0m)
        {
            return "Nenhuma operação atende aos critérios de rebalanceamento.";
        }

        var tradeBySymbol = trades.ToDictionary(trade => trade.Symbol);
        var currentConcentration = allocations.Max(allocation => allocation.CurrentWeight);
        var projectedValues = allocations.Select(allocation =>
        {
            if (!tradeBySymbol.TryGetValue(allocation.Symbol, out var trade))
            {
                return allocation.CurrentValue;
            }

            return trade.Action == "SELL"
                ? allocation.CurrentValue - trade.EstimatedValue
                : allocation.CurrentValue + trade.EstimatedValue;
        });
        var projectedTotal = projectedValues.Sum();
        var projectedConcentration = projectedTotal == 0m
            ? 0m
            : projectedValues.Max() / projectedTotal * 100m;
        var improvement = Math.Max(0m, currentConcentration - projectedConcentration);

        return $"Redução estimada de {Format(improvement)} pontos percentuais no risco de concentração.";
    }

    private static decimal CalculateWeight(decimal positionValue, decimal totalValue) =>
        totalValue == 0m ? 0m : decimal.Round(positionValue / totalValue * 100m, 4);

    private static string Format(decimal value) => value.ToString("F1", CultureInfo.InvariantCulture);

    private sealed record PositionRow(string Symbol, decimal CurrentValue, decimal Price, decimal TargetWeight);
    private sealed record AllocationRow(string Symbol, decimal CurrentValue, decimal Price, decimal CurrentWeight, decimal TargetWeight);
}
