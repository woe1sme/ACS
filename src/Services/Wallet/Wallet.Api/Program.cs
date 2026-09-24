using BuildingBlocks.ServiceDefaults;
using BuildingBlocks.ServiceDefaults.Persistence;
using Wallet.Api;
using Wallet.Api.Api;
using Wallet.Api.Infrastructure;

var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();
builder.AddWallet();

var app = builder.Build();
app.UseServiceDefaults();
app.MapWalletEndpoints();

await app.ApplyMigrationsAsync<WalletDbContext>();
await app.RunAsync();

public partial class Program;
