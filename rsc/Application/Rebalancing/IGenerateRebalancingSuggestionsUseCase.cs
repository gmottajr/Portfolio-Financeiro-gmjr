namespace Application.Rebalancing;

public interface IGenerateRebalancingSuggestionsUseCase
{
    Task<RebalancingResponse?> ExecuteAsync(int portfolioId, CancellationToken ct = default);
}
