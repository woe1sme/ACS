using BuildingBlocks.ServiceDefaults.Grpc;
using BuildingBlocks.ServiceDefaults.Persistence;
using Partners.Api.Application;
using Partners.Api.Infrastructure;

namespace Partners.Api;

internal static class DependencyInjection
{
    public static WebApplicationBuilder AddPartners(this WebApplicationBuilder builder)
    {
        builder.AddServiceDbContext<PartnersDbContext>("Partners");
        builder.Services.AddScoped<IUserRepository, UserRepository>();
        builder.Services.AddScoped<ITreeMutationScope, TreeMutationScope>();
        builder.Services.AddScoped<PartnersService>();
        builder.Services.AddGrpcServerDefaults();
        return builder;
    }
}
