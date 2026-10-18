namespace PS.Wallet.Domain.Entities;

public sealed class User
{
    public Guid Id { get; private set; }
    public string Username { get; private set; } = default!;
    public string Email { get; private set; } = default!;
    public DateTime CreatedAtUtc { get; private set; }

    private User() { }

    public static User Create(string username, string email, DateTime createdAtUtc)
    {
        if (string.IsNullOrWhiteSpace(username))
        {
            throw new DomainException("Username is required.");
        }

        if (!System.Text.RegularExpressions.Regex.IsMatch(username, @"^[A-Za-z0-9_]{3,32}$"))
        {
            throw new DomainException("Username must match ^[A-Za-z0-9_]{3,32}$.");
        }

        if (string.IsNullOrWhiteSpace(email))
        {
            throw new DomainException("Email is required.");
        }

        return new User
        {
            Id = Guid.NewGuid(),
            Username = username.ToLowerInvariant(),
            Email = email,
            CreatedAtUtc = createdAtUtc,
        };
    }
}
