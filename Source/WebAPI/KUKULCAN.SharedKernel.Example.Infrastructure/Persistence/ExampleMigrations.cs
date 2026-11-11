using KUKULCAN.SharedKernel.Database.Configuration;

namespace KUKULCAN.SharedKernel.Example.Infrastructure.Persistence;

/// <summary>Resolves the provider-specific, versioned Example migration assembly.</summary>
public static class ExampleMigrations
{
    /// <summary>Gets the migration assembly name for the configured database provider.</summary>
    public static string GetAssemblyName(DatabaseProvider provider)
        => provider switch
        {
            DatabaseProvider.PostgresSql => "KUKULCAN.SharedKernel.Example.Migrations.PostgreSQL",
            DatabaseProvider.SqlServer => "KUKULCAN.SharedKernel.Example.Migrations.SQLServer",
            DatabaseProvider.MySql => "KUKULCAN.SharedKernel.Example.Migrations.MySQL",
            _ => throw new NotSupportedException(
                $"Database provider '{provider}' does not have an Example migration assembly.")
        };
}
