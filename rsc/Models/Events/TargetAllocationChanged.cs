using Abstractions._04_Domain;
using SharedKernel.ValueObjects;

namespace Models.Events;

/// <summary>
/// Levantado quando a alocação-alvo de uma posição é alterada
/// independentemente de uma trade de rebalanceamento (ex.: o usuário
/// simplesmente redefine sua estratégia de alocação para um ativo).
/// </summary>
public sealed class TargetAllocationChanged : DomainEventBase
{
    public int PortfolioId { get; }
    public AssetSymbol Symbol { get; }
    public Percentage OldAllocation { get; }
    public Percentage NewAllocation { get; }

    public TargetAllocationChanged(int portfolioId, AssetSymbol symbol, Percentage oldAllocation, Percentage newAllocation)
    {
        PortfolioId = portfolioId;
        Symbol = symbol;
        OldAllocation = oldAllocation;
        NewAllocation = newAllocation;
    }
}
