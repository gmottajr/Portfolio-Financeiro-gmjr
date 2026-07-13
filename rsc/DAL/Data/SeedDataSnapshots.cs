namespace DAL.Data;
public sealed class MarketDataSnapshot { public int Id { get; set; } = 1; public decimal SelicRate { get; set; } public List<MarketIndexSnapshot> Indexes { get; } = []; public List<MarketSectorSnapshot> Sectors { get; } = []; }
public sealed class MarketIndexSnapshot { public int Id { get; set; } public string Code { get; set; } = null!; public decimal CurrentValue { get; set; } public decimal DailyChange { get; set; } public decimal MonthlyChange { get; set; } public decimal YearToDate { get; set; } }
public sealed class MarketSectorSnapshot { public int Id { get; set; } public string Name { get; set; } = null!; public decimal AverageReturn { get; set; } public decimal Volatility { get; set; } public List<MarketSectorAssetSnapshot> Assets { get; } = []; }
public sealed class MarketSectorAssetSnapshot { public int Id { get; set; } public string Symbol { get; set; } = null!; }
public sealed class SeedTestScenarioSnapshot { public int Id { get; set; } public string Name { get; set; } = null!; public string Description { get; set; } = null!; public string PortfolioJson { get; set; } = null!; public string ExpectedResultsJson { get; set; } = null!; }
