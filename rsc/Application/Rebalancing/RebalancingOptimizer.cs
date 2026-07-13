using System.Globalization;

namespace Application.Rebalancing;

public interface IRebalancingOptimizer
{
    RebalancingResult Optimize(
        IReadOnlyList<RebalancingPosition> positions,
        RebalancingOptimizationMode mode = RebalancingOptimizationMode.CompareAll);
}

/// <summary>
/// Orchestrates interchangeable optimization strategies and applies one common
/// evaluator before selecting the recommended plan.
/// </summary>
public sealed class RebalancingOptimizer : IRebalancingOptimizer
{
    private readonly RebalancingStrategyRegistry _registry;

    public RebalancingOptimizer()
        : this(new RebalancingStrategyRegistry(
        [
            new ExhaustiveSubsetOptimizationStrategy(),
            new QuadraticProgrammingOptimizationStrategy(),
            new CpSatOptimizationStrategy()
        ]))
    {
    }

    public RebalancingOptimizer(RebalancingStrategyRegistry registry)
    {
        _registry = registry;
    }

    public RebalancingResult Optimize(
        IReadOnlyList<RebalancingPosition> positions,
        RebalancingOptimizationMode mode = RebalancingOptimizationMode.CompareAll)
    {
        ArgumentNullException.ThrowIfNull(positions);
        var problem = RebalancingPlanEngine.CreateProblem(positions);
        var allocations = RebalancingPlanEngine.BuildAllocations(problem);
        if (positions.Count == 0 || problem.TotalValue <= 0m || problem.TargetTotal <= 0m)
            return Empty(allocations, mode);

        var strategies = ResolveStrategies(mode);
        var plans = strategies.Select(strategy => ExecuteSafely(strategy, problem)).ToList();
        var fallback = RebalancingPlanEngine.Empty(
            "None",
            "Nenhuma estratégia",
            "Nenhum plano viável foi produzido.",
            problem);
        var selected = RebalancingPlanEngine.SelectNearOptimal(plans, fallback);
        var totalCost = selected.Trades.Sum(trade => trade.TransactionCost);
        var needsRebalancing = selected.Metrics.IsFeasible;
        var alternatives = plans.Select(ToComparison).ToList();
        var selectionReason = needsRebalancing
            ? $"Plano dentro de 95% do melhor benefício líquido, com {selected.Metrics.TradeCount} trade(s) e custo de {totalCost.ToString("C2", CultureInfo.GetCultureInfo("pt-BR"))}."
            : "Nenhuma estratégia produziu benefício líquido autofinanciado.";

        return new RebalancingResult(
            needsRebalancing,
            allocations,
            needsRebalancing ? selected.Trades : [],
            needsRebalancing ? totalCost : 0m,
            needsRebalancing
                ? $"Redução estimada de {selected.Metrics.GrossImprovement.ToString("F2", CultureInfo.InvariantCulture)} pontos no desvio total de alocação."
                : "Nenhuma operação atende aos critérios de rebalanceamento.",
            new RebalancingOptimizationComparisonResult(
                mode.ToString(),
                needsRebalancing ? selected.Strategy : "None",
                selectionReason,
                alternatives));
    }

    private IReadOnlyList<IRebalancingOptimizationStrategy> ResolveStrategies(
        RebalancingOptimizationMode mode) => mode switch
    {
        RebalancingOptimizationMode.CompareAll => _registry.GetAll(),
        RebalancingOptimizationMode.Recommended => [_registry.Get(RebalancingOptimizationMode.Exhaustive)],
        _ => [_registry.Get(mode)]
    };

    private static RebalancingStrategyPlan ExecuteSafely(
        IRebalancingOptimizationStrategy strategy,
        RebalancingProblem problem)
    {
        try
        {
            return strategy.Optimize(problem);
        }
        catch (Exception exception)
        {
            return RebalancingPlanEngine.Empty(
                strategy.Key.ToString(),
                strategy.Title,
                strategy.Description,
                problem,
                "Failed",
                $"{exception.GetType().Name}: {exception.Message}");
        }
    }

    private static RebalancingStrategyComparisonResult ToComparison(RebalancingStrategyPlan plan) =>
        new(
            plan.Strategy,
            plan.Title,
            plan.Description,
            plan.Status,
            plan.Metrics,
            plan.Trades,
            plan.Message);

    private static RebalancingResult Empty(
        IReadOnlyList<CurrentAllocationResult> allocations,
        RebalancingOptimizationMode mode) =>
        new(
            false,
            allocations,
            [],
            0m,
            "Nenhuma operação atende aos critérios de rebalanceamento.",
            new RebalancingOptimizationComparisonResult(
                mode.ToString(),
                "None",
                "A carteira não possui valor, targets ou desvios elegíveis.",
                []));
}
