using BuildingBlocks.ServiceDefaults;
using BuildingBlocks.ServiceDefaults.Persistence;
using Partners.Api;
using Partners.Api.Api;
using Partners.Api.Infrastructure;

var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();
builder.AddPartners();

var app = builder.Build();
app.UseServiceDefaults();
app.MapPartnersEndpoints();
app.MapGrpcService<PartnersGrpcService>();

await app.ApplyMigrationsAsync<PartnersDbContext>();
await app.RunAsync();

public partial class Program;
