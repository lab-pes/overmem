using ModelContextProtocol.Server;
using Overmem.Abstractions.Processes;
using Overmem.Application.Tables;
using System.ComponentModel;

namespace Overmem.McpServer.Tools;

[McpServerToolType]
public sealed class TableTools(MemoryTableService memoryTableService)
{
    [McpServerTool(Name = "load_memory_table"), Description("Load a versioned memory table document from disk.")]
    public Task<MemoryTableDocument> LoadMemoryTable(
        [Description("The path to the JSON table document.")] string filePath,
        CancellationToken cancellationToken = default)
        => memoryTableService.LoadAsync(filePath, cancellationToken);

    [McpServerTool(Name = "save_memory_table"), Description("Save a versioned memory table document to disk.")]
    public Task SaveMemoryTable(
        [Description("The path to the JSON table document.")] string filePath,
        [Description("The memory table document to persist.")] MemoryTableDocument document,
        CancellationToken cancellationToken = default)
        => memoryTableService.SaveAsync(filePath, document, cancellationToken);

    [McpServerTool(Name = "refresh_memory_table"), Description("Resolve and read all entries in a memory table document for an attached process.")]
    public async Task<MemoryTableSnapshot> RefreshMemoryTable(
        [Description("The attachment identifier returned by attach_process.")] Guid attachmentId,
        [Description("The path to the JSON table document.")] string filePath,
        CancellationToken cancellationToken = default)
    {
        var document = await memoryTableService.LoadAsync(filePath, cancellationToken);
        return await memoryTableService.RefreshAsync(new AttachmentId(attachmentId), document, cancellationToken);
    }
}