namespace PS.Wallet.Infrastructure.Persistence;

public sealed class UncertainCommitException : Exception
{
    public UncertainCommitException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }
}
