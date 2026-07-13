namespace Application.Risk;

public interface IRiskAnalysisAppService
{
    Task<RiskAnalysisResponse?> AnalyzeAsync(int portfolioId, CancellationToken ct = default);
}
