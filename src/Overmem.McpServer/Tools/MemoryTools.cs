using ModelContextProtocol.Server;
using Overmem.Abstractions.Memory;
using Overmem.Abstractions.Processes;
using Overmem.Application;
using Overmem.Application.Pointers;
using System.ComponentModel;

namespace Overmem.McpServer.Tools;

[McpServerToolType]
public sealed class MemoryTools(ProcessMemoryApplicationService applicationService, IPointerDiscoveryService pointerDiscoveryService)
{
    [McpServerTool(Name = "list_regions"), Description("List memory regions for an attached process.")]
    public Task<IReadOnlyList<MemoryRegionInfo>> ListRegions(
        [Description("The attachment identifier returned by attach_process.")] Guid attachmentId,
        CancellationToken cancellationToken = default)
        => applicationService.ListRegionsAsync(new AttachmentId(attachmentId), cancellationToken);

    [McpServerTool(Name = "discover_pointers"), Description("Discover absolute pointer candidates that can resolve to a known target address within a bounded depth and offset range.")]
    public Task<DiscoverPointersResult> DiscoverPointers(
        [Description("The attachment identifier returned by attach_process.")] Guid attachmentId,
        [Description("The final target address you want candidate pointer chains to resolve to.")] ulong targetAddress,
        [Description("Maximum pointer depth explored.")] int maxDepth = 2,
        [Description("Maximum absolute offset applied after each pointer dereference.")] long maxOffset = 0,
        [Description("Address alignment step in bytes. Use 0 to default to the process pointer size.")] int alignment = 0,
        [Description("Maximum number of candidates returned.")] int maxResults = 100,
        [Description("Optional module name used to keep only candidates whose base address falls inside that loaded module.")] string? baseModuleName = null,
        [Description("Whether each candidate should be revalidated by resolving the pointer chain before it is returned.")] bool revalidateCandidates = true,
        CancellationToken cancellationToken = default)
        => pointerDiscoveryService.DiscoverAsync(new DiscoverPointersRequest(
            new AttachmentId(attachmentId),
            targetAddress,
            maxDepth,
            maxOffset,
            alignment,
            maxResults,
            baseModuleName,
            revalidateCandidates), cancellationToken);

    [McpServerTool(Name = "resolve_pointer"), Description("Resolve a pointer chain to a final absolute address.")]
    public Task<ResolvePointerResult> ResolvePointer(
        [Description("The attachment identifier returned by attach_process.")] Guid attachmentId,
        [Description("The absolute base address that stores the first pointer.")] ulong baseAddress,
        [Description("The offsets applied after each pointer dereference.")] long[] offsets,
        CancellationToken cancellationToken = default)
        => applicationService.ResolvePointerAsync(new ResolvePointerRequest(new AttachmentId(attachmentId), baseAddress, offsets), cancellationToken);

    [McpServerTool(Name = "resolve_module_pointer"), Description("Resolve a pointer chain using a module-relative base address.")]
    public Task<ResolvePointerResult> ResolveModulePointer(
        [Description("The attachment identifier returned by attach_process.")] Guid attachmentId,
        [Description("The module name used as the base of the pointer chain.")] string moduleName,
        [Description("The signed offset added to the module base before the first dereference.")] long baseOffset,
        [Description("The offsets applied after each pointer dereference.")] long[] offsets,
        CancellationToken cancellationToken = default)
        => applicationService.ResolveModulePointerAsync(new ResolveModulePointerRequest(new AttachmentId(attachmentId), moduleName, baseOffset, offsets), cancellationToken);

    [McpServerTool(Name = "scan_pattern"), Description("Scan readable memory regions for a hex pattern with optional ?? wildcards.")]
    public Task<PatternScanResult> ScanPattern(
        [Description("The attachment identifier returned by attach_process.")] Guid attachmentId,
        [Description("Pattern like '39 05 ?? ?? ?? ??'.") ] string pattern,
        [Description("Optional module name to constrain the scan.")] string? moduleName = null,
        [Description("Maximum number of result addresses returned.")] int maxResults = 100,
        CancellationToken cancellationToken = default)
        => applicationService.ScanPatternAsync(new PatternScanRequest(new AttachmentId(attachmentId), pattern, moduleName, maxResults), cancellationToken);

    [McpServerTool(Name = "read_value"), Description("Read a typed value from the memory of an attached process.")]
    public Task<ReadMemoryResult> ReadValue(
        [Description("The attachment identifier returned by attach_process.")] Guid attachmentId,
        [Description("The absolute address to read from.")] ulong address,
        [Description("The kind of value to read.")] MemoryValueKind valueKind,
        [Description("Byte length for byte arrays and strings.")] int size = 0,
        CancellationToken cancellationToken = default)
        => applicationService.ReadAsync(new ReadMemoryRequest(new AttachmentId(attachmentId), address, valueKind, size), cancellationToken);

    [McpServerTool(Name = "write_value"), Description("Write a typed value into the memory of an attached process.")]
    public Task<WriteMemoryResult> WriteValue(
        [Description("The attachment identifier returned by attach_process.")] Guid attachmentId,
        [Description("The absolute address to write to.")] ulong address,
        [Description("The kind of value to write.")] MemoryValueKind valueKind,
        [Description("The value to write. Bytes must be uppercase or lowercase hex without separators.")] string value,
        [Description("Byte length for fixed-size strings.")] int size = 0,
        CancellationToken cancellationToken = default)
        => applicationService.WriteAsync(new WriteMemoryRequest(new AttachmentId(attachmentId), address, valueKind, value, size), cancellationToken);
}