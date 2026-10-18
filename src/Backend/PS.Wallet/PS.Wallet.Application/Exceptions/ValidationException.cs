namespace PS.Wallet.Application;

public sealed class ValidationAppException : Exception
{
    public IReadOnlyDictionary<string, string[]> Errors { get; }

    public ValidationAppException(IReadOnlyDictionary<string, string[]> errors)
        : base("Request validation failed.")
    {
        Errors = errors;
    }
}
