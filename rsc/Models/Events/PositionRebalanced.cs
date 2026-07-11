using Abstractions._04_Domain;
using SharedKernel.ValueObjects;

namespace Models.Events;

/// <summary>
/// Levantado pelo agregado Portfolio (não pela Position) ao aplicar uma
/// sugestão de rebalanceamento -- domain events são responsabilidade do
/// aggregate root, não de entidades filhas.
/// </summary>
public sealed class PositionRebalanced : DomainEventBase
{
    public int PortfolioId { get; }
    public AssetSymbol Symbol { get; }
    public string Action { get; } // "BUY" ou "SELL"
    public Quantity TradedQuantity { get; }

    public PositionRebalanced(int portfolioId, AssetSymbol symbol, string action, Quantity tradedQuantity)
    {
        PortfolioId = portfolioId;
        Symbol = symbol;
        Action = action;
        TradedQuantity = tradedQuantity;
    }
}
