using System.Globalization;

namespace Application.Rebalancing;

public sealed record RebalancingPosition(string Symbol, decimal CurrentValue, decimal Price, decimal TargetWeight);

public interface IRebalancingOptimizer
{
    RebalancingResult Optimize(IReadOnlyList<RebalancingPosition> positions);
}

/// <summary>
/// Produces a self-financed plan. It solves the post-cost portfolio value before
/// calculating trades, so purchases, sales and 0.3% fees form one feasible plan.
/// </summary>
public sealed class RebalancingOptimizer : IRebalancingOptimizer
{
    private const decimal DeviationThreshold = 2m;
    private const decimal MinimumTradeValue = 100m;
    private const decimal TransactionCostRate = 0.003m;
    private const int SolverIterations = 80;

    public RebalancingResult Optimize(IReadOnlyList<RebalancingPosition> positions)
    {
        ArgumentNullException.ThrowIfNull(positions);
        if (positions.Count == 0) return Empty([]);

        var totalValue = positions.Sum(position => position.CurrentValue);
        var allocations = BuildAllocations(positions, totalValue);
        if (totalValue <= 0m) return Empty(allocations);

        var targetTotal = positions.Sum(position => position.TargetWeight);
        if (targetTotal <= 0m) return Empty(allocations);

        var candidates = positions
            .Where(position => position.Price > 0m)
            .Where(position => Math.Abs(CalculateWeight(position.CurrentValue, totalValue) - position.TargetWeight) > DeviationThreshold)
            .ToList();
        if (candidates.Count == 0) return Empty(allocations);

        // Solve Vpost + feeRate × Σ|Vpost × normalizedTarget[i] - currentValue[i]| = Vcurrent.
        // This reserves transaction fees before allocating target values.
        var postCostValue = SolvePostCostValue(totalValue, candidates, targetTotal);
        var trades = BuildExecutableTrades(candidates, postCostValue, targetTotal, totalValue);
        trades = EnforceSelfFinancing(trades);
        var totalCost = trades.Sum(trade => trade.TransactionCost);

        return new RebalancingResult(
            trades.Count > 0,
            allocations,
            trades.OrderByDescending(trade => Math.Abs(trade.Deviation)).ThenBy(trade => trade.Symbol).Select(ToResult).ToList(),
            totalCost,
            BuildExpectedImprovement(positions, trades, totalValue, totalCost));
    }

    private static IReadOnlyList<CurrentAllocationResult> BuildAllocations(IReadOnlyList<RebalancingPosition> positions, decimal totalValue) =>
        positions.Select(position =>
        {
            var weight = CalculateWeight(position.CurrentValue, totalValue);
            return new CurrentAllocationResult(position.Symbol, weight, position.TargetWeight, decimal.Round(weight - position.TargetWeight, 4));
        }).OrderByDescending(allocation => Math.Abs(allocation.Deviation)).ThenBy(allocation => allocation.Symbol).ToList();

    private static decimal SolvePostCostValue(decimal totalValue, IReadOnlyList<RebalancingPosition> candidates, decimal targetTotal)
    {
        var low = 0m;
        var high = totalValue;
        for (var iteration = 0; iteration < SolverIterations; iteration++)
        {
            var value = (low + high) / 2m;
            var costs = candidates.Sum(position => Math.Abs(value * position.TargetWeight / targetTotal - position.CurrentValue)) * TransactionCostRate;
            if (value + costs > totalValue) high = value;
            else low = value;
        }

        return decimal.Round((low + high) / 2m, 8);
    }

    private static List<PlannedTrade> BuildExecutableTrades(IReadOnlyList<RebalancingPosition> candidates, decimal postCostValue, decimal targetTotal, decimal totalValue)
    {
        var trades = new List<PlannedTrade>();
        foreach (var position in candidates)
        {
            // Target value[i] = post-cost portfolio value × normalized target weight[i].
            var targetValue = postCostValue * position.TargetWeight / targetTotal;
            var delta = targetValue - position.CurrentValue;
            var estimatedValue = decimal.Round(Math.Abs(delta), 4, MidpointRounding.AwayFromZero);
            if (estimatedValue < MinimumTradeValue) continue;

            // Quantity = |target value - current value| / current market price.
            var quantity = decimal.Round(estimatedValue / position.Price, 4, MidpointRounding.AwayFromZero);
            estimatedValue = decimal.Round(quantity * position.Price, 4, MidpointRounding.AwayFromZero);
            if (quantity <= 0m || estimatedValue < MinimumTradeValue) continue;

            var action = delta < 0m ? "SELL" : "BUY";
            trades.Add(new PlannedTrade(
                position.Symbol, action, position.Price, quantity, estimatedValue,
                RoundCost(estimatedValue),
                Math.Abs(CalculateWeight(position.CurrentValue, totalValue) - position.TargetWeight)));
        }

        return trades;
    }

    private static List<PlannedTrade> EnforceSelfFinancing(List<PlannedTrade> trades)
    {
        var sales = trades.Where(trade => trade.Action == "SELL").Sum(trade => trade.EstimatedValue);
        var purchases = trades.Where(trade => trade.Action == "BUY").Sum(trade => trade.EstimatedValue + trade.TransactionCost);
        if (purchases <= sales) return trades;

        // Preserve the largest deviations first and trim only purchases. This
        // prevents recommendations that require an undocumented cash injection.
        var availableForPurchases = sales - trades.Where(trade => trade.Action == "SELL").Sum(trade => trade.TransactionCost);
        foreach (var trade in trades.Where(trade => trade.Action == "BUY").OrderBy(trade => trade.Deviation).ThenBy(trade => trade.Symbol).ToList())
        {
            var otherPurchases = trades.Where(item => item.Action == "BUY" && item != trade).Sum(item => item.EstimatedValue + item.TransactionCost);
            var allowedValue = decimal.Max(0m, availableForPurchases - otherPurchases) / (1m + TransactionCostRate);
            if (allowedValue >= trade.EstimatedValue) continue;
            if (allowedValue < MinimumTradeValue) trades.Remove(trade);
            else
            {
                var quantity = decimal.Floor(allowedValue / trade.Price * 10_000m) / 10_000m;
                if (quantity <= 0m || quantity * trade.Price < MinimumTradeValue) trades.Remove(trade);
                else
                {
                    var value = decimal.Round(quantity * trade.Price, 4, MidpointRounding.AwayFromZero);
                    trades[trades.IndexOf(trade)] = trade with { Quantity = quantity, EstimatedValue = value, TransactionCost = RoundCost(value) };
                }
            }
        }

        return trades;
    }

    private static RebalancingResult Empty(IReadOnlyList<CurrentAllocationResult> allocations) => new(false, allocations, [], 0m, "Nenhuma operação atende aos critérios de rebalanceamento.");
    private static SuggestedTradeResult ToResult(PlannedTrade trade) => new(trade.Symbol, trade.Action, trade.Quantity, trade.EstimatedValue, trade.TransactionCost, $"{(trade.Action == "SELL" ? "Reduzir" : "Aumentar")} posição para aproximar a alocação-alvo.");
    // Transaction cost = trade value × 0.3%, rounded commercially to cents.
    private static decimal RoundCost(decimal value) => decimal.Round(value * TransactionCostRate, 2, MidpointRounding.AwayFromZero);
    // Current allocation (%) = position value / portfolio value × 100.
    private static decimal CalculateWeight(decimal value, decimal total) => total == 0m ? 0m : decimal.Round(value / total * 100m, 4);

    private static string BuildExpectedImprovement(IReadOnlyList<RebalancingPosition> positions, IReadOnlyList<PlannedTrade> trades, decimal totalValue, decimal totalCost)
    {
        if (trades.Count == 0) return "Nenhuma operação atende aos critérios de rebalanceamento.";
        var bySymbol = trades.ToDictionary(trade => trade.Symbol);
        var projectedMaximum = positions.Max(position =>
        {
            if (!bySymbol.TryGetValue(position.Symbol, out var trade)) return position.CurrentValue;
            return trade.Action == "SELL" ? position.CurrentValue - trade.EstimatedValue : position.CurrentValue + trade.EstimatedValue;
        });
        var projectedTotal = totalValue - totalCost;
        var currentMaximum = positions.Max(position => position.CurrentValue);
        var improvement = projectedTotal <= 0m ? 0m : Math.Max(0m, currentMaximum / totalValue * 100m - projectedMaximum / projectedTotal * 100m);
        return $"Redução estimada de {improvement.ToString("F1", CultureInfo.InvariantCulture)} pontos percentuais no risco de concentração.";
    }

    private sealed record PlannedTrade(string Symbol, string Action, decimal Price, decimal Quantity, decimal EstimatedValue, decimal TransactionCost, decimal Deviation);
}
