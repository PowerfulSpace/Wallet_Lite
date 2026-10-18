using System.Globalization;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PS.Wallet.API.Contracts;
using PS.Wallet.Application;
using PS.Wallet.Application.Wallets.Commands.CreateDeposit;
using PS.Wallet.Application.Wallets.Commands.CreateTransfer;
using PS.Wallet.Application.Wallets.DTOs;
using PS.Wallet.Application.Wallets.Queries.GetDeposit;
using PS.Wallet.Application.Wallets.Queries.GetTransfer;
using PS.Wallet.Application.Wallets.Queries.GetTransactions;
using PS.Wallet.Application.Wallets.Queries.GetWallet;

namespace PS.Wallet.API.Controllers;

[ApiController]
[Route("api/v1/wallet")]
[Authorize]
public sealed class WalletsController : ControllerBase
{
    private readonly IMediator _mediator;

    public WalletsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet]
    [ProducesResponseType(typeof(WalletDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<WalletDto>> GetWallet(CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetWalletQuery(), cancellationToken);
        return Ok(result);
    }

    [HttpPost("deposits")]
    [ProducesResponseType(typeof(DepositDto), StatusCodes.Status201Created)]
    public async Task<ActionResult<DepositDto>> CreateDeposit(
        [FromBody] CreateDepositRequest request,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        CancellationToken cancellationToken)
    {
        var amount = ParseAmount(request?.Amount);

        var command = new CreateDepositCommand(amount, idempotencyKey ?? string.Empty);
        var result = await _mediator.Send(command, cancellationToken);

        return CreatedAtAction(nameof(GetDeposit), new { id = result.DepositId }, result);
    }

    [HttpGet("deposits/{id:guid}")]
    [ProducesResponseType(typeof(DepositDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<DepositDto>> GetDeposit(Guid id, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetDepositQuery(id), cancellationToken);
        return Ok(result);
    }

    [HttpPost("/api/v1/transfers")]
    [ProducesResponseType(typeof(TransferDto), StatusCodes.Status201Created)]
    public async Task<ActionResult<TransferDto>> CreateTransfer(
        [FromBody] CreateTransferRequest request,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        CancellationToken cancellationToken)
    {
        var amount = ParseAmount(request?.Amount);
        var recipientUsername = request?.RecipientUsername?.Trim() ?? string.Empty;

        var command = new CreateTransferCommand(recipientUsername, amount, idempotencyKey ?? string.Empty);
        var result = await _mediator.Send(command, cancellationToken);

        return CreatedAtAction(nameof(GetTransfer), new { id = result.TransferId }, result);
    }

    [HttpGet("/api/v1/transfers/{id:guid}")]
    [ProducesResponseType(typeof(TransferDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<TransferDto>> GetTransfer(Guid id, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetTransferQuery(id), cancellationToken);
        return Ok(result);
    }

    [HttpGet("transactions")]
    [ProducesResponseType(typeof(PagedTransactionsDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedTransactionsDto>> GetTransactions(
        [FromQuery] string? cursor,
        [FromQuery] int? limit,
        CancellationToken cancellationToken)
    {
        var effectiveLimit = limit ?? 20; // CONST-007
        var result = await _mediator.Send(new GetTransactionsQuery(cursor, effectiveLimit), cancellationToken);
        return Ok(result);
    }

    private static decimal ParseAmount(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw) ||
            !decimal.TryParse(
                raw,
                NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
                CultureInfo.InvariantCulture,
                out var amount))
        {
            throw new ValidationAppException(new Dictionary<string, string[]>
            {
                ["amount"] = new[] { "Amount must be a decimal string." },
            });
        }

        return amount;
    }
}
