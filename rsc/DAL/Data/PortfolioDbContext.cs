using Microsoft.EntityFrameworkCore;
using Models;
using SharedKernel.ValueObjects;

namespace DAL.Data;

/// <summary>
/// Contexto de persistência dos agregados de portfólio e ativos.
/// MarketData é um snapshot de referência e será disponibilizado pelo
/// carregador de seed, não como uma entidade persistida.
/// </summary>
public sealed class PortfolioDbContext(DbContextOptions<PortfolioDbContext> options) : DbContext(options)
{
    public DbSet<Asset> Assets => Set<Asset>();
    public DbSet<Portfolio> Portfolios => Set<Portfolio>();
    public DbSet<MarketDataSnapshot> MarketDataSnapshots => Set<MarketDataSnapshot>();
    public DbSet<SeedTestScenarioSnapshot> SeedTestScenarios => Set<SeedTestScenarioSnapshot>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ConfigureAssets(modelBuilder);
        ConfigurePortfolios(modelBuilder);
        modelBuilder.Entity<MarketDataSnapshot>().OwnsMany(x => x.Indexes);
        modelBuilder.Entity<MarketDataSnapshot>().OwnsMany(x => x.Sectors, sector => sector.OwnsMany(x => x.Assets));
        modelBuilder.Entity<SeedTestScenarioSnapshot>().HasIndex(x => x.Name).IsUnique();
    }

    private static void ConfigureAssets(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Asset>(asset =>
        {
            asset.HasKey(x => x.Id);
            asset.Property(x => x.Id)
                .HasConversion(symbol => symbol.Value, value => new AssetSymbol(value))
                .HasMaxLength(6);
            asset.Property(x => x.Name).IsRequired();
            asset.Property(x => x.Type)
                .HasConversion<string>()
                .IsRequired();
            asset.Property(x => x.Sector).IsRequired();
            asset.Property(x => x.CurrentPrice)
                .HasConversion(money => money.Value, value => new Money(value));

            asset.OwnsMany(x => x.PriceHistory, history =>
            {
                history.Property<int>("Id");
                history.HasKey("Id");
                history.Property<AssetSymbol>("AssetId")
                    .HasConversion(symbol => symbol.Value, value => new AssetSymbol(value));
                history.WithOwner().HasForeignKey("AssetId");
                history.Property(x => x.Date).IsRequired();
                history.Property(x => x.Price)
                    .HasConversion(money => money.Value, value => new Money(value));
            });

            asset.Navigation(x => x.PriceHistory)
                .HasField("_priceHistory")
                .UsePropertyAccessMode(PropertyAccessMode.Field);
        });
    }

    private static void ConfigurePortfolios(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Portfolio>(portfolio =>
        {
            portfolio.HasKey(x => x.Id);
            portfolio.Property(x => x.Name).IsRequired();
            portfolio.Property(x => x.UserId).IsRequired();
            portfolio.Property(x => x.TotalInvestment)
                .HasConversion(money => money.Value, value => new Money(value));
            portfolio.Property(x => x.PortfolioCreatedAt);

            portfolio.OwnsMany(x => x.Positions, position =>
            {
                position.Property<int>("Id");
                position.HasKey("Id");
                position.WithOwner().HasForeignKey("PortfolioId");
                position.Property(x => x.AssetSymbol)
                    .HasConversion(symbol => symbol.Value, value => new AssetSymbol(value))
                    .HasMaxLength(6);
                position.Property(x => x.Quantity)
                    .HasConversion(quantity => quantity.Value, value => new Quantity(value));
                position.Property(x => x.AveragePrice)
                    .HasConversion(money => money.Value, value => new Money(value));
                position.Property(x => x.TargetAllocation)
                    .HasConversion(percentage => percentage.Value, value => new Percentage(value));
            });

            portfolio.Navigation(x => x.Positions)
                .HasField("_positions")
                .UsePropertyAccessMode(PropertyAccessMode.Field);
        });
    }
}
