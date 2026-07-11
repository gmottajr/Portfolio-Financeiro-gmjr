using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Abstractions._04_Domain;

namespace Models.Events;

/// <summary>
/// Levantado quando um novo Portfolio é registrado no sistema.
/// Útil, por exemplo, para um consumidor de auditoria ou onboarding
/// (ex.: disparar um e-mail de boas-vindas, iniciar métricas de acompanhamento).
/// </summary>
public sealed class PortfolioCreated : DomainEventBase
{
    public int PortfolioId { get; }
    public string UserId { get; }

    public PortfolioCreated(int portfolioId, string userId)
    {
        PortfolioId = portfolioId;
        UserId = userId;
    }
}
