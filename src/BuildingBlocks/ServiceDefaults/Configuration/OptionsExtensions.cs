using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace BuildingBlocks.ServiceDefaults.Configuration;

public static class OptionsExtensions
{
    /// <summary>Binds a configuration section to <typeparamref name="T"/>, validates data annotations and fails startup when invalid.</summary>
    public static OptionsBuilder<T> AddValidatedOptions<T>(this IServiceCollection services, IConfiguration configuration, string section)
        where T : class
    {
        return services.AddOptions<T>()
            .Bind(configuration.GetSection(section))
            .ValidateDataAnnotations()
            .ValidateOnStart();
    }
}
