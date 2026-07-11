using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SharedKernel.ValueObjects;

namespace Models;

/// <summary>
/// Dados de mercado de referência (Selic, índices, setores).
/// Sem identidade própria — praticamente um snapshot/Value Object composto.
/// </summary>
public sealed class MarketData
{
    /// <summary>Taxa Selic anual, como Percentage (12.00 = 12% a.a.).</summary>
    public Percentage SelicRate { get; }

    private readonly Dictionary<string, IndexPerformance> _indexPerformance;
    public IReadOnlyDictionary<string, IndexPerformance> IndexPerformance => _indexPerformance;

    private readonly List<SectorInfo> _sectors = new();
    public IReadOnlyList<SectorInfo> Sectors => _sectors.AsReadOnly();

    public MarketData(Percentage selicRate, IDictionary<string, IndexPerformance> indexPerformance, IEnumerable<SectorInfo> sectors)
    {
        SelicRate = selicRate;
        _indexPerformance = new Dictionary<string, IndexPerformance>(indexPerformance);
        _sectors.AddRange(sectors);
    }
}

/// <summary>
/// Performance de um índice de mercado (ex.: IBOV). CurrentValue fica como
/// decimal proposital -- não é "dinheiro" no sentido de Money (é um número
/// de pontos do índice, sem a semântica de moeda/arredondamento contábil).
/// </summary>
public sealed record IndexPerformance(decimal CurrentValue, Percentage DailyChange, Percentage MonthlyChange, Percentage YearToDate);

public sealed record SectorInfo(string Name, ReturnRate AverageReturn, Volatility Volatility, IReadOnlyList<AssetSymbol> Assets);