using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using KUKULCAN.SharedKernel.JsonEngine.SQL.Interfaces;

namespace KUKULCAN.SharedKernel.Example.Application.JsonEngine;

/// <summary>
/// Executes bounded, SQL-like projections against a caller-supplied JSON document
/// using the published KUKULCAN.SharedKernel.JsonEngine contract.
/// </summary>
public sealed class JsonEngineQueryService(IJsonSqlEngine queryEngine)
{
    public const int MaxDocumentSizeBytes = 64 * 1024;
    public const int MaxQueryLength = 2_048;
    public const int MaxResultRows = 100;

    private static readonly Regex QueryShape = new(
        @"^SELECT\s+.+?\s+FROM\s+\S+",
        RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.CultureInvariant);

    private static readonly Regex LimitClause = new(
        @"\bLIMIT\s+(?<limit>\d+)\s*$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex LimitKeyword = new(
        @"\bLIMIT\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    public JsonEngineQueryResult Execute(JsonElement document, string? query)
    {
        if (document.ValueKind is not (JsonValueKind.Object or JsonValueKind.Array))
        {
            throw new JsonEngineQueryValidationException(
                "Document must be a JSON object or array.",
                "document");
        }

        string documentJson = document.GetRawText();
        if (Encoding.UTF8.GetByteCount(documentJson) > MaxDocumentSizeBytes)
        {
            throw new JsonEngineQueryValidationException(
                $"Document must not exceed {MaxDocumentSizeBytes} bytes.",
                "document");
        }

        if (string.IsNullOrWhiteSpace(query))
        {
            throw new JsonEngineQueryValidationException(
                "Query is required.",
                "query");
        }

        if (query.Length > MaxQueryLength)
        {
            throw new JsonEngineQueryValidationException(
                $"Query must not exceed {MaxQueryLength} characters.",
                "query");
        }

        string normalizedQuery = query.Trim();
        if (!QueryShape.IsMatch(normalizedQuery))
        {
            throw new JsonEngineQueryValidationException(
                "Query must use the supported SELECT ... FROM ... syntax.",
                "query");
        }

        Match limitMatch = LimitClause.Match(normalizedQuery);
        if (LimitKeyword.Matches(normalizedQuery).Count != 1 || !limitMatch.Success)
        {
            throw new JsonEngineQueryValidationException(
                "Query must end with exactly one LIMIT clause.",
                "query");
        }

        if (!int.TryParse(limitMatch.Groups["limit"].Value, out int requestedLimit)
            || requestedLimit is < 1 or > MaxResultRows)
        {
            throw new JsonEngineQueryValidationException(
                $"LIMIT must be between 1 and {MaxResultRows}.",
                "query");
        }

        JsonNode root;
        try
        {
            root = JsonNode.Parse(documentJson)
                ?? throw new JsonEngineQueryValidationException(
                    "Document must contain a JSON object or array.",
                    "document");
        }
        catch (JsonException)
        {
            throw new JsonEngineQueryValidationException(
                "Document is not valid JSON.",
                "document");
        }

        List<KUKULCAN.SharedKernel.JsonEngine.SQL.JsonSqlRow> queryRows;
        try
        {
            queryRows = queryEngine.Execute(root, normalizedQuery);
        }
        catch (ArgumentException)
        {
            throw new JsonEngineQueryValidationException(
                "Query contains an invalid argument for the supported JSON query dialect.",
                "query");
        }
        catch (FormatException)
        {
            throw new JsonEngineQueryValidationException(
                "Query contains a value that cannot be interpreted by the supported JSON query dialect.",
                "query");
        }

        // Return detached JSON values instead of exposing JsonNode instances from the library
        // as part of the Example's application response contract.
        IReadOnlyDictionary<string, JsonElement?>[] rows = queryRows
            .Select(row => (IReadOnlyDictionary<string, JsonElement?>)row.ToDictionary(
                pair => pair.Key,
                pair => pair.Value is null
                    ? (JsonElement?)null
                    : JsonSerializer.SerializeToElement(pair.Value),
                StringComparer.Ordinal))
            .ToArray();

        return new JsonEngineQueryResult(rows.Length, rows);
    }
}

public sealed record JsonEngineQueryResult(
    int ResultCount,
    IReadOnlyList<IReadOnlyDictionary<string, JsonElement?>> Rows);

public sealed class JsonEngineQueryValidationException(
    string message,
    string fieldName) : Exception(message)
{
    public string FieldName { get; } = fieldName;
}
