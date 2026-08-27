using Microsoft.Extensions.DependencyInjection;
using Overmem.Abstractions.Extensions;
using Overmem.Extensions.Pes2021.Fixtures;

namespace Overmem.Extensions.Pes2021;

/// <summary>
/// Overmem extension that registers PES 2021 Master League agenda services and MCP tools.
/// </summary>
public sealed class Pes2021Extension : IOvermemExtension
{
    public string Name => "PES 2021 Agenda";

    public string Description => "Master League calendar inspection, runtime day analysis, and competition mapping for PES 2021.";

    public IServiceCollection RegisterServices(IServiceCollection services)
    {
        services.AddSingleton<Pes2021AgendaService>();
        services.AddSingleton<Pes2021CalendarSessionCache>();
        services.AddSingleton<Pes2021CompetitionFixtureService>();
        return services;
    }
}
