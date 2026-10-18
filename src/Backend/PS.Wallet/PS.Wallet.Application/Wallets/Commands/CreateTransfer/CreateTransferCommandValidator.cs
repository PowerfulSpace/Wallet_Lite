using FluentValidation;

namespace PS.Wallet.Application.Wallets.Commands.CreateTransfer;

public sealed class CreateTransferCommandValidator : AbstractValidator<CreateTransferCommand>
{
    public CreateTransferCommandValidator()
    {
        RuleFor(x => x.RecipientUsername)
            .NotEmpty().WithMessage("Recipient username is required.")
            .Matches(@"^[A-Za-z0-9_]{3,32}$").WithMessage("Recipient username must match ^[A-Za-z0-9_]{3,32}$.");

        RuleFor(x => x.Amount)
            .GreaterThan(0m).WithMessage("Amount must be greater than zero.")
            .LessThanOrEqualTo(1000000.00m).WithMessage("Amount must not exceed 1,000,000.00.")
            .Must(a => decimal.Round(a, 2) == a).WithMessage("Amount must have at most two fractional decimal digits.");

        RuleFor(x => x.IdempotencyKey)
            .NotEmpty().WithMessage("Idempotency-Key is required.")
            .Matches(@"^[A-Za-z0-9_-]{8,128}$").WithMessage("Idempotency-Key must match ^[A-Za-z0-9_-]{8,128}$.");
    }
}
