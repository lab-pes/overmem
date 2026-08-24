using ModelContextProtocol.Server;
using Overmem.Runtime.Attachments;
using Overmem.Runtime.Diagnostics;
using System.ComponentModel;

namespace Overmem.McpServer.Tools;

[McpServerToolType]
public sealed class RuntimeTools(IAttachmentSessionRegistry sessionRegistry, IOperationJournal operationJournal)
{
    [McpServerTool(Name = "list_active_attachments"), Description("List active attachments tracked by the current Overmem host.")]
    public IReadOnlyList<AttachmentSessionInfo> ListActiveAttachments()
        => sessionRegistry.ListActive();

    [McpServerTool(Name = "list_recent_operations"), Description("List recent runtime operations tracked by the current Overmem host.")]
    public IReadOnlyList<OperationLogEntry> ListRecentOperations(
        [Description("Maximum number of entries returned.")] int maxEntries = 100)
        => operationJournal.ListRecent(maxEntries);
}