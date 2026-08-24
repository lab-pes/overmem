using Microsoft.Extensions.DependencyInjection;

namespace Overmem.Extensions.Pes2021;

public static class Pes2021ServiceCollectionExtensions
{
    /// <summary>
    /// Register the PES 2021 extension services into the DI container.
    /// </summary>
    public static IServiceCollection AddPes2021Extension(this IServiceCollection services)
    {
        var extension = new Pes2021Extension();
        return extension.RegisterServices(services);
    }
}
