using Microsoft.Extensions.DependencyInjection;
using Overmem.Extensions.Pes2021.Cli;
using Overmem.Extensions.Pes2021.ClubRelations;
using Overmem.Extensions.Pes2021.Fixtures;
using Overmem.Extensions.Pes2021.Players;
using Overmem.Extensions.Pes2021.Players.FamilyDiscovery;
using Overmem.Extensions.Pes2021.Tools;

namespace Overmem.Extensions.Pes2021;

public static class Pes2021ServiceCollectionExtensions
{
    /// <summary>
    /// Register the PES 2021 extension services into the DI container.
    /// </summary>
    public static IServiceCollection AddPes2021Extension(this IServiceCollection services)
    {
        services.AddSingleton<Pes2021FamilyDiscoveryService>();
        var extension = new Pes2021Extension();
        return extension.RegisterServices(services);
    }
}
