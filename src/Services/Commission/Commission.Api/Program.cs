using BuildingBlocks.ServiceDefaults;
using BuildingBlocks.ServiceDefaults.Persistence;
using Commission.Api;
using Commission.Api.Api;
using Commission.Api.Infrastructure;

var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();
builder.AddCommission();

var app = builder.Build();
app.UseServiceDefaults();
app.MapCommissionEndpoints();
app.MapGrpcService<CommissionGrpcService>();

await app.ApplyMigrationsAsync<CommissionDbContext>();
await app.RunAsync();

public partial class Program;
