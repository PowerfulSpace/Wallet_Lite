namespace PS.Wallet.API.Contracts;

public sealed record CreateTransferRequest(string? RecipientUsername, string? Amount);
