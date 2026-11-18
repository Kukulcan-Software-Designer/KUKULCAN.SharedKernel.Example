using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using KUKULCAN.SharedKernel.Example.Domain.Entities;
using KUKULCAN.SharedKernel.Example.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Testcontainers.PostgreSql;

namespace KUKULCAN.SharedKernel.Example.Infrastructure.IntegrationTests;

[TestFixture]
[NonParallelizable]
public sealed class ClientListingEndpointIntegrationTests
{
    private static readonly string SigningKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
    private const string Issuer = "issue-8-client-listing-tests";
    private const string Audience = "issue-8-client-listing-api";

    [Test]
    public async Task GetClients_WithNameFilter_IsCaseAndAccentInsensitiveAndUsesContainsMatching()
    {
        await using var postgres = new PostgreSqlBuilder("postgres:16-alpine")
            .WithDatabase("example_client_search_integration")
            .WithUsername(TestDatabaseCredentials.GenerateUserName())
            .WithPassword(TestDatabaseCredentials.GeneratePassword())
            .Build();
        await postgres.StartAsync();

        var tenantId = Guid.NewGuid();
        await using var factory = new ClientListingTestApplicationFactory(postgres.GetConnectionString(), tenantId);
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ExampleDbContext>();
            await context.Database.MigrateAsync();
            context.Clients.AddRange(
                new Client { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Álvaro Núñez", City = "Málaga", State = "Andalucía", PostalCode = "00123" },
                new Client { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Alvaro Rios", City = "Sevilla", State = "Andalucía", PostalCode = "41001" },
                new Client { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Beatriz García", City = "Madrid", State = "Madrid", PostalCode = "28001" });
            await context.SaveChangesAsync();
        }

        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", CreateAccessToken(tenantId));
        using var response = await client.GetAsync("/api/example/clients?name=alvaro");

        Assert.That(response.StatusCode, Is.EqualTo(System.Net.HttpStatusCode.OK));
        var payload = await response.Content.ReadFromJsonAsync<JsonElement>();
        var items = payload.GetProperty("items").EnumerateArray().ToArray();
        Assert.Multiple(() =>
        {
            Assert.That(items, Has.Length.EqualTo(2));
            Assert.That(items.Select(item => item.GetProperty("name").GetString()),
                Is.EquivalentTo(new[] { "Álvaro Núñez", "Alvaro Rios" }));
        });
    }

    private static string CreateAccessToken(Guid tenantId)
    {
        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(SigningKey)),
            SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            Issuer,
            Audience,
            [
                new Claim(JwtRegisteredClaimNames.Sub, Guid.NewGuid().ToString()),
                new Claim("tenant_id", tenantId.ToString()),
                new Claim("account_ready", "true")
            ],
            expires: DateTime.UtcNow.AddMinutes(5),
            signingCredentials: credentials);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private sealed class ClientListingTestApplicationFactory(string connectionString, Guid tenantId)
        : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureAppConfiguration((_, configuration) =>
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Kukulcan:Database:Provider"] = "PostgresSql",
                    ["Kukulcan:Database:ConnectionString"] = connectionString,
                    ["Kukulcan:Database:Migration:AutoMigrateOnStartup"] = "false",
                    ["Kukulcan:Database:Migration:SeedDataOnStartup"] = "false",
                    ["Kukulcan:Auth:DefaultTenantId"] = tenantId.ToString(),
                    ["Kukulcan:Auth:Jwt:SigningKey"] = SigningKey,
                    ["Kukulcan:Auth:Jwt:Issuer"] = Issuer,
                    ["Kukulcan:Auth:Jwt:Audience"] = Audience
                }));
        }
    }
}
