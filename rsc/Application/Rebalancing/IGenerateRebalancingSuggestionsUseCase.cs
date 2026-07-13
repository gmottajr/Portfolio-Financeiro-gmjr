namespace Application.Rebalancing;

public interface IGenerateRebalancingSuggestionsUseCase
{
    Task<RebalancingResponse?> ExecuteAsync(
        int portfolioId,
        RebalancingOptimizationMode mode = RebalancingOptimizationMode.CompareAll,
        CancellationToken ct = default);
}
