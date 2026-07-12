using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Abstractions._04_Domain;
using Models.Events;
using SharedKernel.Exceptions;
using SharedKernel.ValueObjects;

namespace Models;

/// <summary>
/// Ativo negociado. Aggregate root próprio: sua identidade natural é o
/// Symbol, e price updates são um caso de uso legítimo (fora do escopo dos
/// 3 endpoints atuais, mas correto de modelar aqui).
/// </summary>
public sealed class Asset : AggregateRoot<AssetSymbol>
{
    public AssetSymbol Symbol => Id;
    public string Name { get; }
    public string Type { get; }
    public string Sector { get; }
    public Money CurrentPrice { get; private set; }

    /// <summary>
    /// Reaproveita o UpdatedAt herdado de AuditableEntity em vez de um
    /// campo próprio -- semanticamente é exatamente "quando o preço foi
    /// atualizado pela última vez". Cai para CreatedAt se nunca atualizado.
    /// </summary>
    public DateTime LastUpdated => UpdatedAt ?? CreatedAt;

    private readonly List<PricePoint> _priceHistory = new();
    public IReadOnlyList<PricePoint> PriceHistory => _priceHistory.AsReadOnly();

    // Necessário para materialização pelo Entity Framework Core.
    private Asset()
    {
        Name = null!;
        Type = null!;
        Sector = null!;
    }

    public Asset(AssetSymbol symbol, string name, string type, string sector, Money currentPrice, DateTime lastUpdated)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("Asset name is required.");

        if (string.IsNullOrWhiteSpace(sector))
            throw new DomainException("Asset sector is required.");

        Id = symbol;
        Name = name;
        Type = type;
        Sector = sector;
        CurrentPrice = currentPrice;

        // Inicializa UpdatedAt com a data de carga do seed (não a data real
        // de instanciação em memória, que fica em CreatedAt).
        MarkAsUpdated(lastUpdated);
    }

    public void UpdatePrice(Money newPrice, DateTime asOf)
    {
        if (asOf < LastUpdated)
            throw new DomainException($"Cannot update {Symbol} price with a date earlier than the last update.");

        var oldPrice = CurrentPrice;
        CurrentPrice = newPrice;
        MarkAsUpdated(asOf);

        Raise(new AssetPriceUpdated(Symbol, oldPrice, newPrice));
    }

    /// <summary>Usado pelo mapper de carga do seed. Mantém a lista ordenada por data.</summary>
    public void SetPriceHistory(IEnumerable<PricePoint> history)
    {
        _priceHistory.Clear();
        _priceHistory.AddRange(history.OrderBy(p => p.Date));
    }
}
