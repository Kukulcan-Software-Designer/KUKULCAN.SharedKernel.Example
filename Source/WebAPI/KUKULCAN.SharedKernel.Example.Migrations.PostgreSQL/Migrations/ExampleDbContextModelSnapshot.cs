using KUKULCAN.SharedKernel.Database.Abstractions;
using KUKULCAN.SharedKernel.Database.Configuration;
using KUKULCAN.SharedKernel.Database.Extensions;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;
using KUKULCAN.SharedKernel.Example.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace KUKULCAN.SharedKernel.Example.Migrations.PostgreSQL.Migrations;

[DbContext(typeof(ExampleDbContext))]
partial class ExampleDbContextModelSnapshot : ModelSnapshot
{
    protected override void BuildModel(ModelBuilder modelBuilder)
    {
        modelBuilder.HasAnnotation("ProductVersion", "10.0.13");
        NpgsqlModelBuilderExtensions.UseIdentityByDefaultColumns(modelBuilder);
        ExampleDbContextModelConfiguration.ConfigureForProvider(modelBuilder, DatabaseProvider.PostgresSql);
        modelBuilder.ApplySoftDeleteFilter();
        modelBuilder.ApplyTenantFilter(new SnapshotTenantContext());
    }

    private sealed class SnapshotTenantContext : ITenantContext
    {
        public Guid TenantId => Guid.Empty;
    }
}
