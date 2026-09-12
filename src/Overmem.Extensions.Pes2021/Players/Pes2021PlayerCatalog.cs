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
        => await RefreshAsync(
            attachmentId,
            process,
            profile,
            controlPlayerId,
            selectedAnchorAddress: null,
            regions,
            cancellationToken);

    /// <summary>
    /// Performs discovery within the single region containing an explicitly selected
    /// anchor candidate. The finder must independently rediscover the exact same address
    /// inside that region; arbitrary addresses are refused.
    /// </summary>
    public async Task<PlayerDiscoveryResult> RefreshAsync(
        AttachmentId attachmentId,
        ProcessInstanceIdentity process,
        Pes2021PlayerProfile profile,
        uint controlPlayerId,
        ulong? selectedAnchorAddress,
        IReadOnlyList<MemoryRegionInfo>? regions,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<MemoryRegionInfo>? discoveryRegions = regions;
        if (selectedAnchorAddress.HasValue)
        {
            var allRegions = regions ?? await _gateway.ListRegionsAsync(attachmentId, cancellationToken);
            var selectedRegion = allRegions.SingleOrDefault(region =>
                selectedAnchorAddress.Value >= region.BaseAddress
                && selectedAnchorAddress.Value < checked(region.BaseAddress + region.RegionSize));
            if (selectedRegion is null)
            {
                throw new InvalidOperationException(
                    $"Selected player anchor 0x{selectedAnchorAddress.Value:X} is not inside a current process region.");
            }

            discoveryRegions = new[] { selectedRegion };
        }

        var anchorResult = await _anchorFinder.FindAsync(
            attachmentId, process, profile, controlPlayerId, discoveryRegions, cancellationToken);
        if (selectedAnchorAddress.HasValue)
        {
            var expected = $"0x{selectedAnchorAddress.Value:X}";
            if (anchorResult.Ambiguous
                || !string.Equals(anchorResult.AnchorAddress, expected, System.StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    $"Selected player anchor {expected} was not independently rediscovered as the unique validated anchor in its region.");
            }

            anchorResult = anchorResult with
            {
                Session = anchorResult.Session with { CacheDisposition = CacheDisposition.ProvidedAddress }
            };
        }

        var scanResult = await _regionScanner.ScanAsync(
            attachmentId, process, profile, anchorResult.Session, discoveryRegions, cancellationToken);
        _catalog.Replace(scanResult);
        return scanResult;
    }

    public Pes2021PlayerCatalog Catalog => _catalog;
}
