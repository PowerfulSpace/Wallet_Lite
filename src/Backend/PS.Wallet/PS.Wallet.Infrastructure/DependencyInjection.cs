using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PS.Wallet.Application.Abstractions;

namespace PS.Wallet.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, string connectionString)
    {
        services.AddDbContext<global::PS.Wallet.Infrastructure.Persistence.WalletDbContext>(options =>
            options.UseSqlServer(connectionString));

        services.AddScoped<IWalletDbContext>(sp =>
            sp.GetRequiredService<global::PS.Wallet.Infrastructure.Persistence.WalletDbContext>());

        services.AddScoped<IDepositTransactionExecutor, global::PS.Wallet.Infrastructure.Persistence.DepositTransactionExecutor>();
        services.AddScoped<ITransferTransactionExecutor, global::PS.Wallet.Infrastructure.Persistence.TransferTransactionExecutor>();
        services.AddScoped<IIdempotencyCoordinator, global::PS.Wallet.Infrastructure.Persistence.IdempotencyCoordinator>();

        return services;
    }
}
