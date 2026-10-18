using PS.Wallet.Application;
using PS.Wallet.Domain;

namespace PS.Wallet.Infrastructure.Persistence;

internal static class TransactionalOutcomePolicy
{
    // Business rejections and cancellation are confirmed outcomes; anything else raised after a
    // financial transaction has begun may be an uncertain commit (ADR-004 §8).
    internal static bool IsConfirmedFailure(Exception exception) =>
        exception is ConflictException
            or ValidationAppException
            or DomainException
            or OperationCanceledException;
}
