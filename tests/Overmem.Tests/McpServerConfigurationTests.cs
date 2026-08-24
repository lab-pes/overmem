using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ModelContextProtocol;
using ModelContextProtocol.Server;
using Overmem.Application.Pointers;
using Overmem.McpServer;
using Overmem.Runtime.Attachments;
using Overmem.Runtime.Diagnostics;
using Overmem.Search;

namespace Overmem.Tests;

#pragma warning disable MCPEXP001

public sealed class McpServerConfigurationTests
{
    [Fact]
    public void AddOvermemServices_ConfiguresTaskStore()
    {
        var services = new ServiceCollection();

        services.AddOvermemServices();
        using var provider = services.BuildServiceProvider();

        var taskStore = provider.GetService<IMcpTaskStore>();
        var options = provider.GetRequiredService<IOptions<McpServerOptions>>().Value;
        var sessionRegistry = provider.GetService<IAttachmentSessionRegistry>();
        var operationJournal = provider.GetService<IOperationJournal>();
        var pointerDiscoveryService = provider.GetService<IPointerDiscoveryService>();
        var valueSearchService = provider.GetService<IValueSearchService>();

        Assert.NotNull(taskStore);
        Assert.Same(taskStore, options.TaskStore);
        Assert.NotNull(sessionRegistry);
        Assert.NotNull(operationJournal);
        Assert.NotNull(pointerDiscoveryService);
        Assert.NotNull(valueSearchService);
    }
}

#pragma warning restore MCPEXP001