using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Overmem.Abstractions;
using Overmem.Abstractions.Memory;
using Overmem.Abstractions.Processes;
using Overmem.Extensions.Pes2021.Fixtures;
using Overmem.Runtime;

namespace Overmem.Extensions.Pes2021.Players;

/// <summary>
/// Region/block scanner for the EDIT-base player arena. Locates the memory region that
/// contains the validated control-player anchor, derives the record-grid residue from
/// that anchor, and walks the region at the profile stride. The scanner never writes.
/// </summary>
public sealed class Pes2021PlayerRegionScanner
{
    private readonly IProcessMemoryGateway _gateway;
    private readonly ISystemClock _clock;

    public Pes2021PlayerRegionScanner(IProcessMemoryGateway gateway, ISystemClock clock)
    {
        _gateway = gateway;
        _clock = clock;
    }

    public async Task<PlayerDiscoveryResult> ScanAsync(
        AttachmentId attachmentId,
        ProcessInstanceIdentity process,
        Pes2021PlayerProfile profile,
        PlayerSession session,
        IReadOnlyList<MemoryRegionInfo>? regions,
        CancellationToken cancellationToken)
    {
        var collector = new Pes2021PlayerDiscoveryDiagnosticsCollector { CacheDisposition = session.CacheDisposition };

        var allRegions = regions ?? await _gateway.ListRegionsAsync(attachmentId, cancellationToken);
        var (acceptedRegions, rejectedRegions) = FilterRegions(allRegions, profile.RegionFilter);
        var regionDiagnostics = BuildRegionDiagnostics(allRegions, profile.RegionFilter);
        collector.AddRegions(regionDiagnostics);

        var stride = profile.Stride;
        if (!TryParseHex(session.AnchorAddress, out var anchorAddress))
        {
            collector.AddWarning("anchor_missing_or_invalid");
            collector.CacheDisposition = CacheDisposition.Refused;
            return new PlayerDiscoveryResult(session, Array.Empty<DecodedPlayerRecord>(), collector.Build());
        }

        var anchorRegion = acceptedRegions.FirstOrDefault(region =>
            anchorAddress >= region.BaseAddress
            && anchorAddress < checked(region.BaseAddress + region.RegionSize));
        if (anchorRegion is null)
        {
            collector.AddWarning("anchor_region_not_found");
            collector.CacheDisposition = CacheDisposition.Refused;
            return new PlayerDiscoveryResult(session, Array.Empty<DecodedPlayerRecord>(), collector.Build());
        }

        var regionStart = anchorRegion.BaseAddress;
        var regionStop = checked(regionStart + anchorRegion.RegionSize);
        var residue = (anchorAddress - regionStart) % (ulong)stride;
        var firstRecordAddress = checked(regionStart + residue);
        var totalGridSlots = (int)((regionStop - firstRecordAddress) / (ulong)stride);
        var anchorGridSlot = checked((int)((anchorAddress - firstRecordAddress) / (ulong)stride));

        var validBySlot = new Dictionary<int, DecodedPlayerRecord>();
        var invalidHashBySlot = new Dictionary<int, string>();
        var blockRecords = Math.Max(1, profile.RegionFilter.ChunkBytes / stride);

        for (var firstSlot = 0; firstSlot < totalGridSlots; firstSlot += blockRecords)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var count = Math.Min(blockRecords, totalGridSlots - firstSlot);
            var bytesToRequest = checked(count * stride);
            var address = checked(firstRecordAddress + (ulong)(firstSlot * stride));
            var buffer = await ReadBytesAsync(attachmentId, address, bytesToRequest, cancellationToken);
            collector.AddReadCall(bytesToRequest, buffer?.Length ?? 0);
            if (buffer is null || buffer.Length < bytesToRequest)
            {
                collector.AddRejection(PlayerRecordRejectionReasons.PartialRead, count);
                continue;
            }

            for (var local = 0; local < count; local++)
            {
                var slot = firstSlot + local;
                var offset = local * stride;
                var recordAddress = checked(firstRecordAddress + (ulong)(slot * stride));
                var slice = buffer.AsSpan(offset, stride);
                var parse = Pes2021PlayerRecordParser.TryParse(slice, slot, recordAddress, profile);
                if (parse.Success && parse.Record is not null
                    && Pes2021PlayerRecordValidator.Validate(parse.Record, profile).Accept)
                {
                    validBySlot[slot] = parse.Record;
                }
                else
                {
                    invalidHashBySlot[slot] = Convert.ToHexString(SHA256.HashData(slice)).ToLowerInvariant();
                }
            }
        }

        if (!validBySlot.ContainsKey(anchorGridSlot))
        {
            collector.AddWarning("anchor_record_not_valid_on_discovered_grid");
            collector.CacheDisposition = CacheDisposition.Refused;
            return new PlayerDiscoveryResult(session, Array.Empty<DecodedPlayerRecord>(), collector.Build());
        }

        var firstPopulatedSlot = anchorGridSlot;
        while (firstPopulatedSlot > 0 && validBySlot.ContainsKey(firstPopulatedSlot - 1)) firstPopulatedSlot--;
        var lastPopulatedSlot = anchorGridSlot;
        while (lastPopulatedSlot + 1 < totalGridSlots && validBySlot.ContainsKey(lastPopulatedSlot + 1)) lastPopulatedSlot++;

        var players = new List<DecodedPlayerRecord>(lastPopulatedSlot - firstPopulatedSlot + 1);
        for (var slot = firstPopulatedSlot; slot <= lastPopulatedSlot; slot++)
        {
            players.Add(validBySlot[slot] with { RecordIndex = slot - firstPopulatedSlot });
        }

        var firstEmptySlot = lastPopulatedSlot + 1;
        string? emptyRecordSha256 = null;
        var emptyReservedSlots = 0;
        if (firstEmptySlot < totalGridSlots && invalidHashBySlot.TryGetValue(firstEmptySlot, out var firstEmptyHash))
        {
            emptyRecordSha256 = firstEmptyHash;
            for (var slot = firstEmptySlot; slot < totalGridSlots; slot++)
            {
                if (!invalidHashBySlot.TryGetValue(slot, out var hash)
                    || !string.Equals(hash, firstEmptyHash, StringComparison.Ordinal))
                {
                    break;
                }

                emptyReservedSlots++;
            }
        }

        var theoreticalSlots = players.Count + emptyReservedSlots;
        var arenaBaseAddress = checked(firstRecordAddress + (ulong)(firstPopulatedSlot * stride));
        var arenaStopAddress = checked(arenaBaseAddress + (ulong)(theoreticalSlots * stride));
        var duplicateIds = players.GroupBy(player => player.PlayerId).Count(group => group.Count() > 1);

        collector.AddRecords(theoreticalSlots, players.Count, emptyReservedSlots);
        collector.AddRejection("EMPTY_RESERVED_SLOT", emptyReservedSlots);
        collector.AddDuplicatePlayerIds(duplicateIds);

        var updatedSession = session with
        {
            ArenaBaseAddress = FormatHex(arenaBaseAddress),
            ArenaStopAddress = FormatHex(arenaStopAddress),
            ValidatedAtUtc = _clock.UtcNow,
        };

        var diagnostics = collector.Build();
        return new PlayerDiscoveryResult(updatedSession, players, diagnostics)
        {
            ArenaCoverage = new PlayerArenaCoverage(
                RegionBaseAddress: FormatHex(regionStart),
                RegionStopAddress: FormatHex(regionStop),
                FirstRecordAddress: FormatHex(firstRecordAddress),
                ArenaBaseAddress: FormatHex(arenaBaseAddress),
                ArenaStopAddress: FormatHex(arenaStopAddress),
                RecordStride: stride,
                AnchorSlotIndex: anchorGridSlot - firstPopulatedSlot,
                PopulatedSlots: players.Count,
                EmptyReservedSlots: emptyReservedSlots,
                TheoreticalSlots: theoreticalSlots,
                UnaccountedSlots: 0,
                EmptyRecordSha256: emptyRecordSha256,
                BoundaryClassification: arenaStopAddress < regionStop ? "NON_PLAYER_DATA" : "REGION_END")
        };
    }

    private async Task<byte[]?> ReadBytesAsync(AttachmentId attachmentId, ulong address, int size, CancellationToken cancellationToken)
    {
        try
        {
            var result = await _gateway.ReadAsync(
                new ReadMemoryRequest(attachmentId, address, MemoryValueKind.Bytes, size),
                cancellationToken);
            return Convert.FromHexString(result.Value);
        }
        catch (OperationCanceledException) { throw; }
        catch { return null; }
    }

    private static (List<MemoryRegionInfo> Accepted, List<MemoryRegionInfo> Rejected) FilterRegions(
        IReadOnlyList<MemoryRegionInfo> regions,
        Pes2021PlayerRegionFilter filter)
    {
        var accepted = new List<MemoryRegionInfo>();
        var rejected = new List<MemoryRegionInfo>();
        foreach (var region in regions)
        {
            if (filter.RequireReadable && !region.IsReadable) { rejected.Add(region); continue; }
            if (filter.RequireWritable && !region.IsWritable) { rejected.Add(region); continue; }
            if (!filter.AllowExecutable && region.IsExecutable) { rejected.Add(region); continue; }

            var stateOk = false;
            foreach (var state in filter.States)
            {
                if (string.Equals(region.State, state, StringComparison.OrdinalIgnoreCase)) { stateOk = true; break; }
            }
            if (!stateOk) { rejected.Add(region); continue; }

            var typeOk = false;
            foreach (var type in filter.Types)
            {
                if (string.Equals(region.Type, type, StringComparison.OrdinalIgnoreCase)) { typeOk = true; break; }
            }
            if (!typeOk) { rejected.Add(region); continue; }

            accepted.Add(region);
        }
        return (accepted, rejected);
    }

    private static IReadOnlyList<PlayerRegionDiagnostic> BuildRegionDiagnostics(
        IReadOnlyList<MemoryRegionInfo> regions,
        Pes2021PlayerRegionFilter filter)
    {
        var list = new List<PlayerRegionDiagnostic>(regions.Count);
        foreach (var region in regions)
        {
            var decision = "accepted";
            string? reason = null;
            if (filter.RequireReadable && !region.IsReadable) { decision = "rejected"; reason = "not_readable"; }
            else if (filter.RequireWritable && !region.IsWritable) { decision = "rejected"; reason = "not_writable"; }
            else if (!filter.AllowExecutable && region.IsExecutable) { decision = "rejected"; reason = "executable_disallowed"; }
            list.Add(new PlayerRegionDiagnostic(
                BaseAddress: $"0x{region.BaseAddress:X}",
                StopAddress: $"0x{region.BaseAddress + region.RegionSize:X}",
                Size: region.RegionSize,
                State: region.State,
                Type: region.Type,
                Protection: region.Protection,
                Readable: region.IsReadable,
                Writable: region.IsWritable,
                Executable: region.IsExecutable,
                Decision: decision,
                Reason: reason));
        }
        return list;
    }

    private static bool TryParseHex(string text, out ulong value)
    {
        if (text.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            return ulong.TryParse(text.AsSpan(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out value);
        }

        return ulong.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
    }

    private static string FormatHex(ulong value)
        => string.Create(CultureInfo.InvariantCulture, $"0x{value:X}");
}
