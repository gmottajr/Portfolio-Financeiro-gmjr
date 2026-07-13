namespace Application.Rebalancing;

public interface IRebalancingOptimizationStrategy
{
    RebalancingOptimizationMode Key { get; }
    string Title { get; }
    string Description { get; }

    RebalancingStrategyPlan Optimize(RebalancingProblem problem);
}

public sealed class RebalancingStrategyRegistry(
    IEnumerable<IRebalancingOptimizationStrategy> strategies)
{
    private readonly IReadOnlyDictionary<RebalancingOptimizationMode, IRebalancingOptimizationStrategy> _strategies =
        strategies.ToDictionary(strategy => strategy.Key);

    public IRebalancingOptimizationStrategy Get(RebalancingOptimizationMode mode) =>
        _strategies.TryGetValue(mode, out var strategy)
            ? strategy
            : throw new NotSupportedException($"Rebalancing strategy '{mode}' is not registered.");

    public IReadOnlyList<IRebalancingOptimizationStrategy> GetAll() =>
        _strategies.Values.OrderBy(strategy => strategy.Key).ToList();
}

