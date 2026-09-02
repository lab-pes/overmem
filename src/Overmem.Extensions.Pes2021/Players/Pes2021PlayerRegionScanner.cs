using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Overmem.Abstractions;
using Overmem.Abstractions.Memory;
using Overmem.Abstractions.Processes;
using Overmem.Extensions.Pes2021.Fixtures;
using Overmem.Runtime;

namespace Overmem.Extensions.Pes2021.Players;

/// <summary>
/// Region/block scanner for the EDIT-base player arena. Walks every region that passes
/// the profile filter, scans with <c>stride - 1</c> byte overlap so a hit cannot straddle
/// two chunks, decodes each candidate with <see cref="Pes2021PlayerRecordParser"/>, and
/// aggregates the survivors plus diagnostics. The scanner never writes.
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
        var players = new List<DecodedPlayerRecord>();
        var duplicateIds = new Dictionary<uint, int>();
        var seenAddresses = new HashSet<ulong>();

        foreach (var region in acceptedRegions)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var regionStart = region.BaseAddress;
            var regionStop = checked(region.BaseAddress + region.RegionSize);
            if (region.RegionSize < (ulong)stride) continue;

            var chunkBytes = (int)Math.Min(profile.RegionFilter.ChunkBytes, (long)region.RegionSize);
            if (chunkBytes <= 0) continue;

            var overlap = stride - 1;
            var cursor = regionStart;
            byte[] previousTail = Array.Empty<byte>();

            while (cursor < regionStop)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var remaining = (long)regionStop - (long)cursor;
                var primaryLength = (int)Math.Min(chunkBytes, remaining);
                var bytesToRequest = (int)Math.Min((long)(chunkBytes + overlap), remaining);
                if (bytesToRequest <= 0) break;

                collector.AddReadCall(bytesToRequest, bytesToRequest);
                var buffer = await ReadBytesAsync(attachmentId, cursor, bytesToRequest, cancellationToken);
                if (buffer is null)
                {
                    collector.AddRejection(PlayerRecordRejectionReasons.PartialRead);
                    break;
                }

                var combined = Combine(previousTail, buffer);
                var recordCount = combined.Length / stride;
                for (var i = 0; i < recordCount; i++)
                {
                    var offset = i * stride;
                    var recordAddress = checked(cursor + (ulong)offset - (ulong)previousTail.Length);
                    if (!seenAddresses.Add(recordAddress)) { continue; }

                    var slice = new byte[stride];
                    Array.Copy(combined, offset, slice, 0, stride);

                    var parse = Pes2021PlayerRecordParser.TryParse(slice, players.Count, recordAddress, profile);
                    collector.AddRecords(1, parse.Success ? 1 : 0, parse.Success ? 0 : 1);
                    if (parse.Success && parse.Record is not null)
                    {
                        players.Add(parse.Record);
                        if (!duplicateIds.ContainsKey(parse.Record.PlayerId)) duplicateIds[parse.Record.PlayerId] = 0;
                        duplicateIds[parse.Record.PlayerId]++;
                    }
                    else if (parse.RejectionReason is not null)
                    {
                        collector.AddRejection(parse.RejectionReason);
                    }
                }

                var overlapStart = Math.Max(0, combined.Length - overlap);
                previousTail = new byte[overlap];
                Array.Copy(combined, overlapStart, previousTail, 0, overlap);
                cursor = checked(cursor + (ulong)primaryLength);
                if (primaryLength == 0) break;
            }
        }

        collector.AddDuplicatePlayerIds(duplicateIds.Count(kvp => kvp.Value > 1));

        var diagnostics = collector.Build();
        return new PlayerDiscoveryResult(session, players, diagnostics);
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

    private static byte[] Combine(byte[] head, byte[] tail)
    {
        var combined = new byte[head.Length + tail.Length];
        Buffer.BlockCopy(head, 0, combined, 0, head.Length);
        Buffer.BlockCopy(tail, 0, combined, head.Length, tail.Length);
        return combined;
    }
}