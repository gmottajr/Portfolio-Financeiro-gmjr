using MediatR;
using Microsoft.Extensions.Logging;
using Models;
using SharedKernel.ValueObjects;
using Application.Performance.Services;
using Application.Exceptions;

namespace Application.Performance.Queries;

public sealed class GetPortfolioPerformanceQueryHandler(
    IPortfolioPerformanceDataReader dataReader,
    IPerformanceCalculator calculator,
    ILogger<GetPortfolioPerformanceQueryHandler> logger)
    : IRequestHandler<GetPortfolioPerformanceQuery, PortfolioPerformanceResponse?>
{
    public async Task<PortfolioPerformanceResponse?> Handle(
        GetPortfolioPerformanceQuery request,
        CancellationToken ct)
    {
        using var scope = logger.BeginScope(new Dictionary<string, object>
        {
            ["Operation"] = "GetPortfolioPerformance",
            ["PortfolioId"] = request.PortfolioId
        });

        logger.LogInformation("Starting portfolio performance analysis.");

        try
        {
            var portfolio = await dataReader.GetPortfolioAsync(request.PortfolioId, ct);
            if (portfolio is null)
            {
                logger.LogWarning("Portfolio was not found.");
                return null;
            }

            logger.LogInformation("Portfolio loaded with {PositionCount} positions.", portfolio.Positions.Count);
            var assets = await LoadAssetsAsync(portfolio, ct);
            logger.LogInformation("Loaded {AssetCount} assets required for performance analysis.", assets.Count);

            // Current prices in the seed are a market snapshot. Use its latest
            // timestamp so annualized returns stay reproducible instead of
            // changing with the API server clock.
            var calculationDate = assets.Values.Max(asset => asset.LastUpdated);
            var response = calculator.Calculate(portfolio, assets, calculationDate);
            logger.LogInformation(
                "Portfolio performance analysis completed. CurrentValue: {CurrentValue}; TotalReturn: {TotalReturn}; Volatility: {Volatility}.",
                response.CurrentValue,
                response.TotalReturn,
                response.Volatility);

            return response;
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Portfolio performance analysis failed.");
            throw;
        }
    }

    private async Task<IReadOnlyDictionary<AssetSymbol, Asset>> LoadAssetsAsync(
        Portfolio portfolio,
        CancellationToken ct)
    {
        var assets = new Dictionary<AssetSymbol, Asset>();
        foreach (var position in portfolio.Positions)
        {
            logger.LogDebug("Loading asset {AssetSymbol}.", position.AssetSymbol.Value);
            var asset = await dataReader.GetAssetAsync(position.AssetSymbol, ct)
                ?? throw new PortfolioDataIncompleteException(
                    $"Asset '{position.AssetSymbol}' was not found for portfolio {portfolio.Id}.");
            assets.Add(position.AssetSymbol, asset);
        }

        return assets;
    }
}
