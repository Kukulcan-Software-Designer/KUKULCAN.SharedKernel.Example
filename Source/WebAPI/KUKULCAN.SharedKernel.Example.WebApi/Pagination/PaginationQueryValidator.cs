using System.Globalization;

namespace KUKULCAN.SharedKernel.Example.WebApi.Pagination;

/// <summary>
/// Reads and validates the common pagination query parameters for Example list endpoints.
/// </summary>
internal static class PaginationQueryValidator
{
    private const int DefaultPage = 1;
    private const int DefaultPageSize = 25;
    private const int MaximumPageSize = 100;

    /// <summary>
    /// Validates pagination parameters and returns field-level errors without relying on
    /// Minimal API binding to nullable integers.
    /// </summary>
    /// <param name="request">The current HTTP request.</param>
    /// <param name="page">The normalized page number, or the default when absent or invalid.</param>
    /// <param name="pageSize">The normalized page size, or the default when absent or invalid.</param>
    /// <returns>Validation errors keyed by the corresponding query parameter.</returns>
    public static Dictionary<string, string[]> Validate(
        HttpRequest request,
        out int page,
        out int pageSize)
    {
        page = DefaultPage;
        pageSize = DefaultPageSize;

        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);

        if (!TryReadInteger(request, "page", DefaultPage, out page))
        {
            page = DefaultPage;
            errors["page"] = ["Page must be a single valid integer."];
        }
        else if (page < 1)
        {
            errors["page"] = ["Page must be greater than or equal to 1."];
        }

        if (!TryReadInteger(request, "pageSize", DefaultPageSize, out pageSize))
        {
            pageSize = DefaultPageSize;
            errors["pageSize"] = ["PageSize must be a single valid integer."];
        }
        else if (pageSize is < 1 or > MaximumPageSize)
        {
            errors["pageSize"] = ["PageSize must be between 1 and 100."];
        }

        return errors;
    }

    private static bool TryReadInteger(
        HttpRequest request,
        string parameterName,
        int defaultValue,
        out int value)
    {
        value = defaultValue;

        if (!request.Query.TryGetValue(parameterName, out var values))
        {
            return true;
        }

        return values.Count == 1
               && int.TryParse(
                   values[0],
                   NumberStyles.Integer,
                   CultureInfo.InvariantCulture,
                   out value);
    }
}
