using FluentValidation;

namespace PS.Wallet.Application.Wallets.Commands.CreateDeposit;

public sealed class CreateDepositCommandValidator : AbstractValidator<CreateDepositCommand>
{
    public CreateDepositCommandValidator()
    {
        RuleFor(x => x.Amount)
            .GreaterThan(0m).WithMessage("Amount must be greater than zero.")
            .LessThanOrEqualTo(1000000.00m).WithMessage("Amount must not exceed 1,000,000.00.")
            .Must(a => decimal.Round(a, 2) == a).WithMessage("Amount must have at most two fractional decimal digits.");

        RuleFor(x => x.IdempotencyKey)
            .NotEmpty().WithMessage("Idempotency-Key is required.")
            .Matches(@"^[A-Za-z0-9_-]{8,128}$").WithMessage("Idempotency-Key must match ^[A-Za-z0-9_-]{8,128}$.");
    }
}
