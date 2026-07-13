using Abstractions._02_Application.Services;
using DAL.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Models.Events;

namespace Persistence.Tests;

public sealed class DomainEventDispatcherTests
{
    [Fact]
    public async Task DispatchAsync_InvokesRegisteredHandler()
    {
        var handler = new RecordingHandler();
        var services = new ServiceCollection()
            .AddSingleton<IDomainEventHandler<PortfolioCreated>>(handler)
            .BuildServiceProvider();
        var dispatcher = new DomainEventDispatcher(services, NullLogger<DomainEventDispatcher>.Instance);
        var domainEvent = new PortfolioCreated(7, "user-001");

        await dispatcher.DispatchAsync([domainEvent]);

        Assert.Same(domainEvent, handler.HandledEvent);
    }

    [Fact]
    public async Task DispatchAsync_ContinuesWhenAHandlerThrows()
    {
        var succeedingHandler = new RecordingHandler();
        var services = new ServiceCollection()
            .AddSingleton<IDomainEventHandler<PortfolioCreated>, ThrowingHandler>()
            .AddSingleton<IDomainEventHandler<PortfolioCreated>>(succeedingHandler)
            .BuildServiceProvider();
        var dispatcher = new DomainEventDispatcher(services, NullLogger<DomainEventDispatcher>.Instance);
        var domainEvent = new PortfolioCreated(7, "user-001");

        await dispatcher.DispatchAsync([domainEvent]);

        Assert.Same(domainEvent, succeedingHandler.HandledEvent);
    }

    private sealed class RecordingHandler : IDomainEventHandler<PortfolioCreated>
    {
        public PortfolioCreated? HandledEvent { get; private set; }

        public Task HandleAsync(PortfolioCreated domainEvent, CancellationToken ct = default)
        {
            HandledEvent = domainEvent;
            return Task.CompletedTask;
        }
    }

    private sealed class ThrowingHandler : IDomainEventHandler<PortfolioCreated>
    {
        public Task HandleAsync(PortfolioCreated domainEvent, CancellationToken ct = default) =>
            Task.FromException(new InvalidOperationException("handler failure"));
    }
}
