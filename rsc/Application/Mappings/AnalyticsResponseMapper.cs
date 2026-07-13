using Application.Performance;
using Application.Rebalancing;
using Application.Risk;
using Riok.Mapperly.Abstractions;

namespace Application.Mappings;

/// <summary>Converts Application calculation results into API DTOs.</summary>
[Mapper]
internal static partial class AnalyticsResponseMapper
{
    internal static partial PortfolioPerformanceResponse ToResponse(PortfolioPerformanceResult source);
    internal static partial PositionPerformanceResponse ToResponse(PositionPerformanceResult source);
    internal static partial RiskAnalysisResponse ToResponse(RiskAnalysisResult source);
    internal static partial ConcentrationRiskResponse ToResponse(ConcentrationRiskResult source);
    internal static partial LargestPositionRisk ToResponse(LargestPositionRiskResult source);
    internal static partial SectorDiversificationResponse ToResponse(SectorDiversificationResult source);
    internal static partial RebalancingResponse ToResponse(RebalancingResult source);
    internal static partial CurrentAllocation ToResponse(CurrentAllocationResult source);
    internal static partial SuggestedTrade ToResponse(SuggestedTradeResult source);
    internal static partial RebalancingPlanMetrics ToResponse(RebalancingPlanMetricsResult source);
    internal static partial RebalancingStrategyComparison ToResponse(RebalancingStrategyComparisonResult source);
    internal static partial RebalancingOptimizationComparison ToResponse(RebalancingOptimizationComparisonResult source);
}
