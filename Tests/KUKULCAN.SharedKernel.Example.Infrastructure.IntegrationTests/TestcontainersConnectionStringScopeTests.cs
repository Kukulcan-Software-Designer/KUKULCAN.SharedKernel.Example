namespace KUKULCAN.SharedKernel.Example.Infrastructure.IntegrationTests;

[TestFixture]
[NonParallelizable]
public sealed class TestcontainersConnectionStringScopeTests
{
    private const string ConnectionStringEnvironmentVariable = "KUKULCAN__DATABASE__CONNECTIONSTRING";

    [Test]
    public void Scope_RestoresTheOriginalConnectionStringAfterDisposal()
    {
        var originalValue = Environment.GetEnvironmentVariable(ConnectionStringEnvironmentVariable);
        const string existingValue = "Host=existing-host;Database=existing-database";
        const string generatedValue = "Host=testcontainers-host;Database=testcontainers-database";

        try
        {
            Environment.SetEnvironmentVariable(ConnectionStringEnvironmentVariable, existingValue);

            using (new TestcontainersConnectionStringScope(generatedValue))
            {
                Assert.That(
                    Environment.GetEnvironmentVariable(ConnectionStringEnvironmentVariable),
                    Is.EqualTo(generatedValue));
            }

            Assert.That(
                Environment.GetEnvironmentVariable(ConnectionStringEnvironmentVariable),
                Is.EqualTo(existingValue));
        }
        finally
        {
            Environment.SetEnvironmentVariable(ConnectionStringEnvironmentVariable, originalValue);
        }
    }

    [Test]
    public void Scope_RemovesTheTemporaryConnectionStringWhenNoOriginalValueExisted()
    {
        var originalValue = Environment.GetEnvironmentVariable(ConnectionStringEnvironmentVariable);

        try
        {
            Environment.SetEnvironmentVariable(ConnectionStringEnvironmentVariable, null);

            using (new TestcontainersConnectionStringScope(
                "Host=testcontainers-host;Database=testcontainers-database"))
            {
                Assert.That(
                    Environment.GetEnvironmentVariable(ConnectionStringEnvironmentVariable),
                    Is.Not.Null.And.Not.Empty);
            }

            Assert.That(
                Environment.GetEnvironmentVariable(ConnectionStringEnvironmentVariable),
                Is.Null);
        }
        finally
        {
            Environment.SetEnvironmentVariable(ConnectionStringEnvironmentVariable, originalValue);
        }
    }

    [Test]
    public void Scope_RejectsAnEmptyGeneratedConnectionStringWithoutChangingTheOriginalValue()
    {
        var originalValue = Environment.GetEnvironmentVariable(ConnectionStringEnvironmentVariable);

        try
        {
            Environment.SetEnvironmentVariable(
                ConnectionStringEnvironmentVariable,
                "Host=existing-host;Database=existing-database");

            Assert.Throws<ArgumentException>(
                () => new TestcontainersConnectionStringScope(" "));

            Assert.That(
                Environment.GetEnvironmentVariable(ConnectionStringEnvironmentVariable),
                Is.EqualTo("Host=existing-host;Database=existing-database"));
        }
        finally
        {
            Environment.SetEnvironmentVariable(ConnectionStringEnvironmentVariable, originalValue);
        }
    }
}
