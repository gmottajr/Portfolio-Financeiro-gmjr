using Abstractions._04_Domain;
using SharedKernel.Enums;
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
    public TradeActionEnum Action { get; }
    public Quantity TradedQuantity { get; }

    public PositionRebalanced(int portfolioId, AssetSymbol symbol, TradeActionEnum action, Quantity tradedQuantity)
    {
        PortfolioId = portfolioId;
        Symbol = symbol;
        Action = action;
        TradedQuantity = tradedQuantity;
    }
}
