using Microsoft.Extensions.DependencyInjection;
using Overmem.Hosting;
using ModelContextProtocol;
using ModelContextProtocol.Server;
using Overmem.Application;
using Overmem.Extensions.Pes2021;
using Overmem.Extensions.Pes2021.Fixtures;
using Overmem.Extensions.Pes2021.Tools;
using Overmem.McpServer.Tools;

namespace Overmem.McpServer;

#pragma warning disable MCPEXP001

public static class OvermemServiceCollectionExtensions
{
    public static IServiceCollection AddOvermemServices(this IServiceCollection services)
    {
        var taskStore = new InMemoryMcpTaskStore(
            pollInterval: TimeSpan.FromSeconds(1),
            cleanupInterval: TimeSpan.FromMinutes(1),
            maxTasks: 256,
            maxTasksPerSession: 256);

        services.AddOvermemPlatformServices();
        services.AddPes2021Extension();
        services.AddSingleton<IMcpTaskStore>(taskStore);

        services.AddMcpServer(options =>
            {
                options.TaskStore = taskStore;
            })
            .WithStdioServerTransport()
            .WithTools<FreezeTools>()
            .WithTools<ProcessTools>()
            .WithTools<MemoryTools>()
            .WithTools<RuntimeTools>()
            .WithTools<SearchTools>()
            .WithTools<TableTools>()
            .WithTools<Pes2021AgendaTools>(Pes2021FixtureJson.Options);

        return services;
    }
}

#pragma warning restore MCPEXP001
