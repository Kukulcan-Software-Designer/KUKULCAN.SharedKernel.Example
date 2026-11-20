using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using KUKULCAN.SharedKernel.Database.Abstractions;
using KUKULCAN.SharedKernel.Example.Domain.Entities;
using KUKULCAN.SharedKernel.Example.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using HttpResults = Microsoft.AspNetCore.Http.Results;

namespace KUKULCAN.SharedKernel.Example.WebApi.ProdInvoices;

public static class ProdInvoiceEndpoints
{
    private static readonly HashSet<string> AllowedQueryParameters = new(StringComparer.OrdinalIgnoreCase)
    {
        "providerId",
        "providerName",
        "invoiceNumber",
        "issuedFrom",
        "issuedTo",
        "page",
        "pageSize",
        "sortBy",
        "sortDirection"
    };

    private static readonly HashSet<string> AllowedSortFields = new(StringComparer.OrdinalIgnoreCase)
    {
        "invoiceNumber",
        "issuedAt",
        "providerName"
    };

    public static IEndpointRouteBuilder MapProdInvoiceEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/example/prod-invoices", GetProdInvoicesAsync)
            .WithName("GetProdInvoices")
            .Produces<ProdInvoicePagedResponse<ProdInvoiceListItemResponse>>(StatusCodes.Status200OK)
            .ProducesValidationProblem(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status403Forbidden);

        endpoints.MapGet("/api/example/prod-invoices/{id}", GetProdInvoiceByIdAsync)
            .WithName("GetProdInvoiceById")
            .Produces<ProdInvoiceDetailsResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound);

        return endpoints;
    }

    private static async Task<IResult> GetProdInvoiceByIdAsync(Guid id,
        [Microsoft.AspNetCore.Mvc.FromServices] ExampleDbContext context, ITenantContext tenantContext,
        CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId == Guid.Empty)
        {
            return HttpResults.Forbid();
        }

        // Do not join the current Provider record here. ProviderName is the
        // historical snapshot stored on the purchase document and remains
        // available when the Provider is subsequently soft-deleted or renamed.
        ProdInvoice? invoice = await context.ProdInvoices
            .AsNoTracking()
            .Include(entity => entity.Lines)
            .SingleOrDefaultAsync(
                entity => entity.Id == id && entity.TenantId == tenantContext.TenantId,
                cancellationToken);

        if (invoice is null)
        {
            return HttpResults.NotFound();
        }

        ProdInvoiceLineResponse[] lines =
        [
            .. invoice.Lines
                .OrderBy(line => line.LineNumber)
                .ThenBy(line => line.Id)
                .Select(line => new ProdInvoiceLineResponse(
                    line.Id,
                    line.LineNumber,
                    line.ProductId,
                    line.ProductCode,
                    line.ProductName,
                    line.Quantity,
                    line.UnitPrice))
        ];

        return HttpResults.Ok(new ProdInvoiceDetailsResponse(
            invoice.Id,
            invoice.InvoiceNumber,
            invoice.IssuedAt,
            invoice.ProviderId,
            invoice.ProviderName,
            lines));
    }

    private static async Task<IResult> GetProdInvoicesAsync(HttpRequest request, Guid? providerId, string? providerName,
        string? invoiceNumber, string? issuedFrom, string? issuedTo, int? page, int? pageSize, string? sortBy,
        string? sortDirection, [Microsoft.AspNetCore.Mvc.FromServices] ExampleDbContext context,
        ITenantContext tenantContext, CancellationToken cancellationToken)
    {
        string[] unsupportedParameters = request.Query.Keys
            .Where(key => !AllowedQueryParameters.Contains(key))
            .OrderBy(key => key, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (unsupportedParameters.Length > 0)
        {
            return HttpResults.ValidationProblem(new Dictionary<string, string[]>
            {
                ["query"] = [$"Unsupported query parameter(s): {string.Join(", ", unsupportedParameters)}."]
            });
        }

        int requestedPage = page ?? 1;
        int requestedPageSize = pageSize ?? 25;
        var validationErrors = new Dictionary<string, string[]>();

        if (requestedPage < 1)
        {
            validationErrors[nameof(page)] = ["Page must be greater than or equal to 1."];
        }

        if (requestedPageSize is < 1 or > 100)
        {
            validationErrors[nameof(pageSize)] = ["PageSize must be between 1 and 100."];
        }

        DateTimeOffset? issuedFromUtc = null;
        if (!string.IsNullOrWhiteSpace(issuedFrom))
        {
            if (!TryParseExplicitDateTimeOffset(issuedFrom, out DateTimeOffset parsedFrom))
            {
                validationErrors[nameof(issuedFrom)] = ["IssuedFrom must be an ISO 8601 instant with an explicit UTC offset."];
            }
            else
            {
                issuedFromUtc = parsedFrom.ToUniversalTime();
            }
        }

        DateTimeOffset? issuedToUtc = null;
        if (!string.IsNullOrWhiteSpace(issuedTo))
        {
            if (!TryParseExplicitDateTimeOffset(issuedTo, out DateTimeOffset parsedTo))
            {
                validationErrors[nameof(issuedTo)] = ["IssuedTo must be an ISO 8601 instant with an explicit UTC offset."];
            }
            else
            {
                issuedToUtc = parsedTo.ToUniversalTime();
            }
        }

        if (issuedFromUtc.HasValue && issuedToUtc.HasValue && issuedFromUtc > issuedToUtc)
        {
            validationErrors[nameof(issuedTo)] = ["IssuedTo must be greater than or equal to IssuedFrom after UTC normalization."];
        }

        string? normalizedSortBy = string.IsNullOrWhiteSpace(sortBy) ? null : sortBy.Trim();
        string? normalizedSortDirection = string.IsNullOrWhiteSpace(sortDirection)
            ? null
            : sortDirection.Trim().ToLowerInvariant();

        if (normalizedSortBy is not null && !AllowedSortFields.Contains(normalizedSortBy))
        {
            validationErrors[nameof(sortBy)] = ["SortBy must be one of: invoiceNumber, issuedAt, providerName."];
        }

        if (normalizedSortDirection is not null && normalizedSortDirection is not ("asc" or "desc"))
        {
            validationErrors[nameof(sortDirection)] = ["SortDirection must be either 'asc' or 'desc'."];
        }

        if (validationErrors.Count > 0)
        {
            return HttpResults.ValidationProblem(validationErrors);
        }

        if (tenantContext.TenantId == Guid.Empty)
        {
            return HttpResults.Forbid();
        }

        IQueryable<ProdInvoice> invoicesQuery = context.ProdInvoices
            .AsNoTracking()
            .Where(invoice => invoice.TenantId == tenantContext.TenantId);

        if (providerId.HasValue)
        {
            invoicesQuery = invoicesQuery.Where(invoice => invoice.ProviderId == providerId.Value);
        }

        if (issuedFromUtc.HasValue)
        {
            invoicesQuery = invoicesQuery.Where(invoice => invoice.IssuedAt >= issuedFromUtc.Value);
        }

        if (issuedToUtc.HasValue)
        {
            invoicesQuery = invoicesQuery.Where(invoice => invoice.IssuedAt <= issuedToUtc.Value);
        }

        // Search the document's ProviderName snapshot, never live Provider.Name.
        // This keeps historical purchase documents searchable after provider
        // renames or soft deletion and gives identical matching across providers.
        List<ProdInvoiceListItemResponse> matchingInvoices = await invoicesQuery
            .Select(invoice => new ProdInvoiceListItemResponse(
                invoice.Id,
                invoice.InvoiceNumber,
                invoice.IssuedAt,
                invoice.ProviderId,
                invoice.ProviderName))
            .ToListAsync(cancellationToken);

        if (!string.IsNullOrWhiteSpace(providerName))
        {
            string normalizedProviderName = NormalizeSearchText(providerName.Trim());
            matchingInvoices =
            [
                .. matchingInvoices.Where(invoice =>
                    NormalizeSearchText(invoice.ProviderName)
                        .Contains(normalizedProviderName, StringComparison.Ordinal))
            ];
        }

        if (!string.IsNullOrWhiteSpace(invoiceNumber))
        {
            string normalizedInvoiceNumber = NormalizeSearchText(invoiceNumber.Trim());
            matchingInvoices =
            [
                .. matchingInvoices.Where(invoice =>
                    NormalizeSearchText(invoice.InvoiceNumber)
                        .Equals(normalizedInvoiceNumber, StringComparison.Ordinal))
            ];
        }

        bool descending = normalizedSortBy is null
            ? normalizedSortDirection is null or "desc"
            : normalizedSortDirection == "desc";

        IOrderedEnumerable<ProdInvoiceListItemResponse> orderedInvoices =
            (normalizedSortBy?.ToLowerInvariant(), descending) switch
            {
                ("invoicenumber", true) => matchingInvoices
                    .OrderByDescending(invoice => NormalizeSearchText(invoice.InvoiceNumber), StringComparer.Ordinal)
                    .ThenByDescending(invoice => invoice.Id),
                ("invoicenumber", false) => matchingInvoices
                    .OrderBy(invoice => NormalizeSearchText(invoice.InvoiceNumber), StringComparer.Ordinal)
                    .ThenBy(invoice => invoice.Id),
                ("providername", true) => matchingInvoices
                    .OrderByDescending(invoice => NormalizeSearchText(invoice.ProviderName), StringComparer.Ordinal)
                    .ThenByDescending(invoice => invoice.Id),
                ("providername", false) => matchingInvoices
                    .OrderBy(invoice => NormalizeSearchText(invoice.ProviderName), StringComparer.Ordinal)
                    .ThenBy(invoice => invoice.Id),
                ("issuedat", true) => matchingInvoices
                    .OrderByDescending(invoice => invoice.IssuedAt)
                    .ThenByDescending(invoice => invoice.Id),
                ("issuedat", false) => matchingInvoices
                    .OrderBy(invoice => invoice.IssuedAt)
                    .ThenBy(invoice => invoice.Id),
                (_, true) => matchingInvoices
                    .OrderByDescending(invoice => invoice.IssuedAt)
                    .ThenByDescending(invoice => invoice.Id),
                _ => matchingInvoices
                    .OrderBy(invoice => invoice.IssuedAt)
                    .ThenBy(invoice => invoice.Id)
            };

        int totalCount = matchingInvoices.Count;
        long offset = (long)(requestedPage - 1) * requestedPageSize;
        ProdInvoiceListItemResponse[] items = offset > int.MaxValue
            ? []
            : [.. orderedInvoices.Skip((int)offset).Take(requestedPageSize)];

        int totalPages = totalCount == 0
            ? 0
            : (int)Math.Ceiling(totalCount / (double)requestedPageSize);

        return HttpResults.Ok(new ProdInvoicePagedResponse<ProdInvoiceListItemResponse>(
            items,
            requestedPage,
            requestedPageSize,
            totalCount,
            totalPages));
    }

    private static bool TryParseExplicitDateTimeOffset(string value, out DateTimeOffset result)
    {
        string trimmed = value.Trim();
        bool hasExplicitOffset = trimmed.EndsWith("Z", StringComparison.OrdinalIgnoreCase)
            || Regex.IsMatch(trimmed, @"[+-]\d{2}:\d{2}$", RegexOptions.CultureInvariant);

        result = default;
        return hasExplicitOffset
            && DateTimeOffset.TryParse(
                trimmed,
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out result);
    }

    private static string NormalizeSearchText(string value)
    {
        string decomposed = value.Normalize(NormalizationForm.FormD);
        var normalized = new StringBuilder(decomposed.Length);

        foreach (Rune rune in decomposed.EnumerateRunes()
                     .Where(rune => Rune.GetUnicodeCategory(rune) != UnicodeCategory.NonSpacingMark))
        {
            normalized.Append(rune);
        }

        return normalized
            .ToString()
            .Normalize(NormalizationForm.FormC)
            .ToUpperInvariant();
    }
}

public sealed record ProdInvoiceListItemResponse(Guid Id, string InvoiceNumber, DateTimeOffset IssuedAt, Guid ProviderId,
    string ProviderName);

public sealed record ProdInvoiceDetailsResponse(Guid Id, string InvoiceNumber, DateTimeOffset IssuedAt, Guid ProviderId,
    string ProviderName, IReadOnlyList<ProdInvoiceLineResponse> Lines);

public sealed record ProdInvoiceLineResponse(Guid Id, int LineNumber, Guid ProductId, string ProductCode,
    string ProductName, decimal Quantity, decimal UnitPrice);

public sealed record ProdInvoicePagedResponse<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount,
    int TotalPages);
