using KUKULCAN.SharedKernel.Database.Abstractions;
using KUKULCAN.SharedKernel.Database.Configuration;
using KUKULCAN.SharedKernel.Database.Extensions;
using KUKULCAN.SharedKernel.Example.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace KUKULCAN.SharedKernel.Example.Migrations.MySQL.Migrations;

[DbContext(typeof(ExampleDbContext))]
partial class ExampleDbContextModelSnapshot : ModelSnapshot
{
    protected override void BuildModel(ModelBuilder modelBuilder)
    {
        modelBuilder.HasAnnotation("ProductVersion", "10.0.13");
        modelBuilder.HasAnnotation("Relational:MaxIdentifierLength", 64);
        ExampleDbContextModelConfiguration.Configure(modelBuilder);
        modelBuilder.ApplySoftDeleteFilter();
        modelBuilder.ApplyTenantFilter(new SnapshotTenantContext());
    }

    private sealed class SnapshotTenantContext : ITenantContext
    {
        public Guid TenantId => Guid.Empty;
    }
}
