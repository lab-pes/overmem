using Microsoft.Extensions.DependencyInjection;
using Overmem.Abstractions;
using Overmem.Application;
using Overmem.Application.Freezing;
using Overmem.Application.Pointers;
using Overmem.Application.Tables;
using Overmem.Runtime;
using Overmem.Runtime.Attachments;
using Overmem.Runtime.Diagnostics;
using Overmem.Search;
using Overmem.Windows.Processes;

namespace Overmem.Hosting;

public static class OvermemPlatformServiceCollectionExtensions
{
    public static IServiceCollection AddOvermemPlatformServices(this IServiceCollection services)
    {
        services.AddSingleton<ISystemClock>(SystemClock.Instance);
        services.AddSingleton<IAttachmentSessionRegistry, InMemoryAttachmentSessionRegistry>();
        services.AddSingleton<IOperationJournal>(_ => new InMemoryOperationJournal());
        services.AddSingleton<IProcessMemoryGateway, WindowsProcessMemoryGateway>();
        services.AddSingleton<IProcessFreezeCoordinator, ProcessFreezeCoordinator>();
        services.AddSingleton<IMemoryTableRepository, JsonMemoryTableRepository>();
        services.AddSingleton<MemoryTableService>();
        services.AddSingleton<ProcessMemoryApplicationService>();
        services.AddSingleton<IPointerDiscoveryService, PointerDiscoveryService>();
        services.AddSingleton<IValueSearchService, ValueSearchService>();

        return services;
    }
}
