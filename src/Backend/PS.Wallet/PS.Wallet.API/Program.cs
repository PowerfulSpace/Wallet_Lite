using Microsoft.AspNetCore.Authentication;
using PS.Wallet.API.Auth;
using PS.Wallet.API.Json;
using PS.Wallet.API.Middleware;
using PS.Wallet.Application;
using PS.Wallet.Application.Abstractions;
using PS.Wallet.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services
    .AddControllers()
    .AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new MoneyJsonConverter()));
builder.Services.AddProblemDetails();

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new MoneyJsonConverter());
});

var connectionString = builder.Configuration.GetConnectionString("Wallet")
    ?? throw new InvalidOperationException("Connection string 'Wallet' is not configured.");

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, HttpContextCurrentUser>();
builder.Services.AddApplication();
builder.Services.AddInfrastructure(connectionString);

if (builder.Environment.IsDevelopment())
{
    builder.Services
        .AddAuthentication(DevelopmentAuthenticationHandler.SchemeName)
        .AddScheme<AuthenticationSchemeOptions, DevelopmentAuthenticationHandler>(
            DevelopmentAuthenticationHandler.SchemeName, _ => { });
}
else
{
    throw new InvalidOperationException("Production auth is not configured.");
}

builder.Services.AddAuthorization();
builder.Services.AddExceptionHandler<ExceptionHandlingMiddleware>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<PS.Wallet.Infrastructure.Persistence.WalletDbContext>();
    await PS.Wallet.Infrastructure.Persistence.Seed.DevelopmentSeeder.SeedAsync(db);

    app.MapOpenApi();
}

app.UseExceptionHandler();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapGet("/health", () => Results.Ok(new { status = "Healthy" }));

app.Run();

public partial class Program { }
