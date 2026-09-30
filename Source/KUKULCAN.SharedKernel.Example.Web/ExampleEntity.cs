namespace KUKULCAN.SharedKernel.Example.Web;

public sealed class ExampleEntity
{
    public Guid Id { get; private set; } = Guid.NewGuid();

    public string Name { get; private set; } = string.Empty;

    private ExampleEntity()
    {
    }

    public ExampleEntity(string name)
    {
        Name = string.IsNullOrWhiteSpace(name)
            ? throw new ArgumentException("Name is required.", nameof(name))
            : name;
    }
}
