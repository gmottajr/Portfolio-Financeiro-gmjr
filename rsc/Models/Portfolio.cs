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
/// Aggregate root. Id é atribuído após a construção (o seed não traz um
/// id explícito) -- ver AssignId.
/// </summary>
public sealed class Portfolio : AggregateRoot<int>
{
    public string Name { get; }
    public string UserId { get; }
    public Money TotalInvestment { get; }

    /// <summary>
    /// Data de criação de NEGÓCIO do portfólio (vem do seed). Não usa o
    /// CreatedAt herdado de AuditableEntity porque este último é fixado em
    /// DateTime.UtcNow no momento da construção do objeto em memória --
    /// sem esse campo separado, perderíamos a data real usada no cálculo
    /// de annualizedReturn. Ver observação no início da resposta sobre um
    /// possível ajuste na base class.
    /// </summary>
    public DateTime? PortfolioCreatedAt { get; }

    private readonly List<Position> _positions = new();
    public IReadOnlyList<Position> Positions => _positions.AsReadOnly();

    // Necessário para materialização pelo Entity Framework Core.
    private Portfolio()
    {
        Name = null!;
        UserId = null!;
    }

    public Portfolio(string name, string userId, Money totalInvestment, DateTime? portfolioCreatedAt, IEnumerable<Position> positions)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("Portfolio name is required.");

        if (string.IsNullOrWhiteSpace(userId))
            throw new DomainException("Portfolio userId is required.");

        Name = name;
        UserId = userId;
        TotalInvestment = totalInvestment;
        PortfolioCreatedAt = portfolioCreatedAt;

        var positionList = positions.ToList();
        EnsureNoDuplicateAssets(positionList);
        EnsureTargetAllocationsAreValid(positionList);
        _positions.AddRange(positionList);
    }

    private static void EnsureNoDuplicateAssets(IReadOnlyCollection<Position> positions)
    {
        var duplicated = positions
            .GroupBy(p => p.AssetSymbol)
            .FirstOrDefault(g => g.Count() > 1);

        if (duplicated is not null)
            throw new DomainException($"Portfolio has duplicated positions for asset {duplicated.Key}.");
    }

    private static void EnsureTargetAllocationsAreValid(IReadOnlyCollection<Position> positions)
    {
        if (positions.Count == 0)
            return;

        var total = positions.Sum(position => position.TargetAllocation.Value);
        if (Math.Abs(total - 100m) > 0.0001m)
            throw new BusinessViolationException($"Portfolio target allocations must total 100%. Current total: {total}%.");
    }

    /// <summary>
    /// Atribuído uma única vez pelo processo de carga (DataContext). Ver
    /// observação sobre equalidade de AggregateRoot antes dessa chamada.
    /// </summary>
    public void AssignId(int id)
    {
        if (Id != 0)
            throw new DomainException("Portfolio id has already been assigned.");

        Id = id;
        Raise(new PortfolioCreated(Id, UserId));
    }

    public Position? FindPosition(AssetSymbol assetSymbol) =>
        _positions.FirstOrDefault(p => p.AssetSymbol == assetSymbol);

    /// <summary>
    /// Único ponto de entrada para aplicar uma trade de rebalanceamento:
    /// muta a Position e levanta o domain event pela raiz do agregado.
    /// </summary>
    public void ApplyRebalanceTrade(AssetSymbol assetSymbol, string action, Quantity tradedQuantity, Quantity newQuantity, Money newAveragePrice, DateTime transactionDate)
    {
        var position = FindPosition(assetSymbol)
            ?? throw new DomainException($"Portfolio {Id} has no position for {assetSymbol}.");

        position.ApplyTransaction(newQuantity, newAveragePrice, transactionDate);
        MarkAsUpdated(transactionDate);

        Raise(new PositionRebalanced(Id, assetSymbol, action, tradedQuantity));
    }

    public void ChangeTargetAllocation(AssetSymbol assetSymbol, Percentage newAllocation)
    {
        var position = FindPosition(assetSymbol)
            ?? throw new DomainException($"Portfolio {Id} has no position for {assetSymbol}.");

        var oldAllocation = position.TargetAllocation;
        var newTotal = Positions.Sum(item => item.TargetAllocation.Value) - oldAllocation.Value + newAllocation.Value;
        if (Math.Abs(newTotal - 100m) > 0.0001m)
            throw new BusinessViolationException($"Portfolio target allocations must total 100%. Current total would be: {newTotal}%.");
        position.ChangeTargetAllocation(newAllocation);

        Raise(new TargetAllocationChanged(Id, assetSymbol, oldAllocation, newAllocation));
    }
}
