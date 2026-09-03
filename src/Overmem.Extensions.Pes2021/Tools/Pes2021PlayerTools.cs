using System.ComponentModel;
using ModelContextProtocol.Server;
using Overmem.Abstractions;
using Overmem.Abstractions.Processes;
using Overmem.Extensions.Pes2021.Fixtures;
using Overmem.Extensions.Pes2021.Players;
using Overmem.Runtime;

namespace Overmem.Extensions.Pes2021.Tools;

[McpServerToolType]
public sealed class Pes2021PlayerTools(
    Pes2021PlayerCatalogService catalogService,
    Pes2021PlayerQueryService queryService,
    IProcessMemoryGateway gateway,
    ISystemClock clock)
{
    [McpServerTool(Name = "pes2021_find_player_anchor"), Description("Find the PES 2021 EDIT-base player anchor for a given control player ID.")]
    public async Task<PlayerAnchorResult> FindPlayerAnchor(
        [Description("The attachment identifier returned by attach_process.")] Guid attachmentId,
        [Description("The control player ID to anchor on.")] int controlPlayerId,
        [Description("Optional process ID for identity propagation.")] int processId = 0,
        [Description("Optional profile path.")] string? profilePath = null,
        CancellationToken cancellationToken = default)
    {
        var profile = string.IsNullOrWhiteSpace(profilePath)
            ? Pes2021PlayerProfileDefaults.GetOrLoad()
            : Pes2021PlayerProfileLoader.LoadFromFile(profilePath);

        return await new Pes2021PlayerAnchorFinder(gateway, clock).FindAsync(
            new AttachmentId(attachmentId),
            new ProcessInstanceIdentity(new AttachmentId(attachmentId), processId, null, "PES2021"),
            profile,
            (uint)controlPlayerId,
            regions: null,
            cancellationToken);
    }

    [McpServerTool(Name = "pes2021_scan_players"), Description("Scan the EDIT-base arena and decode every structurally valid player record.")]
    public async Task<PlayerDiscoveryResult> ScanPlayers(
        [Description("The attachment identifier returned by attach_process.")] Guid attachmentId,
        [Description("The control player ID to anchor on.")] int controlPlayerId,
        [Description("Optional process ID for identity propagation.")] int processId = 0,
        [Description("Optional profile path.")] string? profilePath = null,
        CancellationToken cancellationToken = default)
    {
        var profile = string.IsNullOrWhiteSpace(profilePath)
            ? Pes2021PlayerProfileDefaults.GetOrLoad()
            : Pes2021PlayerProfileLoader.LoadFromFile(profilePath);

        return await catalogService.RefreshAsync(
            new AttachmentId(attachmentId),
            new ProcessInstanceIdentity(new AttachmentId(attachmentId), processId, null, "PES2021"),
            profile,
            (uint)controlPlayerId,
            regions: null,
            cancellationToken);
    }

    [McpServerTool(Name = "pes2021_query_player"), Description("Query the in-memory catalog by player ID. Returns ambiguous results when duplicates exist.")]
    public PlayerQueryResult QueryPlayer([Description("Player ID to look up.")] int playerId)
        => queryService.QueryByPlayerId((uint)playerId);

    // --- Family Discovery System Tools ---

    [McpServerTool(Name = "pes2021_discover_player_families"), Description("Discover all player families using the multi-anchor FDS scanner.")]
    public Task<string> DiscoverPlayerFamilies(
        [Description("The attachment identifier returned by attach_process.")] Guid attachmentId,
        [Description("Optional profile path.")] string? profilePath = null,
        [Description("Region policy to use (DefaultPlayerArena, All, IncludeMapped, etc).")] string policy = "DefaultPlayerArena",
        [Description("Maximum bytes to read. 0 = unlimited.")] long maxBytes = 0,
        [Description("Timeout in milliseconds. 0 = unlimited.")] int timeoutMs = 0,
        [Description("Output mode (Summary, Compact, Full, Hits, Coverage).")] string outputMode = "Summary",
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult("Not implemented via MCP yet. Wait for Phase 10 completion.");
    }

    [McpServerTool(Name = "pes2021_inventory_player_hits"), Description("Inventory all hits using the FDS scanner.")]
    public Task<string> InventoryPlayerHits(
        [Description("The attachment identifier returned by attach_process.")] Guid attachmentId,
        [Description("Optional profile path.")] string? profilePath = null,
        [Description("Region policy to use.")] string policy = "DefaultPlayerArena",
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult("Not implemented via MCP yet. Wait for Phase 10 completion.");
    }

    [McpServerTool(Name = "pes2021_compare_player_sessions"), Description("Compare two FDS catalogs.")]
    public Task<string> ComparePlayerSessions(
        [Description("Path to the before catalog.")] string beforeCatalogPath,
        [Description("Path to the after catalog.")] string afterCatalogPath,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult("Not implemented via MCP yet. Wait for Phase 10 completion.");
    }

    [McpServerTool(Name = "pes2021_export_family_catalog"), Description("Export the current FDS catalog to a specific path.")]
    public Task<string> ExportFamilyCatalog(
        [Description("The attachment identifier returned by attach_process.")] Guid attachmentId,
        [Description("Path to save the catalog.")] string outputPath,
        [Description("Optional profile path.")] string? profilePath = null,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult("Not implemented via MCP yet. Wait for Phase 10 completion.");
    }
}