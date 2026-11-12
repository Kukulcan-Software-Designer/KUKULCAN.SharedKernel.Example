using MySql.Data.MySqlClient;
using Testcontainers.MySql;

namespace KUKULCAN.SharedKernel.Example.Infrastructure.IntegrationTests;

[TestFixture]
[NonParallelizable]
public sealed class MySqlContainerConnectivityIntegrationTests
{
    private const string MySqlImage =
        "mysql@sha256:893418c8a7a34c60d2d961cc30ef136095da141478562ed2101ae3f5faba9c3f";

    private const string ExpectedServerVersion = "26.7.0";

    [Test]
    public async Task MySqlContainer_ShouldStartAndAcceptRealDatabaseConnections()
    {
        var databaseName = $"example_connectivity_{Guid.NewGuid():N}";
        var username = TestDatabaseCredentials.GenerateUserName();
        var password = TestDatabaseCredentials.GeneratePassword();

        await using var mysql = new MySqlBuilder(MySqlImage)
            .WithDatabase(databaseName)
            .WithUsername(username)
            .WithPassword(password)
            .Build();

        await mysql.StartAsync();

        await using var connection = new MySqlConnection(mysql.GetConnectionString());
        await connection.OpenAsync();

        await using var versionCommand = connection.CreateCommand();
        versionCommand.CommandText = "SELECT VERSION();";
        var actualVersion = Convert.ToString(await versionCommand.ExecuteScalarAsync());

        await using var databaseCommand = connection.CreateCommand();
        databaseCommand.CommandText = "SELECT DATABASE();";
        var actualDatabase = Convert.ToString(await databaseCommand.ExecuteScalarAsync());

        Assert.Multiple(() =>
        {
            Assert.That(
                connection.State,
                Is.EqualTo(System.Data.ConnectionState.Open),
                "The MySQL connection must remain open.");

            Assert.That(
                actualVersion,
                Is.EqualTo(ExpectedServerVersion),
                "The container must run the expected MySQL server version.");

            Assert.That(
                actualDatabase,
                Is.EqualTo(databaseName),
                "The connection must target the database created by Testcontainers.");
        });
    }
}
