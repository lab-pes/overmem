using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Overmem.Abstractions;
using Overmem.Abstractions.Processes;
using Overmem.Extensions.Pes2021.Players;
using Overmem.Extensions.Pes2021.Players.FamilyDiscovery;
using Overmem.Extensions.Pes2021.Fixtures;
using Overmem.Abstractions.Memory;

namespace Overmem.Extensions.Pes2021.Players.FamilyDiscovery;

public sealed class Pes2021FamilyDiscoveryService
{
    private readonly IProcessMemoryGateway _gateway;
    private readonly ILogger<Pes2021FamilyDiscoveryService> _logger;
    private readonly Pes2021PlayerAnchorFinder _anchorFinder;

    public Pes2021FamilyDiscoveryService(
        IProcessMemoryGateway gateway, 
        ILogger<Pes2021FamilyDiscoveryService> logger,
        Pes2021PlayerAnchorFinder anchorFinder)
    {
        _gateway = gateway;
        _logger = logger;
        _anchorFinder = anchorFinder;
    }

    public async Task<string> DiscoverFamiliesAsync(
        AttachmentId attachmentId,
        ProcessInstanceIdentity identity,
        Pes2021PlayerProfile profile,
        uint controlPlayerId,
        string policyName,
        long maxBytes,
        int timeoutMs,
        string outputMode,
        CancellationToken cancellationToken)
    {
        var scanner = new MultiAnchorScanner(_gateway);
        
        if (!Enum.TryParse<RegionPolicy>(policyName, true, out var policy))
            policy = RegionPolicy.DefaultPlayerArena;

        var budget = new FamilyScanBudget(
            maxBytes == 0 ? long.MaxValue : maxBytes, 
            int.MaxValue, 
            int.MaxValue, 
            int.MaxValue, 
            timeoutMs);

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        if (timeoutMs > 0) cts.CancelAfter(timeoutMs);

        var anchorResult = await _anchorFinder.FindAsync(attachmentId, identity, profile, controlPlayerId, null, cts.Token);
        if (anchorResult.AnchorAddress == null)
            throw new Exception($"Control player {controlPlayerId} not found in EDIT arena.");

        var address = Convert.ToUInt64(anchorResult.AnchorAddress, 16);
        var req = new ReadMemoryRequest(attachmentId, address, MemoryValueKind.Bytes, profile.Stride);
        var bytesResp = await _gateway.ReadAsync(req, cts.Token);

        var rawBytes = Convert.FromHexString(bytesResp.Value);
        var parseResult = Pes2021PlayerRecordParser.TryParse(rawBytes, 0, address, profile);
        if (!parseResult.Success || parseResult.Record == null)
            throw new Exception($"Failed to parse control player at {address:X}.");

        var controlPlayers = new[] { parseResult.Record };
        var fingerprints = FingerprintBuilder.Build(profile, controlPlayers);
        
        var scanResult = await scanner.ScanAsync(attachmentId, fingerprints, profile, policy, budget, null, cts.Token);

        var clusteringEngine = new FamilyClusteringEngine();
        var families = clusteringEngine.Cluster(scanResult.Hits, profile.Stride);

        // Scan for pointers and relations for each family
        var pointerScanner = new PointerFamilyScanner(_gateway);
        var relationScanner = new PlayerTeamRelationScanner(_gateway);
        
        int totalPointers = 0;
        int totalRelations = 0;

        foreach (var fam in families)
        {
            totalPointers += await pointerScanner.ScanForPointersAsync(attachmentId, fam, cts.Token);
            totalRelations += await relationScanner.ScanForRelationsAsync(attachmentId, fam, cts.Token);
        }

        var newDiagnostics = scanResult.Diagnostics with 
        {
            FamiliesDiscovered = families.Count,
            AmbiguousFamilies = families.Count(f => f.Class == FamilyResultClass.AmbiguousFamily),
            TotalPointersFound = totalPointers,
            TotalTeamRelationsFound = totalRelations
        };

        var result = new FamilyDiscoveryResult(
            families,
            scanResult.Hits,
            scanResult.Hits.Where(h => !h.Accepted).ToList(),
            newDiagnostics);
        
        var formatMode = OutputMode.Summary;
        if (Enum.TryParse<OutputMode>(outputMode, true, out var parsedMode))
            formatMode = parsedMode;

        return FamilyOutputFormatter.Format(result, formatMode);
    }

    public async Task<string> InventoryHitsAsync(
        AttachmentId attachmentId,
        ProcessInstanceIdentity identity,
        Pes2021PlayerProfile profile,
        uint controlPlayerId,
        string policyName,
        CancellationToken cancellationToken)
    {
        return await DiscoverFamiliesAsync(attachmentId, identity, profile, controlPlayerId, policyName, 0, 0, "Hits", cancellationToken);
    }

    public Task<string> CompareSessionsAsync(
        AttachmentId attachmentId,
        string beforeCatalogPath,
        string afterCatalogPath,
        CancellationToken cancellationToken)
    {
        var catalogBefore = new FamilyCatalog(beforeCatalogPath, attachmentId);
        var catalogAfter = new FamilyCatalog(afterCatalogPath, attachmentId);

        return CompareInternalAsync(catalogBefore, catalogAfter, cancellationToken);
    }

    private async Task<string> CompareInternalAsync(FamilyCatalog before, FamilyCatalog after, CancellationToken ct)
    {
        var beforeResult = await before.LoadAsync(ct);
        var afterResult = await after.LoadAsync(ct);

        if (beforeResult == null || afterResult == null)
            return "Error: Could not load one or both catalogs.";

        var comparator = new SessionComparator();
        var report = comparator.Compare(beforeResult, afterResult);

        var sw = new StringWriter();
        sw.WriteLine($"=== Session Comparison ===");
        sw.WriteLine($"Persistent Families: {report.PersistentFamilies.Count}");
        sw.WriteLine($"New Families: {report.NewFamilies.Count}");
        sw.WriteLine($"Disappeared Families: {report.DisappearedFamilies.Count}");
        sw.WriteLine($"Player Changes (Moves/Adds/Removes): {report.PlayerChanges.Count}");
        
        if (report.PlayerChanges.Any())
        {
            sw.WriteLine("\n--- Changes ---");
            foreach (var change in report.PlayerChanges)
            {
                sw.WriteLine($"Player {change.PlayerId}: {change.ChangeType} (0x{change.OldAddress:X} -> 0x{change.NewAddress:X})");
            }
        }

        return sw.ToString();
    }

    public async Task<string> ExportCatalogAsync(
        AttachmentId attachmentId,
        ProcessInstanceIdentity identity,
        Pes2021PlayerProfile profile,
        uint controlPlayerId,
        string outputPath,
        CancellationToken cancellationToken)
    {
        var scanner = new MultiAnchorScanner(_gateway);
        
        var anchorResult = await _anchorFinder.FindAsync(attachmentId, identity, profile, controlPlayerId, null, cancellationToken);
        if (anchorResult.AnchorAddress == null)
            throw new Exception($"Control player {controlPlayerId} not found.");

        var address = Convert.ToUInt64(anchorResult.AnchorAddress, 16);
        var req = new ReadMemoryRequest(attachmentId, address, MemoryValueKind.Bytes, profile.Stride);
        var bytesResp = await _gateway.ReadAsync(req, cancellationToken);
        var rawBytes = Convert.FromHexString(bytesResp.Value);
        var parseResult = Pes2021PlayerRecordParser.TryParse(rawBytes, 0, address, profile);
        if (!parseResult.Success || parseResult.Record == null)
            throw new Exception($"Failed to parse control player at {address:X}.");
        
        var controlPlayers = new[] { parseResult.Record };
        var fingerprints = FingerprintBuilder.Build(profile, controlPlayers);
        var scanResult = await scanner.ScanAsync(attachmentId, fingerprints, profile, RegionPolicy.DefaultPlayerArena, FamilyScanBudget.Unlimited, null, cancellationToken);
        
        var clusteringEngine = new FamilyClusteringEngine();
        var families = clusteringEngine.Cluster(scanResult.Hits, profile.Stride);
        
        var newDiagnostics = scanResult.Diagnostics with 
        {
            FamiliesDiscovered = families.Count,
            AmbiguousFamilies = families.Count(f => f.Class == FamilyResultClass.AmbiguousFamily)
        };

        var result = new FamilyDiscoveryResult(
            families,
            scanResult.Hits,
            scanResult.Hits.Where(h => !h.Accepted).ToList(),
            newDiagnostics);
        
        var catalog = new FamilyCatalog(outputPath, attachmentId);
        await catalog.SaveAsync(result, cancellationToken);
        
        return $"Catalog exported to {outputPath} with {result.Families.Count} families and {result.AllHits.Count} hits.";
    }
}
