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
}