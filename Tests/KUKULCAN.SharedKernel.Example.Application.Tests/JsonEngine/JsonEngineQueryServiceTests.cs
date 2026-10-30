using System.Text.Json;
using KUKULCAN.SharedKernel.Example.Application.JsonEngine;
using KUKULCAN.SharedKernel.JsonEngine.SQL;
using KUKULCAN.SharedKernel.JsonEngine.SQL.Interfaces;

namespace KUKULCAN.SharedKernel.Example.Application.Tests.JsonEngine;

[TestFixture]
public sealed class JsonEngineQueryServiceTests
{
    private sealed class StubJsonSqlEngine : IJsonSqlEngine
    {
        public int ExecuteCalls { get; private set; }

        public string? ReceivedSql { get; private set; }

        public List<JsonSqlRow> Rows { get; set; } = [];

        public Exception? ExceptionToThrow { get; set; }

        public List<JsonSqlRow> Execute(
            System.Text.Json.Nodes.JsonNode root,
            string sql)
        {
            ExecuteCalls++;
            ReceivedSql = sql;

            return ExceptionToThrow is not null ? throw ExceptionToThrow : Rows;
        }
    }

    private static JsonDocument CreateDocument(string json) =>
        JsonDocument.Parse(json);

    [TestCase("null")]
    [TestCase("42")]
    [TestCase("\"text\"")]
    [TestCase("true")]
    public void Execute_WhenDocumentIsNotObjectOrArray_ThrowsValidationException(
        string json)
    {
        using JsonDocument document = CreateDocument(json);
        var engine = new StubJsonSqlEngine();
        var service = new JsonEngineQueryService(engine);

        JsonEngineQueryValidationException exception =
            Assert.Throws<JsonEngineQueryValidationException>(
                () => service.Execute(document.RootElement, "SELECT * FROM items LIMIT 1"))!;

        Assert.That(exception.FieldName, Is.EqualTo("document"));
        Assert.That(engine.ExecuteCalls, Is.Zero);
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("   ")]
    public void Execute_WhenQueryIsMissing_ThrowsValidationException(string? query)
    {
        using JsonDocument document = CreateDocument("""{"id":1}""");
        var engine = new StubJsonSqlEngine();
        var service = new JsonEngineQueryService(engine);

        JsonEngineQueryValidationException exception =
            Assert.Throws<JsonEngineQueryValidationException>(
                () => service.Execute(document.RootElement, query))!;

        Assert.That(exception.FieldName, Is.EqualTo("query"));
        Assert.That(engine.ExecuteCalls, Is.Zero);
    }

    [TestCase("UPDATE items SET id = 1 LIMIT 1")]
    [TestCase("SELECT * FROM items")]
    [TestCase("SELECT * FROM items LIMIT 1 LIMIT 2")]
    [TestCase("SELECT * FROM items LIMIT 0")]
    [TestCase("SELECT * FROM items LIMIT 101")]
    [TestCase("SELECT * FROM items LIMIT abc")]
    public void Execute_WhenQueryIsInvalid_ThrowsValidationException(string query)
    {
        using JsonDocument document = CreateDocument("""{"id":1}""");
        var engine = new StubJsonSqlEngine();
        var service = new JsonEngineQueryService(engine);

        JsonEngineQueryValidationException exception =
            Assert.Throws<JsonEngineQueryValidationException>(
                () => service.Execute(document.RootElement, query))!;

        Assert.That(exception.FieldName, Is.EqualTo("query"));
        Assert.That(engine.ExecuteCalls, Is.Zero);
    }

    [Test]
    public void Execute_WhenQueryContainsLineBreaks_NormalizesBeforeCallingEngine()
    {
        using JsonDocument document = CreateDocument("""{"id":1}""");
        var engine = new StubJsonSqlEngine();
        var service = new JsonEngineQueryService(engine);

        service.Execute(
            document.RootElement,
            "SELECT *\nFROM items\nLIMIT 1");

        Assert.That(engine.ExecuteCalls, Is.EqualTo(1));
        Assert.That(engine.ReceivedSql, Is.EqualTo("SELECT * FROM items LIMIT 1"));
    }

    [Test]
    public void Execute_WhenEngineThrowsArgumentException_TranslatesException()
    {
        using JsonDocument document = CreateDocument("""{"id":1}""");
        var engine = new StubJsonSqlEngine
        {
            ExceptionToThrow = new ArgumentException("Internal engine detail")
        };
        var service = new JsonEngineQueryService(engine);

        var exception =
            Assert.Throws<JsonEngineQueryValidationException>(
                () => service.Execute(document.RootElement, "SELECT * FROM items LIMIT 1"))!;

        Assert.Multiple(() =>
        {
            Assert.That(exception.FieldName, Is.EqualTo("query"));
            Assert.That(exception.Message, Does.Contain("invalid argument"));
        });
    }

    [Test]
    public void Execute_WhenEngineThrowsFormatException_TranslatesException()
    {
        using JsonDocument document = CreateDocument("""{"id":1}""");
        var engine = new StubJsonSqlEngine
        {
            ExceptionToThrow = new FormatException("Internal engine detail")
        };
        var service = new JsonEngineQueryService(engine);

        var exception =
            Assert.Throws<JsonEngineQueryValidationException>(
                () => service.Execute(document.RootElement, "SELECT * FROM items LIMIT 1"))!;

        Assert.Multiple(() =>
        {
            Assert.That(exception.FieldName, Is.EqualTo("query"));
            Assert.That(exception.Message, Does.Contain("cannot be interpreted"));
        });
    }

    [Test]
    public void Execute_WhenEngineReturnsNoRows_ReturnsEmptyResult()
    {
        using JsonDocument document = CreateDocument("""{"id":1}""");
        var engine = new StubJsonSqlEngine();
        var service = new JsonEngineQueryService(engine);

        JsonEngineQueryResult result =
            service.Execute(document.RootElement, "SELECT * FROM items LIMIT 1");

        Assert.Multiple(() =>
        {
            Assert.That(result.ResultCount, Is.Zero);
            Assert.That(result.Rows, Is.Empty);
        });
    }
}
