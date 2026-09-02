using Microsoft.Extensions.DependencyInjection;
using Overmem.Abstractions;
using Overmem.Abstractions.Extensions;
using Overmem.Extensions.Pes2021.ClubRelations;
using Overmem.Extensions.Pes2021.Fixtures;
using Overmem.Extensions.Pes2021.Players;
using Overmem.Runtime;

namespace Overmem.Extensions.Pes2021;

/// <summary>
/// Overmem extension that registers PES 2021 Master League agenda services and MCP tools.
/// </summary>
public sealed class Pes2021Extension : IOvermemExtension
{
    public string Name => "PES 2021 Agenda";

    public string Description => "Master League calendar inspection, runtime day analysis, competition mapping, and player-memory read services for PES 2021.";

    public IServiceCollection RegisterServices(IServiceCollection services)
    {
        services.AddSingleton<Pes2021AgendaService>();
        services.AddSingleton<Pes2021CalendarSessionCache>();
        services.AddSingleton<Pes2021CompetitionFixtureService>();
        services.AddSingleton<Pes2021ClubRelationsService>();
        services.AddSingleton<Pes2021PlayerSessionCache>(sp =>
            new Pes2021PlayerSessionCache(sp.GetRequiredService<IProcessMemoryGateway>()));
        services.AddSingleton<Pes2021PlayerAnchorFinder>(sp =>
            new Pes2021PlayerAnchorFinder(sp.GetRequiredService<IProcessMemoryGateway>(), sp.GetRequiredService<ISystemClock>()));
        services.AddSingleton<Pes2021PlayerRegionScanner>(sp =>
            new Pes2021PlayerRegionScanner(sp.GetRequiredService<IProcessMemoryGateway>(), sp.GetRequiredService<ISystemClock>()));
        services.AddSingleton<Pes2021PlayerCatalog>();
        services.AddSingleton<Pes2021PlayerCatalogService>(sp =>
            new Pes2021PlayerCatalogService(
                sp.GetRequiredService<Pes2021PlayerCatalog>(),
                sp.GetRequiredService<Pes2021PlayerAnchorFinder>(),
                sp.GetRequiredService<Pes2021PlayerRegionScanner>(),
                sp.GetRequiredService<Pes2021PlayerSessionCache>(),
                sp.GetRequiredService<IProcessMemoryGateway>(),
                sp.GetRequiredService<ISystemClock>()));
        services.AddSingleton<Pes2021PlayerQueryService>(sp =>
            new Pes2021PlayerQueryService(sp.GetRequiredService<Pes2021PlayerCatalog>()));
        return services;
    }
}
