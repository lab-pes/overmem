using ModelContextProtocol.Server;
using Overmem.Abstractions.Processes;
using Overmem.Application;
using System.ComponentModel;

namespace Overmem.McpServer.Tools;

[McpServerToolType]
public sealed class ProcessTools(ProcessMemoryApplicationService applicationService)
{
    [McpServerTool(Name = "attach_process"), Description("Attach to a process by PID or by process name.")]
    public Task<AttachmentInfo> AttachProcess(
        [Description("The process ID to attach to.")] int? processId = null,
        [Description("The process name to attach to when PID is not provided.")] string? processName = null,
        CancellationToken cancellationToken = default)
        => applicationService.AttachAsync(new ProcessSelector(processId, processName), cancellationToken);

    [McpServerTool(Name = "detach_process"), Description("Detach a previously attached process session.")]
    public Task DetachProcess(
        [Description("The attachment identifier returned by attach_process.")] Guid attachmentId,
        CancellationToken cancellationToken = default)
        => applicationService.DetachAsync(new AttachmentId(attachmentId), cancellationToken);

    [McpServerTool(Name = "list_modules"), Description("List modules loaded by an attached process.")]
    public Task<IReadOnlyList<Overmem.Abstractions.Memory.ModuleInfo>> ListModules(
        [Description("The attachment identifier returned by attach_process.")] Guid attachmentId,
        CancellationToken cancellationToken = default)
        => applicationService.ListModulesAsync(new AttachmentId(attachmentId), cancellationToken);
}