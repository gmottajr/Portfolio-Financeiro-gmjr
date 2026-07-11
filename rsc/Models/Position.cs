using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Abstractions._04_Domain;
using SharedKernel.Exceptions;
using SharedKernel.ValueObjects;

namespace Models;

/// <summary>
/// Entidade filha dentro do agregado Portfolio. 
/// tem Id próprio nas base classes fornecidas, então sua identidade
/// dentro da coleção de posições continua sendo o AssetSymbol (chave
/// natural, já garantida única pelo Portfolio).
/// CreatedAt/UpdatedAt (herdados) rastreiam o registro no sistema; já
/// LastTransaction continua sendo a data de negócio da última operação.
/// </summary>
public sealed class Position : AuditableEntity
{
    public AssetSymbol AssetSymbol { get; }
    public Quantity Quantity { get; private set; }
    public Money AveragePrice { get; private set; }
    public Percentage TargetAllocation { get; private set; }
    public DateTime? LastTransaction { get; private set; }

    public Position(
        AssetSymbol assetSymbol,
        Quantity quantity,
        Money averagePrice,
        Percentage targetAllocation,
        DateTime? lastTransaction = null)
    {
        if (targetAllocation.Value is < 0m or > 100m)
            throw new DomainException($"Target allocation for {assetSymbol} must be between 0% and 100%.");

        AssetSymbol = assetSymbol;
        Quantity = quantity;
        AveragePrice = averagePrice;
        TargetAllocation = targetAllocation;
        LastTransaction = lastTransaction;
    }

    public Money InvestedAmount => AveragePrice * Quantity.Value;

    public Money CurrentValue(Money currentPrice) => currentPrice * Quantity.Value;

    /// <summary>
    /// Aplica o efeito de uma transação de rebalanceamento. Chamado apenas
    /// por Portfolio.ApplyRebalanceTrade -- o evento de domínio é levantado
    /// lá, não aqui (Position não tem acesso a Raise fora de si mesma, e
    /// eventos de agregado devem sair pela raiz).
    /// </summary>
    internal void ApplyTransaction(Quantity newQuantity, Money newAveragePrice, DateTime transactionDate)
    {
        Quantity = newQuantity;
        AveragePrice = newAveragePrice;
        LastTransaction = transactionDate;
        MarkAsUpdated(transactionDate);
    }

    internal void ChangeTargetAllocation(Percentage newAllocation)
    {
        if (newAllocation.Value is < 0m or > 100m)
            throw new DomainException($"Target allocation for {AssetSymbol} must be between 0% and 100%.");

        TargetAllocation = newAllocation;
        MarkAsUpdated();
    }
}