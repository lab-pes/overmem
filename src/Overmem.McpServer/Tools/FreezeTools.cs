using ModelContextProtocol.Server;
using Overmem.Abstractions.Freezing;
using Overmem.Abstractions.Memory;
using Overmem.Abstractions.Processes;
using Overmem.Application;
using System.ComponentModel;

namespace Overmem.McpServer.Tools;

[McpServerToolType]
public sealed class FreezeTools(ProcessMemoryApplicationService applicationService)
{
    [McpServerTool(Name = "freeze_value_at_address"), Description("Freeze a typed value at an absolute address inside an attached process.")]
    public Task<FreezeInfo> FreezeValueAtAddress(
        [Description("The attachment identifier returned by attach_process.")] Guid attachmentId,
        [Description("The absolute address to freeze.")] ulong address,
        [Description("The kind of value to write continuously.")] MemoryValueKind valueKind,
        [Description("The value to freeze.")] string value,
        [Description("Byte length for fixed-size strings.")] int size = 0,
        [Description("Rewrite interval in milliseconds.")] int intervalMs = 25,
        CancellationToken cancellationToken = default)
        => applicationService.FreezeAsync(
            new FreezeRequest(new AttachmentId(attachmentId), new AbsoluteAddressSource(address), valueKind, value, size, intervalMs),
            cancellationToken);

    [McpServerTool(Name = "freeze_value_at_pointer"), Description("Freeze a typed value resolved from an absolute-base pointer chain.")]
    public Task<FreezeInfo> FreezeValueAtPointer(
        [Description("The attachment identifier returned by attach_process.")] Guid attachmentId,
        [Description("The absolute base address that stores the first pointer.")] ulong baseAddress,
        [Description("The offsets applied after each pointer dereference.")] long[] offsets,
        [Description("The kind of value to write continuously.")] MemoryValueKind valueKind,
        [Description("The value to freeze.")] string value,
        [Description("Byte length for fixed-size strings.")] int size = 0,
        [Description("Rewrite interval in milliseconds.")] int intervalMs = 25,
        CancellationToken cancellationToken = default)
        => applicationService.FreezeAsync(
            new FreezeRequest(new AttachmentId(attachmentId), new PointerAddressSource(baseAddress, offsets), valueKind, value, size, intervalMs),
            cancellationToken);

    [McpServerTool(Name = "freeze_value_at_module_pointer"), Description("Freeze a typed value resolved from a module-relative pointer chain.")]
    public Task<FreezeInfo> FreezeValueAtModulePointer(
        [Description("The attachment identifier returned by attach_process.")] Guid attachmentId,
        [Description("The module name used as the base of the pointer chain.")] string moduleName,
        [Description("The signed offset added to the module base before the first dereference.")] long baseOffset,
        [Description("The offsets applied after each pointer dereference.")] long[] offsets,
        [Description("The kind of value to write continuously.")] MemoryValueKind valueKind,
        [Description("The value to freeze.")] string value,
        [Description("Byte length for fixed-size strings.")] int size = 0,
        [Description("Rewrite interval in milliseconds.")] int intervalMs = 25,
        CancellationToken cancellationToken = default)
        => applicationService.FreezeAsync(
            new FreezeRequest(new AttachmentId(attachmentId), new ModulePointerAddressSource(moduleName, baseOffset, offsets), valueKind, value, size, intervalMs),
            cancellationToken);

    [McpServerTool(Name = "unfreeze_value"), Description("Cancel a previously created freeze operation.")]
    public Task<bool> UnfreezeValue(
        [Description("The freeze identifier returned by a freeze tool.")] Guid freezeId,
        CancellationToken cancellationToken = default)
        => applicationService.UnfreezeAsync(new FreezeId(freezeId), cancellationToken);

    [McpServerTool(Name = "list_frozen_values"), Description("List active freeze operations tracked by the current Overmem host.")]
    public Task<IReadOnlyList<FreezeInfo>> ListFrozenValues(CancellationToken cancellationToken = default)
        => applicationService.ListFreezesAsync(cancellationToken);
}