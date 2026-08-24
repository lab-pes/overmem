using ModelContextProtocol.Server;
using Overmem.Abstractions.Memory;
using Overmem.Abstractions.Search;
using Overmem.Search;
using System.ComponentModel;

namespace Overmem.McpServer.Tools;

[McpServerToolType]
public sealed class SearchTools(IValueSearchService valueSearchService)
{
    [McpServerTool(Name = "start_value_search"), Description("Start an exact typed value search session for an attached process.")]
    public Task<ValueSearchResult> StartValueSearch(
        [Description("The attachment identifier returned by attach_process.")] Guid attachmentId,
        [Description("The kind of value to search.")] MemoryValueKind valueKind,
        [Description("The exact value to search for.")] string value,
        [Description("Byte length for byte arrays and strings.")] int size = 0,
        [Description("Alignment step in bytes. Use 1 to scan every address.")] int alignment = 1,
        [Description("Maximum number of matches retained in the session.")] int maxResults = 1000,
        CancellationToken cancellationToken = default)
        => valueSearchService.StartExactSearchAsync(
            new StartValueSearchRequest(new(attachmentId), valueKind, value, size, alignment, maxResults),
            cancellationToken);

    [McpServerTool(Name = "start_unknown_value_search"), Description("Start a value search session without an initial value. All aligned locations are captured as baseline. Use refine_value_search to narrow the results with Changed, Unchanged, Increased, Decreased, Between, etc.")]
    public Task<ValueSearchResult> StartUnknownValueSearch(
        [Description("The attachment identifier returned by attach_process.")] Guid attachmentId,
        [Description("The kind of value to track.")] MemoryValueKind valueKind,
        [Description("Byte length. Required for Bytes, Utf8String, and Utf16String.")] int size = 0,
        [Description("Alignment step in bytes. Use 4 for Int32, 8 for Int64/Double.")] int alignment = 1,
        [Description("Maximum number of baseline snapshots retained.")] int maxResults = 1_000_000,
        CancellationToken cancellationToken = default)
        => valueSearchService.StartUnknownSearchAsync(
            new StartUnknownValueSearchRequest(new(attachmentId), valueKind, size, alignment, maxResults),
            cancellationToken);

    [McpServerTool(Name = "refine_value_search"), Description("Refine an existing value search session. Comparisons: Exact, NotEqual, Changed, Unchanged, Increased, Decreased, IncreasedBy, DecreasedBy, ChangedBy, Between.")]
    public Task<ValueSearchResult> RefineValueSearch(
        [Description("The value search session identifier returned by start_value_search.")] Guid sessionId,
        [Description("How the existing result set should be filtered.")] ValueSearchComparison comparison,
        [Description("For Exact/NotEqual: the value to compare. For IncreasedBy/DecreasedBy/ChangedBy: the numeric delta. For Between: the lower bound.")] string? value = null,
        [Description("Only used by Between: the upper bound.")] string? secondaryValue = null,
        CancellationToken cancellationToken = default)
        => valueSearchService.RefineAsync(new RefineValueSearchRequest(new(sessionId), comparison, value, secondaryValue), cancellationToken);

    [McpServerTool(Name = "list_value_search_sessions"), Description("List value search sessions tracked by the current Overmem host.")]
    public Task<IReadOnlyList<ValueSearchSessionInfo>> ListValueSearchSessions(CancellationToken cancellationToken = default)
        => valueSearchService.ListSessionsAsync(cancellationToken);

    [McpServerTool(Name = "list_value_search_results"), Description("List the current results of a value search session.")]
    public Task<ValueSearchResult> ListValueSearchResults(
        [Description("The value search session identifier.")] Guid sessionId,
        CancellationToken cancellationToken = default)
        => valueSearchService.GetResultsAsync(new(sessionId), cancellationToken);

    [McpServerTool(Name = "close_value_search_session"), Description("Close and discard a value search session.")]
    public Task<bool> CloseValueSearchSession(
        [Description("The value search session identifier.")] Guid sessionId,
        CancellationToken cancellationToken = default)
        => valueSearchService.CloseSessionAsync(new(sessionId), cancellationToken);
}