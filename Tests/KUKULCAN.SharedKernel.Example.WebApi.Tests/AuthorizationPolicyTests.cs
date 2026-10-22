using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace KUKULCAN.SharedKernel.Example.WebApi.Tests;

[TestFixture]
public sealed class AuthorizationPolicyTests
{
    [Test]
    public async Task DefaultAndFallbackPolicies_RequireAnAuthenticatedReadyAccount()
    {
        await using var factory = new WebApplicationFactory<Program>();
        var provider = factory.Services.GetRequiredService<IAuthorizationPolicyProvider>();

        var defaultPolicy = await provider.GetDefaultPolicyAsync();
        var fallbackPolicy = await provider.GetFallbackPolicyAsync();

        Assert.That(defaultPolicy.Requirements
            .OfType<ClaimsAuthorizationRequirement>()
            .Select(requirement => requirement.ClaimType), Does.Contain("account_ready"));
        Assert.That(fallbackPolicy, Is.Not.Null);
        Assert.That(fallbackPolicy!.Requirements
            .OfType<ClaimsAuthorizationRequirement>()
            .Select(requirement => requirement.ClaimType), Does.Contain("account_ready"));
    }

    [Test]
    public async Task AuthenticatedUserPolicy_AllowsBootstrapPasswordChangeBeforeAccountIsReady()
    {
        await using var factory = new WebApplicationFactory<Program>();
        var provider = factory.Services.GetRequiredService<IAuthorizationPolicyProvider>();

        var policy = await provider.GetPolicyAsync("AuthenticatedUser");

        Assert.That(policy, Is.Not.Null);
        Assert.That(policy!.Requirements
            .OfType<ClaimsAuthorizationRequirement>()
            .Select(requirement => requirement.ClaimType), Does.Not.Contain("account_ready"));
    }
}
