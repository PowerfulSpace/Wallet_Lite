using PS.Wallet.Application.Abstractions;

namespace PS.Wallet.API.Auth;

public sealed class HttpContextCurrentUser : ICurrentUser
{
    private readonly IHttpContextAccessor _accessor;

    public HttpContextCurrentUser(IHttpContextAccessor accessor)
    {
        _accessor = accessor;
    }

    public string Username => _accessor.HttpContext?.User.Identity?.Name
        ?? throw new InvalidOperationException("No authenticated user.");
}
