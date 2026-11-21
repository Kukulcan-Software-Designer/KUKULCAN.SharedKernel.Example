using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using KUKULCAN.SharedKernel.Database.Configuration;
using KUKULCAN.SharedKernel.Example.Domain.Entities;
using KUKULCAN.SharedKernel.Example.Infrastructure;
using KUKULCAN.SharedKernel.Example.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace KUKULCAN.SharedKernel.Example.Infrastructure.IntegrationTests;

[TestFixture]
[NonParallelizable]
public sealed class ProdInvoiceEndpointIntegrationTests : ExampleDatabaseTestFixture
{
    private static readonly string SigningKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
    private const string Issuer = "issue-8-prod-invoice-tests";
    private const string Audience = "issue-8-prod-invoice-api";

    [Test]
    public async Task GetProdInvoices_ReturnsPagedSummariesOrderedByIssuedAtAndIdDescending()
    {
        var tenantId = Guid.NewGuid();
        var issuedAt = new DateTimeOffset(2026, 7, 5, 10, 0, 0, TimeSpan.Zero);
        var oldestId = Guid.Parse("00000000-0000-0000-0000-000000000001");
        var newestTieId = Guid.Parse("00000000-0000-0000-0000-000000000003");
        var olderTieId = Guid.Parse("00000000-0000-0000-0000-000000000002");
        await SeedProdInvoicesAsync(
            tenantId,
            new ProdInvoiceSeed(oldestId, "PUR-001", "Álvaro Núñez Suministros", issuedAt.AddDays(-1)),
            new ProdInvoiceSeed(olderTieId, "PUR-002", "Beatriz García Proveedores", issuedAt),
            new ProdInvoiceSeed(newestTieId, "PUR-003", "Álvaro Núñez Suministros", issuedAt));

        await using var factory = new ProdInvoiceTestApplicationFactory(ConnectionString, ProviderName, tenantId);
        using var client = CreateAuthenticatedClient(factory, tenantId);
        using var response = await client.GetAsync("/api/example/prod-invoices");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var payload = await response.Content.ReadFromJsonAsync<JsonElement>();
        var items = payload.GetProperty("items").EnumerateArray().ToArray();

        Assert.Multiple(() =>
        {
            Assert.That(payload.GetProperty("page").GetInt32(), Is.EqualTo(1));
            Assert.That(payload.GetProperty("pageSize").GetInt32(), Is.EqualTo(25));
            Assert.That(payload.GetProperty("totalCount").GetInt32(), Is.EqualTo(3));
            Assert.That(payload.GetProperty("totalPages").GetInt32(), Is.EqualTo(1));
            Assert.That(items.Select(item => item.GetProperty("id").GetGuid()),
                Is.EqualTo(new[] { newestTieId, olderTieId, oldestId }));
            Assert.That(items.All(item => !item.TryGetProperty("lines", out _)), Is.True);
            Assert.That(items.All(item => !item.TryGetProperty("tenantId", out _)), Is.True);
            Assert.That(items[0].GetProperty("providerName").GetString(),
                Is.EqualTo("Álvaro Núñez Suministros"));
            Assert.That(items[0].GetProperty("invoiceNumber").GetString(), Is.EqualTo("PUR-003"));
        });
    }

    [Test]
    public async Task GetProdInvoices_WithProviderAndUtcDateRange_IncludesBothInclusiveBoundaries()
    {
        var tenantId = Guid.NewGuid();
        var lowerBoundary = new DateTimeOffset(2026, 7, 5, 10, 0, 0, TimeSpan.Zero);
        var upperBoundary = lowerBoundary.AddHours(2);
        var seeded = await SeedProdInvoicesAsync(
            tenantId,
            new ProdInvoiceSeed(null, "PUR-LOWER", "Álvaro Núñez Suministros", lowerBoundary),
            new ProdInvoiceSeed(null, "PUR-UPPER", "Álvaro Núñez Suministros", upperBoundary),
            new ProdInvoiceSeed(null, "PUR-AFTER", "Álvaro Núñez Suministros", upperBoundary.AddSeconds(1)),
            new ProdInvoiceSeed(null, "PUR-OTHER-PROVIDER", "Beatriz García Proveedores", lowerBoundary));

        await using var factory = new ProdInvoiceTestApplicationFactory(ConnectionString, ProviderName, tenantId);
        using var client = CreateAuthenticatedClient(factory, tenantId);
        var query =
            $"providerId={seeded.ProviderIds["Álvaro Núñez Suministros"]}" +
            $"&issuedFrom={Uri.EscapeDataString("2026-07-05T12:00:00+02:00")}" +
            $"&issuedTo={Uri.EscapeDataString("2026-07-05T07:00:00-05:00")}";
        using var response = await client.GetAsync($"/api/example/prod-invoices?{query}");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var payload = await response.Content.ReadFromJsonAsync<JsonElement>();
        var items = payload.GetProperty("items").EnumerateArray().ToArray();

        Assert.Multiple(() =>
        {
            Assert.That(items, Has.Length.EqualTo(2));
            Assert.That(items.Select(item => item.GetProperty("invoiceNumber").GetString()),
                Is.EquivalentTo(new[] { "PUR-LOWER", "PUR-UPPER" }));
            Assert.That(payload.GetProperty("totalCount").GetInt32(), Is.EqualTo(2));
        });
    }

    [Test]
    public async Task GetProdInvoices_WithProviderName_UsesContainsMatchingInsensitiveToCaseAndAccents()
    {
        var tenantId = Guid.NewGuid();
        await SeedProdInvoicesAsync(
            tenantId,
            new ProdInvoiceSeed(null, "PUR-ALVARO", "Álvaro Núñez Distribuciones", DateTimeOffset.UtcNow),
            new ProdInvoiceSeed(null, "PUR-OTHER", "Beatriz García Proveedores", DateTimeOffset.UtcNow));

        await using var factory = new ProdInvoiceTestApplicationFactory(ConnectionString, ProviderName, tenantId);
        using var client = CreateAuthenticatedClient(factory, tenantId);
        using var response = await client.GetAsync(
            $"/api/example/prod-invoices?providerName={Uri.EscapeDataString("alvaro nunez")}");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var payload = await response.Content.ReadFromJsonAsync<JsonElement>();
        var items = payload.GetProperty("items").EnumerateArray().ToArray();

        Assert.Multiple(() =>
        {
            Assert.That(items, Has.Length.EqualTo(1));
            Assert.That(items[0].GetProperty("invoiceNumber").GetString(), Is.EqualTo("PUR-ALVARO"));
            Assert.That(items[0].GetProperty("providerName").GetString(),
                Is.EqualTo("Álvaro Núñez Distribuciones"));
        });
    }

    [Test]
    public async Task GetProdInvoices_WithInvoiceNumber_UsesExactCaseAndAccentInsensitiveMatching()
    {
        var tenantId = Guid.NewGuid();
        await SeedProdInvoicesAsync(
            tenantId,
            new ProdInvoiceSeed(null, "FÁCT-001", "Álvaro Núñez Suministros", DateTimeOffset.UtcNow),
            new ProdInvoiceSeed(null, "FACT-001-EXTRA", "Álvaro Núñez Suministros", DateTimeOffset.UtcNow));

        await using var factory = new ProdInvoiceTestApplicationFactory(ConnectionString, ProviderName, tenantId);
        using var client = CreateAuthenticatedClient(factory, tenantId);
        using var response = await client.GetAsync(
            $"/api/example/prod-invoices?invoiceNumber={Uri.EscapeDataString("fact-001")}");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var payload = await response.Content.ReadFromJsonAsync<JsonElement>();
        var items = payload.GetProperty("items").EnumerateArray().ToArray();

        Assert.Multiple(() =>
        {
            Assert.That(items, Has.Length.EqualTo(1));
            Assert.That(items[0].GetProperty("invoiceNumber").GetString(), Is.EqualTo("FÁCT-001"));
        });
    }

    [Test]
    public async Task GetProdInvoices_WithInvoiceNumberAndProviderId_CombinesExactFiltersWithAnd()
    {
        var tenantId = Guid.NewGuid();
        var seeded = await SeedProdInvoicesAsync(
            tenantId,
            new ProdInvoiceSeed(null, "PUR-EXACT-001", "Álvaro Núñez Suministros", DateTimeOffset.UtcNow),
            new ProdInvoiceSeed(null, "PUR-EXACT-001-EXTRA", "Álvaro Núñez Suministros", DateTimeOffset.UtcNow),
            new ProdInvoiceSeed(null, "PUR-EXACT-002", "Beatriz García Proveedores", DateTimeOffset.UtcNow));

        await using var factory = new ProdInvoiceTestApplicationFactory(ConnectionString, ProviderName, tenantId);
        using var client = CreateAuthenticatedClient(factory, tenantId);
        var matchingProviderId = seeded.ProviderIds["Álvaro Núñez Suministros"];
        var differentProviderId = seeded.ProviderIds["Beatriz García Proveedores"];
        var invoiceNumber = Uri.EscapeDataString("PUR-EXACT-001");

        using var matchingResponse = await client.GetAsync(
            $"/api/example/prod-invoices?invoiceNumber={invoiceNumber}&providerId={matchingProviderId}");
        using var nonMatchingResponse = await client.GetAsync(
            $"/api/example/prod-invoices?invoiceNumber={invoiceNumber}&providerId={differentProviderId}");

        Assert.That(matchingResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(nonMatchingResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var matchingPayload = await matchingResponse.Content.ReadFromJsonAsync<JsonElement>();
        var nonMatchingPayload = await nonMatchingResponse.Content.ReadFromJsonAsync<JsonElement>();
        var matchingItems = matchingPayload.GetProperty("items").EnumerateArray().ToArray();
        var nonMatchingItems = nonMatchingPayload.GetProperty("items").EnumerateArray().ToArray();

        Assert.Multiple(() =>
        {
            Assert.That(matchingItems, Has.Length.EqualTo(1));
            Assert.That(matchingItems[0].GetProperty("invoiceNumber").GetString(), Is.EqualTo("PUR-EXACT-001"));
            Assert.That(matchingItems[0].GetProperty("providerId").GetGuid(), Is.EqualTo(matchingProviderId));
            Assert.That(matchingPayload.GetProperty("totalCount").GetInt32(), Is.EqualTo(1));
            Assert.That(nonMatchingItems, Is.Empty);
            Assert.That(nonMatchingPayload.GetProperty("totalCount").GetInt32(), Is.Zero);
        });
    }

    [Test]
    public async Task GetProdInvoices_WithEquivalentUtcAndOffsetDateRanges_ReturnsSameResults()
    {
        var tenantId = Guid.NewGuid();
        var lowerBoundary = new DateTimeOffset(2026, 7, 5, 10, 0, 0, TimeSpan.Zero);
        var upperBoundary = lowerBoundary.AddHours(2);
        var seeded = await SeedProdInvoicesAsync(
            tenantId,
            new ProdInvoiceSeed(null, "PUR-T50-LOWER", "Álvaro Núñez Suministros", lowerBoundary),
            new ProdInvoiceSeed(null, "PUR-T50-MIDDLE", "Álvaro Núñez Suministros", lowerBoundary.AddHours(1)),
            new ProdInvoiceSeed(null, "PUR-T50-UPPER", "Álvaro Núñez Suministros", upperBoundary),
            new ProdInvoiceSeed(null, "PUR-T50-AFTER", "Álvaro Núñez Suministros", upperBoundary.AddSeconds(1)));

        await using var factory = new ProdInvoiceTestApplicationFactory(ConnectionString, ProviderName, tenantId);
        using var client = CreateAuthenticatedClient(factory, tenantId);
        var utcQuery =
            $"providerId={seeded.ProviderIds["Álvaro Núñez Suministros"]}" +
            $"&issuedFrom={Uri.EscapeDataString("2026-07-05T10:00:00Z")}" +
            $"&issuedTo={Uri.EscapeDataString("2026-07-05T12:00:00Z")}";
        var offsetQuery =
            $"providerId={seeded.ProviderIds["Álvaro Núñez Suministros"]}" +
            $"&issuedFrom={Uri.EscapeDataString("2026-07-05T12:00:00+02:00")}" +
            $"&issuedTo={Uri.EscapeDataString("2026-07-05T14:00:00+02:00")}";

        using var utcResponse = await client.GetAsync($"/api/example/prod-invoices?{utcQuery}");
        using var offsetResponse = await client.GetAsync($"/api/example/prod-invoices?{offsetQuery}");

        Assert.That(utcResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(offsetResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var utcPayload = await utcResponse.Content.ReadFromJsonAsync<JsonElement>();
        var offsetPayload = await offsetResponse.Content.ReadFromJsonAsync<JsonElement>();
        var utcIds = utcPayload.GetProperty("items").EnumerateArray()
            .Select(item => item.GetProperty("id").GetGuid()).ToArray();
        var offsetIds = offsetPayload.GetProperty("items").EnumerateArray()
            .Select(item => item.GetProperty("id").GetGuid()).ToArray();

        Assert.Multiple(() =>
        {
            Assert.That(utcIds, Is.EquivalentTo(offsetIds));
            Assert.That(utcIds, Is.EquivalentTo(seeded.Invoices.Select(invoice => invoice.Id).Take(3)));
            Assert.That(utcPayload.GetProperty("totalCount").GetInt32(), Is.EqualTo(3));
            Assert.That(offsetPayload.GetProperty("totalCount").GetInt32(), Is.EqualTo(3));
        });
    }

    [Test]
    public async Task GetProdInvoiceById_ReturnsHeaderAndHistoricalSnapshotsWhenProviderIsDeletedAndProductChanges()
    {
        var tenantId = Guid.NewGuid();
        var issuedAt = new DateTimeOffset(2026, 7, 5, 10, 30, 0, TimeSpan.Zero);
        var seeded = await SeedProdInvoicesAsync(
            tenantId,
            new ProdInvoiceSeed(null, "PUR-HISTORY-001", "Álvaro Núñez Suministros", issuedAt));
        var invoice = seeded.Invoices.Single();

        await using (var context = CreateDbContext(tenantId))
        {
            var provider = await context.Providers.SingleAsync(entity =>
                entity.Id == seeded.ProviderIds["Álvaro Núñez Suministros"]);
            provider.Name = "Renamed live provider";
            provider.IsDeleted = true;
            provider.DeletedOn = DateTimeOffset.UtcNow;

            var firstProduct = await context.Products.SingleAsync(entity =>
                entity.Id == seeded.ProductIds[0]);
            firstProduct.Code = "LIVE-CHANGED-01";
            firstProduct.Name = "Changed live product";

            await context.SaveChangesAsync();
        }

        await using var factory = new ProdInvoiceTestApplicationFactory(ConnectionString, ProviderName, tenantId);
        using var client = CreateAuthenticatedClient(factory, tenantId);
        using var response = await client.GetAsync($"/api/example/prod-invoices/{invoice.Id}");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var payload = await response.Content.ReadFromJsonAsync<JsonElement>();
        var lines = payload.GetProperty("lines").EnumerateArray().ToArray();

        Assert.Multiple(() =>
        {
            Assert.That(payload.GetProperty("id").GetGuid(), Is.EqualTo(invoice.Id));
            Assert.That(payload.GetProperty("invoiceNumber").GetString(), Is.EqualTo("PUR-HISTORY-001"));
            Assert.That(DateTimeOffset.Parse(payload.GetProperty("issuedAt").GetString()!).ToUniversalTime(),
                Is.EqualTo(issuedAt));
            Assert.That(payload.GetProperty("providerId").GetGuid(),
                Is.EqualTo(seeded.ProviderIds["Álvaro Núñez Suministros"]));
            Assert.That(payload.GetProperty("providerName").GetString(),
                Is.EqualTo("Álvaro Núñez Suministros"));
            Assert.That(lines, Has.Length.EqualTo(2));
            Assert.That(lines[0].GetProperty("lineNumber").GetInt32(), Is.EqualTo(1));
            Assert.That(lines[0].GetProperty("productId").GetGuid(), Is.EqualTo(seeded.ProductIds[0]));
            Assert.That(lines[0].GetProperty("productCode").GetString(), Is.EqualTo("HIST-001"));
            Assert.That(lines[0].GetProperty("productName").GetString(), Is.EqualTo("Historical product one"));
            Assert.That(lines[0].GetProperty("quantity").GetDecimal(), Is.EqualTo(10m));
            Assert.That(lines[0].GetProperty("unitPrice").GetDecimal(), Is.EqualTo(12.50m));
            Assert.That(lines[1].GetProperty("lineNumber").GetInt32(), Is.EqualTo(2));
            Assert.That(lines[1].GetProperty("productCode").GetString(), Is.EqualTo("HIST-002"));
            Assert.That(lines[1].GetProperty("productName").GetString(), Is.EqualTo("Historical product two"));
            Assert.That(payload.TryGetProperty("tenantId", out _), Is.False);
            Assert.That(lines.All(line => !line.TryGetProperty("tenantId", out _)), Is.True);
        });
    }

    [Test]
    public async Task GetProdInvoiceById_ReturnsNotFoundForUnknownAndOtherTenantIds()
    {
        var tenantId = Guid.NewGuid();
        var own = await SeedProdInvoicesAsync(
            tenantId,
            new ProdInvoiceSeed(null, "PUR-OWN", "Álvaro Núñez Suministros", DateTimeOffset.UtcNow));
        var foreign = await SeedProdInvoicesAsync(
            Guid.NewGuid(),
            new ProdInvoiceSeed(null, "PUR-FOREIGN", "Foreign provider", DateTimeOffset.UtcNow));

        await using var factory = new ProdInvoiceTestApplicationFactory(ConnectionString, ProviderName, tenantId);
        using var client = CreateAuthenticatedClient(factory, tenantId);
        using var unknownResponse = await client.GetAsync($"/api/example/prod-invoices/{Guid.NewGuid()}");
        using var foreignResponse = await client.GetAsync(
            $"/api/example/prod-invoices/{foreign.Invoices.Single().Id}");
        using var ownResponse = await client.GetAsync(
            $"/api/example/prod-invoices/{own.Invoices.Single().Id}");

        Assert.Multiple(() =>
        {
            Assert.That(unknownResponse.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
            Assert.That(foreignResponse.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
            Assert.That(ownResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        });
    }

    [Test]
    public async Task GetProdInvoices_RejectsDateFiltersWithoutAnExplicitOffset()
    {
        var tenantId = Guid.NewGuid();
        await SeedProdInvoicesAsync(
            tenantId,
            new ProdInvoiceSeed(null, "PUR-DATE", "Álvaro Núñez Suministros", DateTimeOffset.UtcNow));

        await using var factory = new ProdInvoiceTestApplicationFactory(ConnectionString, ProviderName, tenantId);
        using var client = CreateAuthenticatedClient(factory, tenantId);
        using var response = await client.GetAsync(
            "/api/example/prod-invoices?issuedFrom=2026-07-05T10%3A00%3A00");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [Test]
    public async Task GetProdInvoices_RejectsIssuedFromLaterThanIssuedToAfterUtcNormalization()
    {
        var tenantId = Guid.NewGuid();
        await SeedProdInvoicesAsync(
            tenantId,
            new ProdInvoiceSeed(null, "PUR-RANGE", "Álvaro Núñez Suministros", DateTimeOffset.UtcNow));

        await using var factory = new ProdInvoiceTestApplicationFactory(ConnectionString, ProviderName, tenantId);
        using var client = CreateAuthenticatedClient(factory, tenantId);
        var query =
            $"issuedFrom={Uri.EscapeDataString("2026-07-05T12:00:00+02:00")}" +
            $"&issuedTo={Uri.EscapeDataString("2026-07-05T09:59:59Z")}";
        using var response = await client.GetAsync($"/api/example/prod-invoices?{query}");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [Test]
    public async Task GetProdInvoices_RejectsUnknownQueryParametersAndUnsupportedSortFields()
    {
        var tenantId = Guid.NewGuid();
        await SeedProdInvoicesAsync(
            tenantId,
            new ProdInvoiceSeed(null, "PUR-QUERY", "Álvaro Núñez Suministros", DateTimeOffset.UtcNow));

        await using var factory = new ProdInvoiceTestApplicationFactory(ConnectionString, ProviderName, tenantId);
        using var client = CreateAuthenticatedClient(factory, tenantId);
        using var unknownParameterResponse = await client.GetAsync(
            "/api/example/prod-invoices?tenantId=" + Guid.NewGuid());
        using var unsupportedSortResponse = await client.GetAsync(
            "/api/example/prod-invoices?sortBy=tenantId&sortDirection=desc");
        using var invalidDirectionResponse = await client.GetAsync(
            "/api/example/prod-invoices?sortBy=issuedAt&sortDirection=sideways");

        Assert.Multiple(() =>
        {
            Assert.That(unknownParameterResponse.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
            Assert.That(unsupportedSortResponse.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
            Assert.That(invalidDirectionResponse.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        });
    }

    [Test]
    public async Task GetProdInvoices_RequiresAuthenticatedUserAndReadyAccount()
    {
        var tenantId = Guid.NewGuid();
        await SeedProdInvoicesAsync(
            tenantId,
            new ProdInvoiceSeed(null, "PUR-SECURITY", "Álvaro Núñez Suministros", DateTimeOffset.UtcNow));

        await using var factory = new ProdInvoiceTestApplicationFactory(ConnectionString, ProviderName, tenantId);
        using var anonymousClient = factory.CreateClient();
        using var anonymousResponse = await anonymousClient.GetAsync("/api/example/prod-invoices");

        using var notReadyClient = CreateAuthenticatedClient(factory, tenantId, accountReady: false);
        using var notReadyResponse = await notReadyClient.GetAsync("/api/example/prod-invoices");

        Assert.Multiple(() =>
        {
            Assert.That(anonymousResponse.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
            Assert.That(notReadyResponse.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
        });
    }

    private async Task<SeededProdInvoices> SeedProdInvoicesAsync(
        Guid tenantId,
        params ProdInvoiceSeed[] invoiceSeeds)
    {
        if (invoiceSeeds.Length == 0)
        {
            throw new ArgumentException("At least one purchase invoice seed is required.", nameof(invoiceSeeds));
        }

        var providers = invoiceSeeds
            .Select(seed => seed.ProviderName)
            .Distinct(StringComparer.Ordinal)
            .ToDictionary(
                name => name,
                name => new Provider
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    Name = name,
                    City = "Madrid",
                    State = "Madrid",
                    PostalCode = "28001"
                },
                StringComparer.Ordinal);

        var products = new[]
        {
            new Product
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Code = "HIST-001",
                Name = "Historical product one",
                Description = "Purchase invoice endpoint test product one"
            },
            new Product
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Code = "HIST-002",
                Name = "Historical product two",
                Description = "Purchase invoice endpoint test product two"
            }
        };

        var invoices = new List<ProdInvoice>();
        var lines = new List<ProdInvoiceLine>();
        foreach (var seed in invoiceSeeds)
        {
            var invoice = new ProdInvoice
            {
                Id = seed.Id ?? Guid.NewGuid(),
                TenantId = tenantId,
                ProviderId = providers[seed.ProviderName].Id,
                InvoiceNumber = seed.InvoiceNumber,
                IssuedAt = seed.IssuedAt,
                ProviderName = seed.ProviderName
            };
            invoices.Add(invoice);
            lines.AddRange(
            [
                new ProdInvoiceLine
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    ProdInvoiceId = invoice.Id,
                    LineNumber = 1,
                    ProductId = products[0].Id,
                    ProductCode = products[0].Code,
                    ProductName = products[0].Name,
                    Quantity = 10m,
                    UnitPrice = 12.50m
                },
                new ProdInvoiceLine
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    ProdInvoiceId = invoice.Id,
                    LineNumber = 2,
                    ProductId = products[1].Id,
                    ProductCode = products[1].Code,
                    ProductName = products[1].Name,
                    Quantity = 5m,
                    UnitPrice = 7.25m
                }
            ]);
        }

        await using var context = CreateDbContext(tenantId);
        context.Providers.AddRange(providers.Values);
        context.Products.AddRange(products);
        context.ProdInvoices.AddRange(invoices);
        context.ProdInvoiceLines.AddRange(lines);
        await context.SaveChangesAsync();

        return new SeededProdInvoices(
            providers.ToDictionary(pair => pair.Key, pair => pair.Value.Id, StringComparer.Ordinal),
            products.Select(product => product.Id).ToArray(),
            invoices);
    }

    private static HttpClient CreateAuthenticatedClient(
        WebApplicationFactory<Program> factory,
        Guid tenantId,
        bool accountReady = true)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", CreateAccessToken(tenantId, accountReady));
        return client;
    }

    private static string CreateAccessToken(Guid tenantId, bool accountReady)
    {
        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(SigningKey)),
            SecurityAlgorithms.HmacSha256);
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, Guid.NewGuid().ToString()),
            new("tenant_id", tenantId.ToString())
        };
        if (accountReady)
        {
            claims.Add(new Claim("account_ready", "true"));
        }

        var token = new JwtSecurityToken(
            Issuer,
            Audience,
            claims,
            expires: DateTime.UtcNow.AddMinutes(5),
            signingCredentials: credentials);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private sealed record ProdInvoiceSeed(
        Guid? Id,
        string InvoiceNumber,
        string ProviderName,
        DateTimeOffset IssuedAt);

    private sealed record SeededProdInvoices(
        IReadOnlyDictionary<string, Guid> ProviderIds,
        IReadOnlyList<Guid> ProductIds,
        IReadOnlyList<ProdInvoice> Invoices);

    private sealed class ProdInvoiceTestApplicationFactory(
        string connectionString,
        string providerName,
        Guid tenantId)
        : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            var settings = new Dictionary<string, string?>
            {
                [$"{KukulcanDatabaseOptions.SectionKey}:Provider"] = providerName,
                [$"{KukulcanDatabaseOptions.SectionKey}:ConnectionString"] = connectionString,
                [$"{KukulcanDatabaseOptions.SectionKey}:Migration:AutoMigrateOnStartup"] = "false",
                [$"{KukulcanDatabaseOptions.SectionKey}:Migration:SeedDataOnStartup"] = "false",
                ["Kukulcan:Auth:DefaultTenantId"] = tenantId.ToString(),
                ["Kukulcan:Auth:Jwt:SigningKey"] = SigningKey,
                ["Kukulcan:Auth:Jwt:Issuer"] = Issuer,
                ["Kukulcan:Auth:Jwt:Audience"] = Audience
            };
            IConfiguration testConfiguration = new ConfigurationBuilder()
                .AddInMemoryCollection(settings)
                .Build();

            builder.ConfigureAppConfiguration((_, configuration) =>
                configuration.AddConfiguration(testConfiguration));
            builder.ConfigureServices(services =>
                services.AddExampleDatabase(testConfiguration));
        }
    }
}
