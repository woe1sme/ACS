using Activity.Api;
using Activity.Api.Api;
using Activity.Api.Infrastructure;
using BuildingBlocks.ServiceDefaults;
using BuildingBlocks.ServiceDefaults.Persistence;

var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();
builder.AddActivity();

var app = builder.Build();
app.UseServiceDefaults();
app.MapActivityEndpoints();

await app.ApplyMigrationsAsync<ActivityDbContext>();
await app.RunAsync();

public partial class Program;
