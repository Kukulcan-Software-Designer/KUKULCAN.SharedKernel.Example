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
public sealed class InvoiceEndpointIntegrationTests : ExampleDatabaseTestFixture
{
    private static readonly string SigningKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
    private const string Issuer = "issue-8-invoice-tests";
    private const string Audience = "issue-8-invoice-api";

    [Test]
    public async Task GetInvoices_ReturnsPagedSummariesOrderedByIssuedAtAndIdDescending()
    {
        var tenantId = Guid.NewGuid();
        var issuedAt = new DateTimeOffset(2026, 7, 5, 10, 0, 0, TimeSpan.Zero);
        var oldestId = Guid.Parse("00000000-0000-0000-0000-000000000001");
        var newestTieId = Guid.Parse("00000000-0000-0000-0000-000000000003");
        var olderTieId = Guid.Parse("00000000-0000-0000-0000-000000000002");
        var seeded = await SeedInvoicesAsync(
            tenantId,
            new InvoiceSeed(oldestId, "SALE-001", "Álvaro Núñez", issuedAt.AddDays(-1)),
            new InvoiceSeed(olderTieId, "SALE-002", "Beatriz García", issuedAt),
            new InvoiceSeed(newestTieId, "SALE-003", "Álvaro Núñez", issuedAt));

        await using var factory = new InvoiceTestApplicationFactory(ConnectionString, ProviderName, tenantId);
        using var client = CreateAuthenticatedClient(factory, tenantId);
        using var response = await client.GetAsync("/api/example/invoices");

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
            Assert.That(items[0].GetProperty("clientName").GetString(), Is.EqualTo("Álvaro Núñez"));
            Assert.That(items[0].GetProperty("invoiceNumber").GetString(), Is.EqualTo("SALE-003"));
        });
    }


    [TestCase("invoiceNumber", "asc")]
    [TestCase("invoiceNumber", "desc")]
    [TestCase("issuedAt", "asc")]
    [TestCase("issuedAt", "desc")]
    [TestCase("clientName", "asc")]
    [TestCase("clientName", "desc")]
    public async Task GetInvoices_WithAllowedSortField_OrdersInRequestedDirectionWithStableIdTieBreaker(
        string sortBy, string sortDirection)
    {
        var tenantId = Guid.NewGuid();
        var firstId = Guid.NewGuid();
        var secondId = Guid.NewGuid();
        var thirdId = Guid.NewGuid();
        var issuedAt = new DateTimeOffset(2026, 7, 5, 10, 0, 0, TimeSpan.Zero);
        var seeded = await SeedInvoicesAsync(
            tenantId,
            new InvoiceSeed(firstId, "SORT-SALE-B", "Bravo Client", issuedAt.AddDays(-1)),
            new InvoiceSeed(secondId, "SORT-SALE-A", "Alpha Client", issuedAt),
            new InvoiceSeed(thirdId, "SORT-SALE-C", "Alpha Client", issuedAt));

        await using var factory = new InvoiceTestApplicationFactory(ConnectionString, ProviderName, tenantId);
        using var client = CreateAuthenticatedClient(factory, tenantId);
        using var response = await client.GetAsync(
            $"/api/example/invoices?sortBy={sortBy}&sortDirection={sortDirection}");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var payload = await response.Content.ReadFromJsonAsync<JsonElement>();
        var actualIds = payload.GetProperty("items").EnumerateArray()
            .Select(item => item.GetProperty("id").GetGuid()).ToArray();
        var comparer = Comparer<IComparable>.Default;
        IOrderedEnumerable<Invoice> ordered = string.Equals(
            sortDirection, "desc", StringComparison.OrdinalIgnoreCase)
            ? seeded.Invoices.OrderByDescending(invoice => SortValue(invoice, sortBy), comparer)
            : seeded.Invoices.OrderBy(invoice => SortValue(invoice, sortBy), comparer);
        var expected = (string.Equals(sortDirection, "desc", StringComparison.OrdinalIgnoreCase)
                ? ordered.ThenByDescending(invoice => invoice.Id)
                : ordered.ThenBy(invoice => invoice.Id))
            .Select(invoice => invoice.Id);

        Assert.That(actualIds, Is.EqualTo(expected));
    }

    [Test]
    public async Task GetInvoices_WithClientAndUtcDateRange_IncludesBothInclusiveBoundaries()
    {
        var tenantId = Guid.NewGuid();
        var lowerBoundary = new DateTimeOffset(2026, 7, 5, 10, 0, 0, TimeSpan.Zero);
        var upperBoundary = lowerBoundary.AddHours(2);
        var seeded = await SeedInvoicesAsync(
            tenantId,
            new InvoiceSeed(null, "SALE-LOWER", "Álvaro Núñez", lowerBoundary),
            new InvoiceSeed(null, "SALE-UPPER", "Álvaro Núñez", upperBoundary),
            new InvoiceSeed(null, "SALE-AFTER", "Álvaro Núñez", upperBoundary.AddSeconds(1)),
            new InvoiceSeed(null, "SALE-OTHER-CLIENT", "Beatriz García", lowerBoundary));

        await using var factory = new InvoiceTestApplicationFactory(ConnectionString, ProviderName, tenantId);
        using var client = CreateAuthenticatedClient(factory, tenantId);
        var query =
            $"clientId={seeded.ClientIds["Álvaro Núñez"]}" +
            $"&issuedFrom={Uri.EscapeDataString("2026-07-05T12:00:00+02:00")}" +
            $"&issuedTo={Uri.EscapeDataString("2026-07-05T07:00:00-05:00")}";
        using var response = await client.GetAsync($"/api/example/invoices?{query}");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var payload = await response.Content.ReadFromJsonAsync<JsonElement>();
        var items = payload.GetProperty("items").EnumerateArray().ToArray();

        Assert.Multiple(() =>
        {
            Assert.That(items, Has.Length.EqualTo(2));
            Assert.That(items.Select(item => item.GetProperty("invoiceNumber").GetString()),
                Is.EquivalentTo(new[] { "SALE-LOWER", "SALE-UPPER" }));
            Assert.That(payload.GetProperty("totalCount").GetInt32(), Is.EqualTo(2));
        });
    }

    [Test]
    public async Task GetInvoices_WithClientAndIssuedFromOnly_IncludesInclusiveUtcLowerBoundary()
    {
        var tenantId = Guid.NewGuid();
        var issuedFrom = new DateTimeOffset(2026, 7, 5, 10, 0, 0, TimeSpan.Zero);
        var seeded = await SeedInvoicesAsync(
            tenantId,
            new InvoiceSeed(null, "SALE-T43-LOWER", "Álvaro Núñez", issuedFrom),
            new InvoiceSeed(null, "SALE-T43-AFTER", "Álvaro Núñez", issuedFrom.AddHours(1)),
            new InvoiceSeed(null, "SALE-T43-BEFORE", "Álvaro Núñez", issuedFrom.AddSeconds(-1)),
            new InvoiceSeed(null, "SALE-T43-OTHER-CLIENT", "Beatriz García", issuedFrom.AddHours(1)));

        await using var factory = new InvoiceTestApplicationFactory(ConnectionString, ProviderName, tenantId);
        using var client = CreateAuthenticatedClient(factory, tenantId);
        var query =
            $"clientId={seeded.ClientIds["Álvaro Núñez"]}" +
            $"&issuedFrom={Uri.EscapeDataString("2026-07-05T12:00:00+02:00")}";
        using var response = await client.GetAsync($"/api/example/invoices?{query}");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var payload = await response.Content.ReadFromJsonAsync<JsonElement>();
        var items = payload.GetProperty("items").EnumerateArray().ToArray();

        Assert.Multiple(() =>
        {
            Assert.That(items, Has.Length.EqualTo(2));
            Assert.That(items.Select(item => item.GetProperty("invoiceNumber").GetString()),
                Is.EquivalentTo(new[] { "SALE-T43-LOWER", "SALE-T43-AFTER" }));
            Assert.That(payload.GetProperty("totalCount").GetInt32(), Is.EqualTo(2));
        });
    }

    [Test]
    public async Task GetInvoices_WithInvoiceNumber_UsesExactCaseAndAccentInsensitiveMatching()
    {
        var tenantId = Guid.NewGuid();
        await SeedInvoicesAsync(
            tenantId,
            new InvoiceSeed(null, "FÁCT-001", "Álvaro Núñez", DateTimeOffset.UtcNow),
            new InvoiceSeed(null, "FACT-001-EXTRA", "Álvaro Núñez", DateTimeOffset.UtcNow));

        await using var factory = new InvoiceTestApplicationFactory(ConnectionString, ProviderName, tenantId);
        using var client = CreateAuthenticatedClient(factory, tenantId);
        using var response = await client.GetAsync(
            $"/api/example/invoices?invoiceNumber={Uri.EscapeDataString("fact-001")}");

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
    public async Task GetInvoices_WithClientName_UsesContainsMatchingInsensitiveToCaseAndAccents()
    {
        var tenantId = Guid.NewGuid();
        await SeedInvoicesAsync(
            tenantId,
            new InvoiceSeed(null, "SALE-ALVARO", "Álvaro Núñez Distribuciones", DateTimeOffset.UtcNow),
            new InvoiceSeed(null, "SALE-OTHER", "Beatriz García", DateTimeOffset.UtcNow));

        await using var factory = new InvoiceTestApplicationFactory(ConnectionString, ProviderName, tenantId);
        using var client = CreateAuthenticatedClient(factory, tenantId);
        using var response = await client.GetAsync(
            $"/api/example/invoices?clientName={Uri.EscapeDataString("alvaro nunez")}");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var payload = await response.Content.ReadFromJsonAsync<JsonElement>();
        var items = payload.GetProperty("items").EnumerateArray().ToArray();

        Assert.Multiple(() =>
        {
            Assert.That(items, Has.Length.EqualTo(1));
            Assert.That(items[0].GetProperty("invoiceNumber").GetString(), Is.EqualTo("SALE-ALVARO"));
            Assert.That(items[0].GetProperty("clientName").GetString(),
                Is.EqualTo("Álvaro Núñez Distribuciones"));
        });
    }

    [Test]
    public async Task GetInvoices_WithClientNameInternalSubstring_UsesContainsMatchingInsensitiveToCaseAndAccents()
    {
        var tenantId = Guid.NewGuid();
        await SeedInvoicesAsync(
            tenantId,
            new InvoiceSeed(null, "SALE-CLIENT-INTERNAL", "Distribuciones ÁLVARO Núñez del Sur", DateTimeOffset.UtcNow),
            new InvoiceSeed(null, "SALE-CLIENT-OTHER", "Beatriz García", DateTimeOffset.UtcNow));

        await using var factory = new InvoiceTestApplicationFactory(ConnectionString, ProviderName, tenantId);
        using var client = CreateAuthenticatedClient(factory, tenantId);
        using var response = await client.GetAsync(
            $"/api/example/invoices?clientName={Uri.EscapeDataString("vArO nUnEz")}");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var payload = await response.Content.ReadFromJsonAsync<JsonElement>();
        var items = payload.GetProperty("items").EnumerateArray().ToArray();

        Assert.Multiple(() =>
        {
            Assert.That(items, Has.Length.EqualTo(1));
            Assert.That(items[0].GetProperty("invoiceNumber").GetString(), Is.EqualTo("SALE-CLIENT-INTERNAL"));
            Assert.That(items[0].GetProperty("clientName").GetString(),
                Is.EqualTo("Distribuciones ÁLVARO Núñez del Sur"));
            Assert.That(payload.GetProperty("totalCount").GetInt32(), Is.EqualTo(1));
        });
    }

    [Test]
    public async Task GetInvoices_WithEquivalentUtcAndOffsetDateRanges_ReturnsSameResults()
    {
        var tenantId = Guid.NewGuid();
        var lowerBoundary = new DateTimeOffset(2026, 7, 5, 10, 0, 0, TimeSpan.Zero);
        var upperBoundary = lowerBoundary.AddHours(2);
        var seeded = await SeedInvoicesAsync(
            tenantId,
            new InvoiceSeed(null, "SALE-T50-LOWER", "Álvaro Núñez", lowerBoundary),
            new InvoiceSeed(null, "SALE-T50-MIDDLE", "Álvaro Núñez", lowerBoundary.AddHours(1)),
            new InvoiceSeed(null, "SALE-T50-UPPER", "Álvaro Núñez", upperBoundary),
            new InvoiceSeed(null, "SALE-T50-AFTER", "Álvaro Núñez", upperBoundary.AddSeconds(1)));

        await using var factory = new InvoiceTestApplicationFactory(ConnectionString, ProviderName, tenantId);
        using var client = CreateAuthenticatedClient(factory, tenantId);
        var utcQuery =
            $"clientId={seeded.ClientIds["Álvaro Núñez"]}" +
            $"&issuedFrom={Uri.EscapeDataString("2026-07-05T10:00:00Z")}" +
            $"&issuedTo={Uri.EscapeDataString("2026-07-05T12:00:00Z")}";
        var offsetQuery =
            $"clientId={seeded.ClientIds["Álvaro Núñez"]}" +
            $"&issuedFrom={Uri.EscapeDataString("2026-07-05T12:00:00+02:00")}" +
            $"&issuedTo={Uri.EscapeDataString("2026-07-05T14:00:00+02:00")}";

        using var utcResponse = await client.GetAsync($"/api/example/invoices?{utcQuery}");
        using var offsetResponse = await client.GetAsync($"/api/example/invoices?{offsetQuery}");

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
    public async Task GetInvoiceById_ReturnsHeaderAndHistoricalLineSnapshotsEvenWhenClientIsDeleted()
    {
        var tenantId = Guid.NewGuid();
        var issuedAt = new DateTimeOffset(2026, 7, 5, 10, 30, 0, TimeSpan.Zero);
        var seeded = await SeedInvoicesAsync(
            tenantId,
            new InvoiceSeed(null, "SALE-HISTORY-001", "Álvaro Núñez", issuedAt));
        var invoice = seeded.Invoices.Single();

        await using (var context = CreateDbContext(tenantId))
        {
            var clientEntity = await context.Clients.SingleAsync(entity =>
                entity.Id == seeded.ClientIds["Álvaro Núñez"]);
            clientEntity.Name = "Renamed live client";
            clientEntity.IsDeleted = true;
            clientEntity.DeletedOn = DateTimeOffset.UtcNow;

            var firstProduct = await context.Products.SingleAsync(entity =>
                entity.Id == seeded.ProductIds[0]);
            firstProduct.Code = "LIVE-CHANGED-01";
            firstProduct.Name = "Changed live product";

            await context.SaveChangesAsync();
        }

        await using var factory = new InvoiceTestApplicationFactory(ConnectionString, ProviderName, tenantId);
        using var client = CreateAuthenticatedClient(factory, tenantId);
        using var response = await client.GetAsync($"/api/example/invoices/{invoice.Id}");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var payload = await response.Content.ReadFromJsonAsync<JsonElement>();
        var lines = payload.GetProperty("lines").EnumerateArray().ToArray();

        Assert.Multiple(() =>
        {
            Assert.That(payload.GetProperty("id").GetGuid(), Is.EqualTo(invoice.Id));
            Assert.That(payload.GetProperty("invoiceNumber").GetString(), Is.EqualTo("SALE-HISTORY-001"));
            Assert.That(DateTimeOffset.Parse(payload.GetProperty("issuedAt").GetString()!).ToUniversalTime(),
                Is.EqualTo(issuedAt));
            Assert.That(payload.GetProperty("clientId").GetGuid(),
                Is.EqualTo(seeded.ClientIds["Álvaro Núñez"]));
            Assert.That(payload.GetProperty("clientName").GetString(), Is.EqualTo("Álvaro Núñez"));
            Assert.That(lines, Has.Length.EqualTo(2));
            Assert.That(lines[0].GetProperty("lineNumber").GetInt32(), Is.EqualTo(1));
            Assert.That(lines[0].GetProperty("productId").GetGuid(), Is.EqualTo(seeded.ProductIds[0]));
            Assert.That(lines[0].GetProperty("productCode").GetString(), Is.EqualTo("HIST-001"));
            Assert.That(lines[0].GetProperty("productName").GetString(), Is.EqualTo("Historical product one"));
            Assert.That(lines[0].GetProperty("quantity").GetDecimal(), Is.EqualTo(2m));
            Assert.That(lines[0].GetProperty("unitPrice").GetDecimal(), Is.EqualTo(15.50m));
            Assert.That(lines[1].GetProperty("lineNumber").GetInt32(), Is.EqualTo(2));
            Assert.That(lines[1].GetProperty("productCode").GetString(), Is.EqualTo("HIST-002"));
            Assert.That(lines[1].GetProperty("productName").GetString(), Is.EqualTo("Historical product two"));
            Assert.That(payload.TryGetProperty("tenantId", out _), Is.False);
            Assert.That(lines.All(line => !line.TryGetProperty("tenantId", out _)), Is.True);
        });
    }

    [Test]
    public async Task GetInvoiceById_ReturnsNotFoundForUnknownAndOtherTenantIds()
    {
        var tenantId = Guid.NewGuid();
        var own = await SeedInvoicesAsync(
            tenantId,
            new InvoiceSeed(null, "SALE-OWN", "Álvaro Núñez", DateTimeOffset.UtcNow));
        var foreign = await SeedInvoicesAsync(
            Guid.NewGuid(),
            new InvoiceSeed(null, "SALE-FOREIGN", "Foreign client", DateTimeOffset.UtcNow));

        await using var factory = new InvoiceTestApplicationFactory(ConnectionString, ProviderName, tenantId);
        using var client = CreateAuthenticatedClient(factory, tenantId);
        using var unknownResponse = await client.GetAsync($"/api/example/invoices/{Guid.NewGuid()}");
        using var foreignResponse = await client.GetAsync(
            $"/api/example/invoices/{foreign.Invoices.Single().Id}");
        using var ownResponse = await client.GetAsync(
            $"/api/example/invoices/{own.Invoices.Single().Id}");

        Assert.Multiple(() =>
        {
            Assert.That(unknownResponse.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
            Assert.That(foreignResponse.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
            Assert.That(ownResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        });
    }

    [Test]
    public async Task GetInvoices_RejectsDateFiltersWithoutAnExplicitOffset()
    {
        var tenantId = Guid.NewGuid();
        await SeedInvoicesAsync(
            tenantId,
            new InvoiceSeed(null, "SALE-DATE", "Álvaro Núñez", DateTimeOffset.UtcNow));

        await using var factory = new InvoiceTestApplicationFactory(ConnectionString, ProviderName, tenantId);
        using var client = CreateAuthenticatedClient(factory, tenantId);
        using var response = await client.GetAsync(
            "/api/example/invoices?issuedFrom=2026-07-05T10%3A00%3A00");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [Test]
    public async Task GetInvoices_RejectsIssuedFromLaterThanIssuedToAfterUtcNormalization()
    {
        var tenantId = Guid.NewGuid();
        await SeedInvoicesAsync(
            tenantId,
            new InvoiceSeed(null, "SALE-RANGE", "Álvaro Núñez", DateTimeOffset.UtcNow));

        await using var factory = new InvoiceTestApplicationFactory(ConnectionString, ProviderName, tenantId);
        using var client = CreateAuthenticatedClient(factory, tenantId);
        var query =
            $"issuedFrom={Uri.EscapeDataString("2026-07-05T12:00:00+02:00")}" +
            $"&issuedTo={Uri.EscapeDataString("2026-07-05T09:59:59Z")}";
        using var response = await client.GetAsync($"/api/example/invoices?{query}");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [Test]
    public async Task GetInvoices_RejectsUnknownQueryParametersAndUnsupportedSortFields()
    {
        var tenantId = Guid.NewGuid();
        await SeedInvoicesAsync(
            tenantId,
            new InvoiceSeed(null, "SALE-QUERY", "Álvaro Núñez", DateTimeOffset.UtcNow));

        await using var factory = new InvoiceTestApplicationFactory(ConnectionString, ProviderName, tenantId);
        using var client = CreateAuthenticatedClient(factory, tenantId);
        using var unknownParameterResponse = await client.GetAsync(
            "/api/example/invoices?tenantId=" + Guid.NewGuid());
        using var unsupportedSortResponse = await client.GetAsync(
            "/api/example/invoices?sortBy=tenantId&sortDirection=desc");
        using var invalidDirectionResponse = await client.GetAsync(
            "/api/example/invoices?sortBy=issuedAt&sortDirection=sideways");

        Assert.Multiple(() =>
        {
            Assert.That(unknownParameterResponse.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
            Assert.That(unsupportedSortResponse.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
            Assert.That(invalidDirectionResponse.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        });
    }

    [Test]
    public async Task GetInvoices_RequiresAuthenticatedUserAndReadyAccount()
    {
        var tenantId = Guid.NewGuid();
        await SeedInvoicesAsync(
            tenantId,
            new InvoiceSeed(null, "SALE-SECURITY", "Álvaro Núñez", DateTimeOffset.UtcNow));

        await using var factory = new InvoiceTestApplicationFactory(ConnectionString, ProviderName, tenantId);
        using var anonymousClient = factory.CreateClient();
        using var anonymousResponse = await anonymousClient.GetAsync("/api/example/invoices");

        using var notReadyClient = CreateAuthenticatedClient(factory, tenantId, accountReady: false);
        using var notReadyResponse = await notReadyClient.GetAsync("/api/example/invoices");

        Assert.Multiple(() =>
        {
            Assert.That(anonymousResponse.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
            Assert.That(notReadyResponse.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
        });
    }

    [Test]
    public async Task PostInvoice_WithValidHeaderAndLines_CreatesInvoiceAndPersistsCompleteAggregate()
    {
        var tenantId = Guid.NewGuid();
        var clientId = Guid.NewGuid();
        var firstProductId = Guid.NewGuid();
        var secondProductId = Guid.NewGuid();

        const string clientName = "I18-015 Customer";
        const string invoiceNumber = "SALE-I18-015-001";
        const string firstProductCode = "I18-015-P01";
        const string firstProductName = "I18-015 Product One";
        const string secondProductCode = "I18-015-P02";
        const string secondProductName = "I18-015 Product Two";

        var issuedAt = DateTimeOffset.UtcNow.AddMinutes(-5);
        issuedAt = issuedAt.AddTicks(-(issuedAt.Ticks % TimeSpan.TicksPerSecond));

        await using (var seedContext = CreateDbContext(tenantId))
        {
            seedContext.Clients.Add(new Client
            {
                Id = clientId,
                TenantId = tenantId,
                Name = clientName
            });
            seedContext.Products.AddRange(
                new Product
                {
                    Id = firstProductId,
                    TenantId = tenantId,
                    Code = firstProductCode,
                    Name = firstProductName
                },
                new Product
                {
                    Id = secondProductId,
                    TenantId = tenantId,
                    Code = secondProductCode,
                    Name = secondProductName
                });
            await seedContext.SaveChangesAsync();
        }

        await using var factory = new InvoiceTestApplicationFactory(ConnectionString, ProviderName, tenantId);
        using var client = CreateAuthenticatedClient(factory, tenantId);

        using var response = await client.PostAsJsonAsync(
            "/api/example/invoices",
            new
            {
                clientId,
                invoiceNumber,
                issuedAt,
                lines = new[]
                {
                    new
                    {
                        lineNumber = 1,
                        productId = firstProductId,
                        quantity = 2m,
                        unitPrice = 15.50m
                    },
                    new
                    {
                        lineNumber = 2,
                        productId = secondProductId,
                        quantity = 3m,
                        unitPrice = 8.25m
                    }
                }
            });

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        Assert.That(response.Headers.Location, Is.Not.Null);

        var payload = await response.Content.ReadFromJsonAsync<JsonElement>();
        var invoiceId = payload.GetProperty("id").GetGuid();
        var responseLines = payload.GetProperty("lines")
            .EnumerateArray()
            .OrderBy(line => line.GetProperty("lineNumber").GetInt32())
            .ToArray();

        Assert.Multiple(() =>
        {
            Assert.That(invoiceId, Is.Not.EqualTo(Guid.Empty));
            Assert.That(
                response.Headers.Location!.ToString(),
                Does.EndWith($"/api/example/invoices/{invoiceId}"));
            Assert.That(payload.GetProperty("invoiceNumber").GetString(), Is.EqualTo(invoiceNumber));
            Assert.That(payload.GetProperty("issuedAt").GetDateTimeOffset(), Is.EqualTo(issuedAt));
            Assert.That(payload.GetProperty("clientId").GetGuid(), Is.EqualTo(clientId));
            Assert.That(payload.GetProperty("clientName").GetString(), Is.EqualTo(clientName));
            Assert.That(responseLines, Has.Length.EqualTo(2));

            Assert.That(responseLines[0].GetProperty("id").GetGuid(), Is.Not.EqualTo(Guid.Empty));
            Assert.That(responseLines[0].GetProperty("lineNumber").GetInt32(), Is.EqualTo(1));
            Assert.That(responseLines[0].GetProperty("productId").GetGuid(), Is.EqualTo(firstProductId));
            Assert.That(responseLines[0].GetProperty("productCode").GetString(), Is.EqualTo(firstProductCode));
            Assert.That(responseLines[0].GetProperty("productName").GetString(), Is.EqualTo(firstProductName));
            Assert.That(responseLines[0].GetProperty("quantity").GetDecimal(), Is.EqualTo(2m));
            Assert.That(responseLines[0].GetProperty("unitPrice").GetDecimal(), Is.EqualTo(15.50m));

            Assert.That(responseLines[1].GetProperty("id").GetGuid(), Is.Not.EqualTo(Guid.Empty));
            Assert.That(responseLines[1].GetProperty("lineNumber").GetInt32(), Is.EqualTo(2));
            Assert.That(responseLines[1].GetProperty("productId").GetGuid(), Is.EqualTo(secondProductId));
            Assert.That(responseLines[1].GetProperty("productCode").GetString(), Is.EqualTo(secondProductCode));
            Assert.That(responseLines[1].GetProperty("productName").GetString(), Is.EqualTo(secondProductName));
            Assert.That(responseLines[1].GetProperty("quantity").GetDecimal(), Is.EqualTo(3m));
            Assert.That(responseLines[1].GetProperty("unitPrice").GetDecimal(), Is.EqualTo(8.25m));

            Assert.That(payload.TryGetProperty("tenantId", out _), Is.False);
            Assert.That(responseLines.All(line => !line.TryGetProperty("tenantId", out _)), Is.True);
        });

        await using var verificationContext = CreateDbContext(tenantId);
        var persistedInvoice = await verificationContext.Invoices
            .Include(invoice => invoice.Lines)
            .SingleAsync(invoice => invoice.Id == invoiceId);
        var persistedLines = persistedInvoice.Lines
            .OrderBy(line => line.LineNumber)
            .ToArray();

        Assert.Multiple(() =>
        {
            Assert.That(persistedInvoice.TenantId, Is.EqualTo(tenantId));
            Assert.That(persistedInvoice.ClientId, Is.EqualTo(clientId));
            Assert.That(persistedInvoice.InvoiceNumber, Is.EqualTo(invoiceNumber));
            Assert.That(persistedInvoice.IssuedAt, Is.EqualTo(issuedAt));
            Assert.That(persistedInvoice.ClientName, Is.EqualTo(clientName));
            Assert.That(persistedLines, Has.Length.EqualTo(2));
            Assert.That(
                persistedLines.Select(line => line.Id),
                Is.EqualTo(responseLines.Select(line => line.GetProperty("id").GetGuid())));
            Assert.That(persistedLines.All(line => line.TenantId == tenantId), Is.True);
            Assert.That(persistedLines.All(line => line.InvoiceId == invoiceId), Is.True);

            Assert.That(persistedLines[0].ProductId, Is.EqualTo(firstProductId));
            Assert.That(persistedLines[0].ProductCode, Is.EqualTo(firstProductCode));
            Assert.That(persistedLines[0].ProductName, Is.EqualTo(firstProductName));
            Assert.That(persistedLines[0].Quantity, Is.EqualTo(2m));
            Assert.That(persistedLines[0].UnitPrice, Is.EqualTo(15.50m));

            Assert.That(persistedLines[1].ProductId, Is.EqualTo(secondProductId));
            Assert.That(persistedLines[1].ProductCode, Is.EqualTo(secondProductCode));
            Assert.That(persistedLines[1].ProductName, Is.EqualTo(secondProductName));
            Assert.That(persistedLines[1].Quantity, Is.EqualTo(3m));
            Assert.That(persistedLines[1].UnitPrice, Is.EqualTo(8.25m));
        });
    }

    private async Task<SeededInvoices> SeedInvoicesAsync(Guid tenantId, params InvoiceSeed[] invoiceSeeds)
    {
        if (invoiceSeeds.Length == 0)
        {
            throw new ArgumentException("At least one invoice seed is required.", nameof(invoiceSeeds));
        }

        var clients = invoiceSeeds
            .Select(seed => seed.ClientName)
            .Distinct(StringComparer.Ordinal)
            .ToDictionary(
                name => name,
                name => new Client
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    Name = name
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
                Description = "Invoice endpoint test product one"
            },
            new Product
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Code = "HIST-002",
                Name = "Historical product two",
                Description = "Invoice endpoint test product two"
            }
        };

        var invoices = new List<Invoice>();
        var lines = new List<InvoiceLine>();
        foreach (var seed in invoiceSeeds)
        {
            var invoice = new Invoice
            {
                Id = seed.Id ?? Guid.NewGuid(),
                TenantId = tenantId,
                ClientId = clients[seed.ClientName].Id,
                InvoiceNumber = seed.InvoiceNumber,
                IssuedAt = seed.IssuedAt,
                ClientName = seed.ClientName
            };
            invoices.Add(invoice);
            lines.AddRange(
            [
                new InvoiceLine
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    InvoiceId = invoice.Id,
                    LineNumber = 1,
                    ProductId = products[0].Id,
                    ProductCode = products[0].Code,
                    ProductName = products[0].Name,
                    Quantity = 2m,
                    UnitPrice = 15.50m
                },
                new InvoiceLine
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    InvoiceId = invoice.Id,
                    LineNumber = 2,
                    ProductId = products[1].Id,
                    ProductCode = products[1].Code,
                    ProductName = products[1].Name,
                    Quantity = 3m,
                    UnitPrice = 20m
                }
            ]);
        }

        await using var context = CreateDbContext(tenantId);
        context.Clients.AddRange(clients.Values);
        context.Products.AddRange(products);
        context.Invoices.AddRange(invoices);
        context.InvoiceLines.AddRange(lines);
        await context.SaveChangesAsync();

        return new SeededInvoices(
            clients.ToDictionary(pair => pair.Key, pair => pair.Value.Id, StringComparer.Ordinal),
            products.Select(product => product.Id).ToArray(),
            invoices);
    }


    private static IComparable SortValue(Invoice invoice, string sortBy) => sortBy switch
    {
        "invoiceNumber" => invoice.InvoiceNumber,
        "issuedAt" => invoice.IssuedAt,
        "clientName" => invoice.ClientName,
        _ => throw new ArgumentOutOfRangeException(nameof(sortBy))
    };

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

    private sealed record InvoiceSeed(
        Guid? Id,
        string InvoiceNumber,
        string ClientName,
        DateTimeOffset IssuedAt);

    private sealed record SeededInvoices(
        IReadOnlyDictionary<string, Guid> ClientIds,
        IReadOnlyList<Guid> ProductIds,
        IReadOnlyList<Invoice> Invoices);

    private sealed class InvoiceTestApplicationFactory(
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
