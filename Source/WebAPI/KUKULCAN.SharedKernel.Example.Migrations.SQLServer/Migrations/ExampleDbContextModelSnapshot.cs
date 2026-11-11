using KUKULCAN.SharedKernel.Database.Abstractions;
using KUKULCAN.SharedKernel.Database.Configuration;
using KUKULCAN.SharedKernel.Database.Extensions;
using KUKULCAN.SharedKernel.Example.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace KUKULCAN.SharedKernel.Example.Migrations.SQLServer.Migrations;

[DbContext(typeof(ExampleDbContext))]
partial class ExampleDbContextModelSnapshot : ModelSnapshot
{
    protected override void BuildModel(ModelBuilder modelBuilder)
        => ExampleSchemaV1Model.Configure(modelBuilder);

}
