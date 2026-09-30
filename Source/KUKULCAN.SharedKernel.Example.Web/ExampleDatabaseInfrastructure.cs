using KUKULCAN.SharedKernel.Abstractions;
using KUKULCAN.SharedKernel.Database.Abstractions;
using KUKULCAN.SharedKernel.DomainEvents.Abstractions;

namespace KUKULCAN.SharedKernel.Example.Web;

internal sealed class ExampleTenantContext : ITenantContext
{
    public Guid TenantId { get; } =
        Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
}

internal sealed class ExampleClock : IClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}

internal sealed class ExampleDomainEventDispatcher : IDomainEventDispatcher
{
    public Task DispatchAsync(
        IDomainEvent domainEvent,
        CancellationToken cancellationToken = default)
        => Task.CompletedTask;
}
