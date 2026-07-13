using System.Globalization;
using SharedKernel.Enums;

namespace Application.Rebalancing;

internal static class RebalancingPlanEngine
{
    private const int SolverIterations = 80;
    private const decimal NearOptimalRatio = 0.95m;

    internal static RebalancingProblem CreateProblem(IReadOnlyList<RebalancingPosition> positions) =>
        new(
            positions,
            positions.Sum(position => position.CurrentValue),
            positions.Sum(position => position.TargetWeight));

    internal static IReadOnlyList<CurrentAllocationResult> BuildAllocations(RebalancingProblem problem) =>
        problem.Positions
            .Select(position =>
            {
                var currentWeight = Weight(position.CurrentValue, problem.TotalValue);
                var targetWeight = NormalizedTarget(position, problem.TargetTotal);
                return new CurrentAllocationResult(
                    position.Symbol,
                    currentWeight,
                    targetWeight,
                    decimal.Round(currentWeight - targetWeight, 4));
            })
            .OrderByDescending(allocation => Math.Abs(allocation.Deviation))
            .ThenBy(allocation => allocation.Symbol)
            .ToList();

    internal static IReadOnlyList<RebalancingPosition> EligiblePositions(RebalancingProblem problem) =>
        problem.Positions
            .Where(position => position.Price > 0m)
            .Where(position => Math.Abs(
                Weight(position.CurrentValue, problem.TotalValue) -
                NormalizedTarget(position, problem.TargetTotal)) > problem.DeviationThreshold)
            .OrderByDescending(position => Math.Abs(
                Weight(position.CurrentValue, problem.TotalValue) -
                NormalizedTarget(position, problem.TargetTotal)))
            .ThenBy(position => position.Symbol)
            .ToList();

    internal static RebalancingStrategyPlan BuildFromDesiredWeights(
        string strategy,
        string title,
        string description,
        RebalancingProblem problem,
        IReadOnlyDictionary<string, decimal> desiredWeights,
        IReadOnlySet<string> selectedSymbols)
    {
        if (problem.TotalValue <= 0m || selectedSymbols.Count == 0)
            return Empty(strategy, title, description, problem);

        var selected = problem.Positions
            .Where(position => selectedSymbols.Contains(position.Symbol) && position.Price > 0m)
            .ToList();
        if (selected.Count == 0)
            return Empty(strategy, title, description, problem);

        var postCostValue = SolvePostCostValue(problem, selected, desiredWeights);
        var trades = new List<PlannedTrade>();
        foreach (var position in selected)
        {
            var desiredWeight = desiredWeights.GetValueOrDefault(position.Symbol);
            var targetValue = postCostValue * desiredWeight / 100m;
            var delta = targetValue - position.CurrentValue;
            var value = decimal.Round(Math.Abs(delta), 4, MidpointRounding.AwayFromZero);
            if (value < problem.MinimumTradeValue) continue;

            var quantity = decimal.Round(value / position.Price, 4, MidpointRounding.AwayFromZero);
            value = decimal.Round(quantity * position.Price, 4, MidpointRounding.AwayFromZero);
            if (quantity <= 0m || value < problem.MinimumTradeValue) continue;

            trades.Add(new PlannedTrade(
                position.Symbol,
                delta < 0m ? TradeActionEnum.Sell : TradeActionEnum.Buy,
                position.Price,
                quantity,
                value,
                RoundCost(value, problem.TransactionCostRate)));
        }

        return BuildFromPlannedTrades(strategy, title, description, problem, trades);
    }

    internal static RebalancingStrategyPlan BuildFromTradeValues(
        string strategy,
        string title,
        string description,
        RebalancingProblem problem,
        IReadOnlyDictionary<string, decimal> signedTradeValues)
    {
        var positionsBySymbol = problem.Positions.ToDictionary(position => position.Symbol);
        var trades = new List<PlannedTrade>();
        foreach (var (symbol, signedValue) in signedTradeValues)
        {
            if (!positionsBySymbol.TryGetValue(symbol, out var position) || position.Price <= 0m)
                continue;
            var value = decimal.Round(Math.Abs(signedValue), 4, MidpointRounding.AwayFromZero);
            if (value < problem.MinimumTradeValue) continue;
            if (signedValue < 0m) value = decimal.Min(value, position.CurrentValue);
            var quantity = decimal.Floor(value / position.Price * 10_000m) / 10_000m;
            value = decimal.Round(quantity * position.Price, 4, MidpointRounding.AwayFromZero);
            if (quantity <= 0m || value < problem.MinimumTradeValue) continue;
            trades.Add(new PlannedTrade(
                symbol,
                signedValue < 0m ? TradeActionEnum.Sell : TradeActionEnum.Buy,
                position.Price,
                quantity,
                value,
                RoundCost(value, problem.TransactionCostRate)));
        }

        return BuildFromPlannedTrades(strategy, title, description, problem, trades);
    }

    internal static RebalancingStrategyPlan SelectNearOptimal(
        IReadOnlyList<RebalancingStrategyPlan> plans,
        RebalancingStrategyPlan fallback)
    {
        var feasible = plans
            .Where(plan => plan.Status == "Succeeded" && plan.Metrics.IsFeasible)
            .ToList();
        if (feasible.Count == 0) return fallback;

        var bestBenefit = feasible.Max(plan => plan.Metrics.NetBenefit);
        var threshold = bestBenefit * NearOptimalRatio;
        return feasible
            .Where(plan => plan.Metrics.NetBenefit >= threshold)
            .OrderBy(plan => plan.Metrics.TradeCount)
            .ThenBy(plan => plan.Trades.Sum(trade => trade.TransactionCost))
            .ThenBy(plan => plan.Metrics.TrackingErrorAfter)
            .ThenBy(plan => plan.Strategy)
            .First();
    }

    internal static RebalancingStrategyPlan Empty(
        string strategy,
        string title,
        string description,
        RebalancingProblem problem,
        string status = "Succeeded",
        string? message = null) =>
        new(
            strategy,
            title,
            description,
            status,
            [],
            Evaluate(problem, []),
            message);

    internal static decimal NormalizedTarget(RebalancingPosition position, decimal targetTotal) =>
        targetTotal <= 0m ? 0m : position.TargetWeight / targetTotal * 100m;

    private static RebalancingStrategyPlan BuildFromPlannedTrades(
        string strategy,
        string title,
        string description,
        RebalancingProblem problem,
        List<PlannedTrade> trades)
    {
        trades = EnforceSelfFinancing(trades, problem);
        var metrics = Evaluate(problem, trades);
        var projected = ProjectValues(problem, trades);
        var projectedTotal = problem.TotalValue - trades.Sum(trade => trade.TransactionCost);
        var resultTrades = trades
            .OrderByDescending(trade => Math.Abs(
                Weight(problem.Positions.First(position => position.Symbol == trade.Symbol).CurrentValue, problem.TotalValue) -
                NormalizedTarget(problem.Positions.First(position => position.Symbol == trade.Symbol), problem.TargetTotal)))
            .ThenBy(trade => trade.Symbol)
            .Select(trade =>
            {
                var position = problem.Positions.First(item => item.Symbol == trade.Symbol);
                var before = Weight(position.CurrentValue, problem.TotalValue);
                var after = Weight(projected[trade.Symbol], projectedTotal);
                var target = NormalizedTarget(position, problem.TargetTotal);
                return new SuggestedTradeResult(
                    trade.Symbol,
                    trade.Action,
                    trade.Quantity,
                    trade.EstimatedValue,
                    trade.TransactionCost,
                    $"{(trade.Action == TradeActionEnum.Sell ? "Reduzir" : "Aumentar")} de {before.ToString("F2", CultureInfo.InvariantCulture)}% para {after.ToString("F2", CultureInfo.InvariantCulture)}% (alvo {target.ToString("F2", CultureInfo.InvariantCulture)}%).");
            })
            .ToList();

        return new RebalancingStrategyPlan(
            strategy,
            title,
            description,
            "Succeeded",
            resultTrades,
            metrics,
            metrics.IsFeasible ? null : "O plano não produziu benefício líquido autofinanciado.");
    }

    private static decimal SolvePostCostValue(
        RebalancingProblem problem,
        IReadOnlyList<RebalancingPosition> selected,
        IReadOnlyDictionary<string, decimal> desiredWeights)
    {
        var low = 0m;
        var high = problem.TotalValue;
        for (var iteration = 0; iteration < SolverIterations; iteration++)
        {
            var value = (low + high) / 2m;
            var costs = selected.Sum(position =>
                Math.Abs(value * desiredWeights.GetValueOrDefault(position.Symbol) / 100m - position.CurrentValue)) *
                problem.TransactionCostRate;
            if (value + costs > problem.TotalValue) high = value;
            else low = value;
        }

        return decimal.Round((low + high) / 2m, 8);
    }

    private static List<PlannedTrade> EnforceSelfFinancing(
        List<PlannedTrade> trades,
        RebalancingProblem problem)
    {
        var salesNet = trades
            .Where(trade => trade.Action == TradeActionEnum.Sell)
            .Sum(trade => trade.EstimatedValue - trade.TransactionCost);
        var purchasesGross = trades
            .Where(trade => trade.Action == TradeActionEnum.Buy)
            .Sum(trade => trade.EstimatedValue + trade.TransactionCost);
        if (purchasesGross <= salesNet) return trades;

        foreach (var trade in trades
                     .Where(trade => trade.Action == TradeActionEnum.Buy)
                     .OrderBy(trade => BenefitPriority(trade, problem))
                     .ThenBy(trade => trade.Symbol)
                     .ToList())
        {
            var otherPurchases = trades
                .Where(item => item.Action == TradeActionEnum.Buy && item != trade)
                .Sum(item => item.EstimatedValue + item.TransactionCost);
            var allowedValue = decimal.Max(0m, salesNet - otherPurchases) /
                               (1m + problem.TransactionCostRate);
            if (allowedValue >= trade.EstimatedValue) continue;
            if (allowedValue < problem.MinimumTradeValue)
            {
                trades.Remove(trade);
                continue;
            }

            var quantity = decimal.Floor(allowedValue / trade.Price * 10_000m) / 10_000m;
            var value = decimal.Round(quantity * trade.Price, 4, MidpointRounding.AwayFromZero);
            if (quantity <= 0m || value < problem.MinimumTradeValue)
                trades.Remove(trade);
            else
                trades[trades.IndexOf(trade)] = trade with
                {
                    Quantity = quantity,
                    EstimatedValue = value,
                    TransactionCost = RoundCost(value, problem.TransactionCostRate)
                };
        }

        return trades;
    }

    private static RebalancingPlanMetricsResult Evaluate(
        RebalancingProblem problem,
        IReadOnlyList<PlannedTrade> trades)
    {
        var values = ProjectValues(problem, trades);
        var totalCost = trades.Sum(trade => trade.TransactionCost);
        var projectedTotal = problem.TotalValue - totalCost;
        var trackingBefore = problem.Positions.Sum(position => Math.Abs(
            Weight(position.CurrentValue, problem.TotalValue) -
            NormalizedTarget(position, problem.TargetTotal)));
        var trackingAfter = projectedTotal <= 0m
            ? trackingBefore
            : problem.Positions.Sum(position => Math.Abs(
                Weight(values[position.Symbol], projectedTotal) -
                NormalizedTarget(position, problem.TargetTotal)));
        var improvement = decimal.Max(0m, trackingBefore - trackingAfter);
        var costImpact = problem.TotalValue <= 0m ? 0m : totalCost / problem.TotalValue * 100m;
        var netBenefit = improvement - costImpact;
        var salesNet = trades.Where(trade => trade.Action == TradeActionEnum.Sell)
            .Sum(trade => trade.EstimatedValue - trade.TransactionCost);
        var purchasesGross = trades.Where(trade => trade.Action == TradeActionEnum.Buy)
            .Sum(trade => trade.EstimatedValue + trade.TransactionCost);
        var selfFinanced = purchasesGross <= salesNet + 0.01m;
        var validTrades = trades.All(trade =>
            trade.EstimatedValue >= problem.MinimumTradeValue && trade.Quantity > 0m);
        var feasible = trades.Count > 0 && selfFinanced && validTrades && netBenefit > 0m;

        return new RebalancingPlanMetricsResult(
            decimal.Round(trackingBefore, 4),
            decimal.Round(trackingAfter, 4),
            decimal.Round(improvement, 4),
            decimal.Round(costImpact, 4),
            decimal.Round(netBenefit, 4),
            trades.Count,
            selfFinanced,
            feasible);
    }

    private static Dictionary<string, decimal> ProjectValues(
        RebalancingProblem problem,
        IReadOnlyList<PlannedTrade> trades)
    {
        var values = problem.Positions.ToDictionary(position => position.Symbol, position => position.CurrentValue);
        foreach (var trade in trades)
            values[trade.Symbol] += trade.Action == TradeActionEnum.Buy ? trade.EstimatedValue : -trade.EstimatedValue;
        return values;
    }

    private static decimal BenefitPriority(PlannedTrade trade, RebalancingProblem problem)
    {
        var position = problem.Positions.First(item => item.Symbol == trade.Symbol);
        return Math.Abs(
            Weight(position.CurrentValue, problem.TotalValue) -
            NormalizedTarget(position, problem.TargetTotal));
    }

    private static decimal RoundCost(decimal value, decimal rate) =>
        decimal.Round(value * rate, 2, MidpointRounding.AwayFromZero);

    private static decimal Weight(decimal value, decimal total) =>
        total <= 0m ? 0m : value / total * 100m;

    private sealed record PlannedTrade(
        string Symbol,
        TradeActionEnum Action,
        decimal Price,
        decimal Quantity,
        decimal EstimatedValue,
        decimal TransactionCost);
}
