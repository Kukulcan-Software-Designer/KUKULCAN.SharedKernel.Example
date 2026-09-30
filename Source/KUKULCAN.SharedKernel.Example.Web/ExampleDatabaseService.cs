using KUKULCAN.SharedKernel.Database.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace KUKULCAN.SharedKernel.Example.Web;

public sealed class ExampleDatabaseService(
    ExampleDbContext context,
    IUnitOfWork unitOfWork)
{
    public async Task<ExampleEntity> CreateAsync(
        string name,
        CancellationToken cancellationToken = default)
    {
        var entity = new ExampleEntity(name);
        context.Entities.Add(entity);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return entity;
    }

    public Task<ExampleEntity?> GetAsync(
        Guid id,
        CancellationToken cancellationToken = default)
        => context.Entities.SingleOrDefaultAsync(
            entity => entity.Id == id,
            cancellationToken);

    public async Task<ExampleEntity?> UpdateAsync(
        Guid id,
        string name,
        CancellationToken cancellationToken = default)
    {
        var entity = await context.Entities.SingleOrDefaultAsync(
            item => item.Id == id,
            cancellationToken);

        if (entity is null)
            return null;

        entity.UpdateName(name);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return entity;
    }
}
