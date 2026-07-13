namespace Application.Rebalancing;

public enum RebalancingOptimizationMode
{
    Recommended,
    Exhaustive,
    QuadraticProgramming,
    CpSat,
    CompareAll
}

public sealed record RebalancingPosition(
    string Symbol,
    decimal CurrentValue,
    decimal Price,
    decimal TargetWeight);

public sealed record CurrentAllocation(
    string Symbol,
    decimal CurrentWeight,
    decimal TargetWeight,
    decimal Deviation);

public sealed record SuggestedTrade(
    string Symbol,
    string Action,
    decimal Quantity,
    decimal EstimatedValue,
    decimal TransactionCost,
    string Reason);

public sealed record RebalancingPlanMetrics(
    decimal TrackingErrorBefore,
    decimal TrackingErrorAfter,
    decimal GrossImprovement,
    decimal CostImpact,
    decimal NetBenefit,
    int TradeCount,
    bool IsSelfFinanced,
    bool IsFeasible);

public sealed record RebalancingStrategyComparison(
    string Strategy,
    string Title,
    string Description,
    string Status,
    RebalancingPlanMetrics Metrics,
    IReadOnlyList<SuggestedTrade> SuggestedTrades,
    string? Message);

public sealed record RebalancingOptimizationComparison(
    string RequestedMode,
    string SelectedStrategy,
    string SelectionReason,
    IReadOnlyList<RebalancingStrategyComparison> Alternatives);

public sealed record RebalancingResponse(
    bool NeedsRebalancing,
    IReadOnlyList<CurrentAllocation> CurrentAllocation,
    IReadOnlyList<SuggestedTrade> SuggestedTrades,
    decimal TotalTransactionCost,
    string ExpectedImprovement,
    RebalancingOptimizationComparison? Optimization = null);

public sealed record CurrentAllocationResult(
    string Symbol,
    decimal CurrentWeight,
    decimal TargetWeight,
    decimal Deviation);

public sealed record SuggestedTradeResult(
    string Symbol,
    string Action,
    decimal Quantity,
    decimal EstimatedValue,
    decimal TransactionCost,
    string Reason);

public sealed record RebalancingPlanMetricsResult(
    decimal TrackingErrorBefore,
    decimal TrackingErrorAfter,
    decimal GrossImprovement,
    decimal CostImpact,
    decimal NetBenefit,
    int TradeCount,
    bool IsSelfFinanced,
    bool IsFeasible);

public sealed record RebalancingStrategyComparisonResult(
    string Strategy,
    string Title,
    string Description,
    string Status,
    RebalancingPlanMetricsResult Metrics,
    IReadOnlyList<SuggestedTradeResult> SuggestedTrades,
    string? Message);

public sealed record RebalancingOptimizationComparisonResult(
    string RequestedMode,
    string SelectedStrategy,
    string SelectionReason,
    IReadOnlyList<RebalancingStrategyComparisonResult> Alternatives);

public sealed record RebalancingResult(
    bool NeedsRebalancing,
    IReadOnlyList<CurrentAllocationResult> CurrentAllocation,
    IReadOnlyList<SuggestedTradeResult> SuggestedTrades,
    decimal TotalTransactionCost,
    string ExpectedImprovement,
    RebalancingOptimizationComparisonResult? Optimization = null);

public sealed record RebalancingProblem(
    IReadOnlyList<RebalancingPosition> Positions,
    decimal TotalValue,
    decimal TargetTotal,
    decimal DeviationThreshold = 2m,
    decimal MinimumTradeValue = 100m,
    decimal TransactionCostRate = 0.003m);

public sealed record RebalancingStrategyPlan(
    string Strategy,
    string Title,
    string Description,
    string Status,
    IReadOnlyList<SuggestedTradeResult> Trades,
    RebalancingPlanMetricsResult Metrics,
    string? Message = null);

