using Microsoft.Extensions.DependencyInjection;

namespace BuildingBlocks.ServiceDefaults.Grpc;

public static class GrpcExtensions
{
    public static IServiceCollection AddGrpcServerDefaults(this IServiceCollection services)
    {
        services.AddGrpc(o => o.Interceptors.Add<ErrorMappingInterceptor>());
        return services;
    }
}
