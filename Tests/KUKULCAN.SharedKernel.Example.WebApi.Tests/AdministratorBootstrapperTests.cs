using KUKULCAN.SharedKernel.Auth.Authentication.Local;
using KUKULCAN.SharedKernel.Example.WebApi.Authentication;
using Microsoft.Extensions.Configuration;

namespace KUKULCAN.SharedKernel.Example.WebApi.Tests;

[TestFixture]
public sealed class AdministratorBootstrapperTests
{
    [Test]
    public async Task EnsureAdministratorAsync_WhenRunRepeatedly_CreatesOneAdminWithHashedPasswordAndTenantMembership()
    {
        var bootstrapPassword = $"test-bootstrap-{Guid.NewGuid():N}";
        var tenantId = Guid.NewGuid();
        var email = "  ADMIN@example.test  ";
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Kukulcan:Auth:Administrator:Email"] = email,
                ["Kukulcan:Auth:Bootstrap:Password"] = bootstrapPassword,
                ["Kukulcan:Auth:DefaultTenantId"] = tenantId.ToString()
            })
            .Build();
        var store = new InMemoryAdministratorBootstrapStore();
        var passwordHasher = new PasswordHasher();
        var bootstrapper = new AdministratorBootstrapper(store, passwordHasher, configuration);

        var firstRunCreatedAdministrator =
            await bootstrapper.EnsureAdministratorAsync();
        var secondRunCreatedAdministrator =
            await bootstrapper.EnsureAdministratorAsync();

        Assert.That(firstRunCreatedAdministrator, Is.True);
        Assert.That(secondRunCreatedAdministrator, Is.False);
        Assert.That(store.Administrators, Has.Count.EqualTo(1));

        var administrator = store.Administrators.Single();
        Assert.That(administrator.Email, Is.EqualTo("admin@example.test"));
        Assert.That(administrator.PasswordHash, Is.Not.EqualTo(bootstrapPassword));
        Assert.That(passwordHasher.Verify(bootstrapPassword, administrator.PasswordHash), Is.True);
        Assert.That(administrator.TenantId, Is.EqualTo(tenantId));
    }

    [Test]
    public async Task EnsureAdministratorAsync_WhenBootstrapPasswordIsNotConfigured_DoesNotCreateAdministrator()
    {
        var store = new InMemoryAdministratorBootstrapStore();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>())
            .Build();
        var bootstrapper = new AdministratorBootstrapper(
            store,
            new PasswordHasher(),
            configuration);

        var created = await bootstrapper.EnsureAdministratorAsync();

        Assert.That(created, Is.False);
        Assert.That(store.Administrators, Is.Empty);
    }
}

internal sealed record BootstrapAdministrator(string Email, string PasswordHash, Guid TenantId);

internal sealed class InMemoryAdministratorBootstrapStore : IAdministratorBootstrapStore
{
    public List<BootstrapAdministrator> Administrators { get; } = [];

    public Task<bool> ExistsByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(Administrators.Any(
            administrator => string.Equals(
                administrator.Email,
                email,
                StringComparison.OrdinalIgnoreCase)));
    }

    public Task<bool> TryCreateAsync(
        string email,
        string passwordHash,
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (Administrators.Any(administrator => string.Equals(
                administrator.Email,
                email,
                StringComparison.OrdinalIgnoreCase)))
        {
            return Task.FromResult(false);
        }

        Administrators.Add(new BootstrapAdministrator(email, passwordHash, tenantId));
        return Task.FromResult(true);
    }
}
