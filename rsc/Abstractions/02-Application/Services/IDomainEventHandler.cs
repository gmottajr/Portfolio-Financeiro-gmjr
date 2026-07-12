using Abstractions._04_Domain;

namespace Abstractions._02_Application.Services;

/// <summary>
/// Handles a specific type of domain event.
/// </summary>
/// <typeparam name="TEvent">The event type handled.</typeparam>
public interface IDomainEventHandler<in TEvent>
    where TEvent : IDomainEvent
{
    /// <summary>
    /// Handles a domain event.
    /// </summary>
    Task HandleAsync(TEvent domainEvent, CancellationToken ct = default);
}
