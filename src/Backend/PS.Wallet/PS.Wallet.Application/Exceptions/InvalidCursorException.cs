namespace PS.Wallet.Application;

public sealed class InvalidCursorException : Exception
{
    public InvalidCursorException(string message) : base(message) { }
}
