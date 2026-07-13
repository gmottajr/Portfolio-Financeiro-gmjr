using System.Text.Json;
using Application.Contracts;
using DAL.Data;
using Microsoft.EntityFrameworkCore;
using Models;
using SharedKernel.ValueObjects;

namespace DAL.Sower;

/// <summary>
/// Seeds assets and portfolios from the application's JSON seed data.
/// </summary>
public sealed class DataSower(
    IAssetRepository assetRepository,
    IPortfolioRepository portfolioRepository,
    PortfolioDbContext context) : IDataSower
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    /// <inheritdoc />
    public Task SowAsync(CancellationToken ct = default)
    {
        var seedFilePath = Path.Combine(AppContext.BaseDirectory, "Data", "SeedData.json");
        return SowAsync(seedFilePath, ct);
    }

    /// <inheritdoc />
    public async Task SowAsync(string seedFilePath, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(seedFilePath);

        await using var stream = File.OpenRead(seedFilePath);
        var seedData = await JsonSerializer.DeserializeAsync<SeedData>(stream, SerializerOptions, ct)
            ?? throw new InvalidOperationException("The seed data file is empty or invalid.");

        await SowAssetsAsync(seedData, ct);
        await SowPortfoliosAsync(seedData, ct);
        await SowMarketDataAsync(seedData, ct);
        await SowTestScenariosAsync(seedData, ct);
    }

    private async Task SowMarketDataAsync(SeedData seedData, CancellationToken ct)
    {
        if (seedData.MarketData is null || await context.MarketDataSnapshots.AnyAsync(ct)) return;
        var data = seedData.MarketData;
        var snapshot = new MarketDataSnapshot { SelicRate = data.SelicRate };
        foreach (var item in data.IndexPerformance)
            snapshot.Indexes.Add(new MarketIndexSnapshot { Code = item.Key, CurrentValue = item.Value.CurrentValue, DailyChange = item.Value.DailyChange, MonthlyChange = item.Value.MonthlyChange, YearToDate = item.Value.YearToDate });
        foreach (var sector in data.Sectors)
        {
            var stored = new MarketSectorSnapshot { Name = sector.Name, AverageReturn = sector.AverageReturn, Volatility = sector.Volatility };
            foreach (var symbol in sector.Assets) stored.Assets.Add(new MarketSectorAssetSnapshot { Symbol = symbol });
            snapshot.Sectors.Add(stored);
        }
        context.MarketDataSnapshots.Add(snapshot);
        await context.SaveChangesAsync(ct);
    }

    private async Task SowTestScenariosAsync(SeedData seedData, CancellationToken ct)
    {
        foreach (var scenario in seedData.TestScenarios)
        {
            if (await context.SeedTestScenarios.AnyAsync(x => x.Name == scenario.Name, ct)) continue;
            context.SeedTestScenarios.Add(new SeedTestScenarioSnapshot { Name = scenario.Name, Description = scenario.Description, PortfolioJson = scenario.Portfolio.GetRawText(), ExpectedResultsJson = scenario.ExpectedResults.GetRawText() });
        }
        await context.SaveChangesAsync(ct);
    }

    private async Task SowAssetsAsync(SeedData seedData, CancellationToken ct)
    {
        foreach (var assetData in seedData.Assets)
        {
            var symbol = new AssetSymbol(assetData.Symbol);
            if (await assetRepository.GetByIdAsync(symbol, ct) is not null)
            {
                continue;
            }

            var asset = new Asset(
                symbol,
                assetData.Name,
                assetData.Type,
                assetData.Sector,
                new Money(assetData.CurrentPrice),
                assetData.LastUpdated);

            if (seedData.PriceHistory.TryGetValue(assetData.Symbol, out var priceHistory))
            {
                asset.SetPriceHistory(priceHistory.Select(pricePoint =>
                    new PricePoint(pricePoint.Date, new Money(pricePoint.Price))));
            }

            await assetRepository.AddAsync(asset, ct);
        }

        await assetRepository.SaveChangesAsync(ct);
    }

    private async Task SowPortfoliosAsync(SeedData seedData, CancellationToken ct)
    {
        var persistedPortfolios = await portfolioRepository.GetAllAsync(ct: ct);
        var nextPortfolioId = persistedPortfolios.Count == 0
            ? 1
            : persistedPortfolios.Max(portfolio => portfolio.Id) + 1;

        foreach (var portfolioData in seedData.Portfolios)
        {
            var portfoliosForUser = await portfolioRepository.GetByUserIdAsync(portfolioData.UserId, ct);
            if (portfoliosForUser.Any(portfolio => portfolio.Name == portfolioData.Name))
            {
                continue;
            }

            var positions = portfolioData.Positions.Select(position => new Position(
                new AssetSymbol(position.AssetSymbol),
                new Quantity(position.Quantity),
                new Money(position.AveragePrice),
                Percentage.FromFraction(position.TargetAllocation),
                position.LastTransaction));

            var portfolio = new Portfolio(
                portfolioData.Name,
                portfolioData.UserId,
                new Money(portfolioData.TotalInvestment),
                portfolioData.CreatedAt,
                positions);
            portfolio.AssignId(nextPortfolioId++);

            await portfolioRepository.AddAsync(portfolio, ct);
        }

        await portfolioRepository.SaveChangesAsync(ct);
    }

    private sealed class SeedData
    {
        public List<AssetData> Assets { get; init; } = [];
        public List<PortfolioData> Portfolios { get; init; } = [];
        public Dictionary<string, List<PricePointData>> PriceHistory { get; init; } = new(StringComparer.OrdinalIgnoreCase);
        public MarketDataData? MarketData { get; init; }
        public List<TestScenarioData> TestScenarios { get; init; } = [];
    }
    private sealed class MarketDataData { public decimal SelicRate { get; init; } public Dictionary<string, IndexData> IndexPerformance { get; init; } = []; public List<SectorData> Sectors { get; init; } = []; }
    private sealed class IndexData { public decimal CurrentValue { get; init; } public decimal DailyChange { get; init; } public decimal MonthlyChange { get; init; } public decimal YearToDate { get; init; } }
    private sealed class SectorData { public required string Name { get; init; } public decimal AverageReturn { get; init; } public decimal Volatility { get; init; } public List<string> Assets { get; init; } = []; }
    private sealed class TestScenarioData { public required string Name { get; init; } public required string Description { get; init; } public JsonElement Portfolio { get; init; } public JsonElement ExpectedResults { get; init; } }

    private sealed class AssetData
    {
        public required string Symbol { get; init; }
        public required string Name { get; init; }
        public required string Type { get; init; }
        public required string Sector { get; init; }
        public decimal CurrentPrice { get; init; }
        public DateTime LastUpdated { get; init; }
    }

    private sealed class PortfolioData
    {
        public required string Name { get; init; }
        public required string UserId { get; init; }
        public decimal TotalInvestment { get; init; }
        public DateTime? CreatedAt { get; init; }
        public List<PositionData> Positions { get; init; } = [];
    }

    private sealed class PositionData
    {
        public required string AssetSymbol { get; init; }
        public decimal Quantity { get; init; }
        public decimal AveragePrice { get; init; }
        public decimal TargetAllocation { get; init; }
        public DateTime? LastTransaction { get; init; }
    }

    private sealed class PricePointData
    {
        public DateTime Date { get; init; }
        public decimal Price { get; init; }
    }
}
