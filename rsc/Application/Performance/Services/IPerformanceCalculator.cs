using Models;
using SharedKernel.ValueObjects;

namespace Application.Performance.Services;

public interface IPerformanceCalculator
{
    PortfolioPerformanceResponse Calculate(
        Portfolio portfolio,
        IReadOnlyDictionary<AssetSymbol, Asset> assets,
        DateTime calculationDate);
}
