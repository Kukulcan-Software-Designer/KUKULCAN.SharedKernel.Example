using System.Security.Claims;
using KUKULCAN.SharedKernel.Example.WebApi.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;

namespace KUKULCAN.SharedKernel.Example.WebApi.Tests;

[TestFixture]
public sealed class TenantContextSecurityTests
{
    [TestCase(null)]
    [TestCase("not-a-guid")]
    [TestCase("00000000-0000-0000-0000-000000000000")]
    public void TenantId_WhenAuthenticatedPrincipalHasNoValidTenantClaim_FailsClosed(string? tenantClaim)
    {
        var defaultTenantId = Guid.NewGuid();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Kukulcan:Auth:DefaultTenantId"] = defaultTenantId.ToString()
            })
            .Build();
        var claims = new List<Claim>();
        if (tenantClaim is not null)
        {
            claims.Add(new Claim("tenant_id", tenantClaim));
        }

        var context = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(claims, "test-authentication"))
        };
        var accessor = new HttpContextAccessor { HttpContext = context };
        var tenantContext = new HttpTenantContext(accessor, configuration);

        Assert.That(tenantContext.TenantId, Is.EqualTo(Guid.Empty));
    }

    [Test]
    public void TenantId_WhenAuthenticatedPrincipalHasValidTenantClaim_UsesClaimRatherThanConfiguredDefault()
    {
        var configuredTenantId = Guid.NewGuid();
        var claimedTenantId = Guid.NewGuid();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Kukulcan:Auth:DefaultTenantId"] = configuredTenantId.ToString()
            })
            .Build();
        var context = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim("tenant_id", claimedTenantId.ToString())],
                "test-authentication"))
        };
        var tenantContext = new HttpTenantContext(
            new HttpContextAccessor { HttpContext = context },
            configuration);

        Assert.That(tenantContext.TenantId, Is.EqualTo(claimedTenantId));
    }
}
