using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Overmem.Abstractions;
using Overmem.Abstractions.Memory;
using Overmem.Abstractions.Processes;
using Overmem.Extensions.Pes2021.Fixtures;
using Overmem.Runtime;

namespace Overmem.Extensions.Pes2021.Players;

/// <summary>
/// Maintains the in-memory list of decoded players for the current process. Holds the
/// latest <see cref="PlayerDiscoveryResult"/> so callers can re-query without re-reading
/// memory. Thread-safe for producers and consumers via a lock.
/// </summary>
public sealed class Pes2021PlayerCatalog
{
    private readonly object _lock = new();
    private List<DecodedPlayerRecord> _players = new();
    private PlayerDiscoveryResult? _result;

    public void Replace(PlayerDiscoveryResult result)
    {
        lock (_lock)
        {
            _players = new List<DecodedPlayerRecord>(result.Players);
            _result = result;
        }
    }

    public IReadOnlyList<DecodedPlayerRecord> Snapshot()
    {
        lock (_lock) return _players.AsReadOnly();
    }

    public PlayerDiscoveryResult? Result
    {
        get { lock (_lock) return _result; }
    }

    public void Clear()
    {
        lock (_lock)
        {
            _players = new List<DecodedPlayerRecord>();
            _result = null;
        }
    }

    public int Count
    {
        get { lock (_lock) return _players.Count; }
    }
}

/// <summary>
/// Top-level orchestrator that wires the anchor finder, region scanner, and session
/// cache into a single <see cref="Pes2021PlayerCatalog"/>.
/// </summary>
public sealed class Pes2021PlayerCatalogService
{
    private readonly Pes2021PlayerCatalog _catalog;
    private readonly Pes2021PlayerAnchorFinder _anchorFinder;
    private readonly Pes2021PlayerRegionScanner _regionScanner;
    private readonly Pes2021PlayerSessionCache _sessionCache;
    private readonly IProcessMemoryGateway _gateway;
    private readonly ISystemClock _clock;

    public Pes2021PlayerCatalogService(
        Pes2021PlayerCatalog catalog,
        Pes2021PlayerAnchorFinder anchorFinder,
        Pes2021PlayerRegionScanner regionScanner,
        Pes2021PlayerSessionCache sessionCache,
        IProcessMemoryGateway gateway,
        ISystemClock clock)
    {
        _catalog = catalog;
        _anchorFinder = anchorFinder;
        _regionScanner = regionScanner;
        _sessionCache = sessionCache;
        _gateway = gateway;
        _clock = clock;
    }

    /// <summary>
    /// Performs an end-to-end discovery: anchor by the control player ID, scan the
    /// arena, and replace the catalog. The cache key includes the profile identity
    /// so a profile bump forces rediscovery.
    /// </summary>
    public async Task<PlayerDiscoveryResult> RefreshAsync(
        AttachmentId attachmentId,
        ProcessInstanceIdentity process,
        Pes2021PlayerProfile profile,
        uint controlPlayerId,
        IReadOnlyList<MemoryRegionInfo>? regions,
        CancellationToken cancellationToken)
    {
        var anchorResult = await _anchorFinder.FindAsync(
            attachmentId, process, profile, controlPlayerId, regions, cancellationToken);
        var scanResult = await _regionScanner.ScanAsync(
            attachmentId, process, profile, anchorResult.Session, regions, cancellationToken);
        _catalog.Replace(scanResult);
        return scanResult;
    }

    public Pes2021PlayerCatalog Catalog => _catalog;
}