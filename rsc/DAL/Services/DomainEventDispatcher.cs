using Abstractions._02_Application.Services;
using Abstractions._04_Domain;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DAL.Services;

/// <summary>
/// Resolves and invokes all registered handlers for each domain event.
/// </summary>
public sealed class DomainEventDispatcher(
    IServiceProvider serviceProvider,
    ILogger<DomainEventDispatcher> logger) : IDomainEventDispatcher
{
    /// <inheritdoc />
    public async Task DispatchAsync(IEnumerable<IDomainEvent> domainEvents, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(domainEvents);

        foreach (var domainEvent in domainEvents)
        {
            ct.ThrowIfCancellationRequested();
            await DispatchEventAsync(domainEvent, ct);
        }
    }

    private async Task DispatchEventAsync(IDomainEvent domainEvent, CancellationToken ct)
    {
        var eventType = domainEvent.GetType();
        var handlerType = typeof(IDomainEventHandler<>).MakeGenericType(eventType);

        logger.LogDebug("Dispatching domain event {EventType}", eventType.Name);

        foreach (var handler in serviceProvider.GetServices(handlerType))
        {
            if (handler is null)
            {
                continue;
            }

            try
            {
                var handleMethod = handlerType.GetMethod(nameof(IDomainEventHandler<IDomainEvent>.HandleAsync))
                    ?? throw new InvalidOperationException($"Handler {handlerType.Name} does not expose HandleAsync.");

                var task = handleMethod.Invoke(handler, [domainEvent, ct]) as Task
                    ?? throw new InvalidOperationException($"Handler {handler.GetType().Name} did not return a task.");

                await task;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Error handling domain event {EventType}", eventType.Name);
            }
        }
    }
}
