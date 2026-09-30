using KUKULCAN.SharedKernel.Auth.Authentication.Local;

namespace KUKULCAN.SharedKernel.Example.Web;

internal sealed class ExampleLocalUserStore : ILocalUserStore
{
    private readonly LocalUser _user;

    public ExampleLocalUserStore(LocalUser user)
    {
        ArgumentNullException.ThrowIfNull(user);
        _user = user;
    }

    public Task<LocalUser?> FindByEmailAsync(
        string email,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return Task.FromResult<LocalUser?>(
            string.Equals(_user.Email, email, StringComparison.Ordinal)
                ? _user
                : null);
    }
}
