using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Diagnostics;
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
/// Discovers the PES 2021 player-record anchor for a given control player ID. The finder
/// only walks regions that pass the profile's strict filter (committed, private, readable,
/// writable, non-executable by default), scans with <c>stride - 1</c> byte overlap so a
/// hit cannot straddle two chunks, and scores every candidate so the caller can audit the
/// result. It never falls back to "the lowest address wins" on ties; ambiguous ties
/// produce <see cref="PlayerAnchorResult.Ambiguous"/> = true.
/// </summary>
public sealed class Pes2021PlayerAnchorFinder
{
    private readonly IProcessMemoryGateway _gateway;
    private readonly ISystemClock _clock;

    public Pes2021PlayerAnchorFinder(IProcessMemoryGateway gateway, ISystemClock clock)
    {
        _gateway = gateway;
        _clock = clock;
    }

    public async Task<PlayerAnchorResult> FindAsync(
        AttachmentId attachmentId,
        ProcessInstanceIdentity process,
        Pes2021PlayerProfile profile,
        uint playerId,
        IReadOnlyList<MemoryRegionInfo>? regions,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        var collector = new Pes2021PlayerDiscoveryDiagnosticsCollector();

        var allRegions = regions ?? await _gateway.ListRegionsAsync(attachmentId, cancellationToken);
        var (acceptedRegions, rejectedRegions) = FilterRegions(allRegions, profile.RegionFilter);
        var regionDiagnostics = BuildRegionDiagnostics(allRegions, profile.RegionFilter);
        collector.AddRegions(regionDiagnostics);

        var stride = profile.Stride;
        var playerIdBytes = EncodeUInt32LittleEndian(playerId);
        var candidates = new List<PlayerAnchorCandidate>();
        var seenCandidateAddresses = new HashSet<ulong>();
        var playerIdOffset = profile.RecordLayout.Fields.Single(f => f.Name == "playerId").Offset;

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

                // Search the ID byte pattern at every byte. The record array is not
                // guaranteed to start at the VirtualQuery region base (live EDIT uses
                // regionBase + 0x10), so stepping from the region base by stride loses
                // the complete arena.
                var searchAt = 0;
                while (searchAt <= buffer.Length - playerIdBytes.Length)
                {
                    var hit = Array.IndexOf(buffer, playerIdBytes[0], searchAt);
                    if (hit < 0 || hit > buffer.Length - playerIdBytes.Length) break;
                    searchAt = hit + 1;

                    if (buffer[hit + 1] != playerIdBytes[1]
                        || buffer[hit + 2] != playerIdBytes[2]
                        || buffer[hit + 3] != playerIdBytes[3])
                    {
                        continue;
                    }

                    var recordStart = hit - playerIdOffset;
                    if (recordStart < 0 || recordStart >= primaryLength || recordStart + stride > buffer.Length)
                    {
                        continue;
                    }

                    var recordAddress = checked(cursor + (ulong)recordStart);
                    if (!seenCandidateAddresses.Add(recordAddress)) continue;

                    var slice = new byte[stride];
                    Array.Copy(buffer, recordStart, slice, 0, stride);

                    var candidate = await ScoreCandidateAsync(
                        attachmentId,
                        slice,
                        recordAddress,
                        regionStart,
                        regionStop,
                        playerId,
                        profile,
                        cancellationToken);
                    if (candidate is not null) candidates.Add(candidate);
                    else collector.AddRejection(PlayerRecordRejectionReasons.NeighborStrideMismatch);
                }

                cursor = checked(cursor + (ulong)primaryLength);
                if (primaryLength == 0) break;
            }
        }

        stopwatch.Stop();
        using (collector.BeginStage("anchor_discovery"))
        {
            // stage already started
        }

        var ordered = candidates
            .OrderByDescending(c => c.Score)
            .ThenBy(c => c.Address, StringComparer.Ordinal)
            .ToList();

        PlayerAnchorCandidate? winner = null;
        bool ambiguous = false;
        if (ordered.Count > 0)
        {
            var top = ordered[0];
            if (top.Score >= profile.AnchorValidation.MediumScore)
            {
                var ties = ordered.Where(c => c.Score == top.Score).ToList();
                if (ties.Count == 1)
                {
                    winner = top;
                }
                else
                {
                    ambiguous = true;
                    collector.AddRejection("ambiguous_tie");
                }
            }
            else
            {
                collector.AddRejection("score_below_medium");
            }
        }
        else
        {
            collector.AddRejection("no_candidate");
        }

        var confidence = ComputeConfidence(winner, ordered, profile.AnchorValidation);
        collector.CacheDisposition = winner is null ? CacheDisposition.Refused : CacheDisposition.Discovered;

        MemoryRegionInfo? anchorRegion = null;
        if (winner is not null && ParseHex(winner.Address, out var winnerAddress))
        {
            anchorRegion = acceptedRegions.FirstOrDefault(region =>
                winnerAddress >= region.BaseAddress
                && winnerAddress < checked(region.BaseAddress + region.RegionSize));
        }

        var arenaBase = anchorRegion?.BaseAddress ?? 0UL;
        var arenaStop = anchorRegion is null
            ? 0UL
            : checked(anchorRegion.BaseAddress + anchorRegion.RegionSize);
        var validationSampleSha256 = winner is null
            ? string.Empty
            : await HashAnchorWindowAsync(attachmentId, winner.Address, profile, cancellationToken);

        var arenaBaseHex = string.Create(CultureInfo.InvariantCulture, $"0x{arenaBase:X}");
        var arenaStopHex = string.Create(CultureInfo.InvariantCulture, $"0x{arenaStop:X}");
        var anchorAddressHex = winner?.Address ?? string.Empty;

        int anchorIndex = 0;
        if (winner is not null && arenaBase > 0 && ParseHex(winner.Address, out var winnerAddr) && winnerAddr >= arenaBase)
        {
            anchorIndex = (int)((winnerAddr - arenaBase) / (ulong)stride);
        }

        var session = new PlayerSession(
            new PlayerProcessInstanceIdentity(attachmentId, process.ProcessId, process.ProcessStartedAtUtc, process.ProcessName),
            profile.ProfileId,
            profile.ProfileVersion,
            profile.Sha256,
            stride,
            arenaBaseHex,
            arenaStopHex,
            anchorAddressHex,
            playerId,
            winner?.Fingerprint ?? string.Empty,
            validationSampleSha256,
            _clock.UtcNow,
            collector.CacheDisposition);

        var stageDurationMs = new Dictionary<string, double>
        {
            ["anchor_discovery"] = stopwatch.Elapsed.TotalMilliseconds,
        };

        var diagnostics = collector.Build();
        var finalDiagnostics = diagnostics with
        {
            StageDurationMs = new Dictionary<string, double>(stageDurationMs, StringComparer.Ordinal),
        };

        return new PlayerAnchorResult(
            Session: session,
            PlayerId: playerId,
            AnchorAddress: winner?.Address,
            AnchorIndex: anchorIndex,
            Ambiguous: ambiguous,
            Confidence: confidence,
            Candidates: ordered,
            Diagnostics: finalDiagnostics);
    }

    private async Task<PlayerAnchorCandidate?> ScoreCandidateAsync(
        AttachmentId attachmentId,
        byte[] recordBytes,
        ulong candidateAddress,
        ulong regionStart,
        ulong regionStop,
        uint expectedPlayerId,
        Pes2021PlayerProfile profile,
        CancellationToken cancellationToken)
    {
        var parse = Pes2021PlayerRecordParser.TryParse(recordBytes, 0, candidateAddress, profile);
        if (!parse.Success || parse.Record is null) return null;
        if (parse.Record.PlayerId != expectedPlayerId) return null;

        var validation = Pes2021PlayerRecordValidator.Validate(parse.Record, profile);
        if (!validation.Accept) return null;

        var reasons = new List<string> { "player_id_match", "cheap_validation_passed", "validator_accepted" };
        var score = 5 + validation.Score;
        if (!string.IsNullOrWhiteSpace(parse.Record.PlayerName))
        {
            score += 2;
            reasons.Add("player_name_present");
        }

        var forward = await CountPlausibleNeighborsAsync(
            attachmentId, candidateAddress, +1, regionStart, regionStop,
            profile.AnchorValidation.RecordsAfter, profile, cancellationToken);
        var backward = await CountPlausibleNeighborsAsync(
            attachmentId, candidateAddress, -1, regionStart, regionStop,
            profile.AnchorValidation.RecordsBefore, profile, cancellationToken);
        var runLength = 1 + forward + backward;
        if (runLength < profile.AnchorValidation.MinimumRun)
        {
            return null;
        }

        score += Math.Min(4, forward + backward);
        reasons.Add("neighbor_stride_confirmed");

        return new PlayerAnchorCandidate(
            Address: string.Create(CultureInfo.InvariantCulture, $"0x{candidateAddress:X}"),
            PlayerId: parse.Record.PlayerId,
            Fingerprint: parse.Record.PlayerName ?? string.Empty,
            Score: score,
            Reasons: reasons,
            PlausibleRunForward: forward,
            PlausibleRunBackward: backward);
    }

    private async Task<int> CountPlausibleNeighborsAsync(
        AttachmentId attachmentId,
        ulong anchorAddress,
        int direction,
        ulong regionStart,
        ulong regionStop,
        int limit,
        Pes2021PlayerProfile profile,
        CancellationToken cancellationToken)
    {
        var count = 0;
        for (var index = 1; index <= limit; index++)
        {
            ulong address;
            var distance = checked((ulong)(index * profile.Stride));
            if (direction < 0)
            {
                if (anchorAddress < regionStart + distance) break;
                address = anchorAddress - distance;
            }
            else
            {
                address = checked(anchorAddress + distance);
                if (address + (ulong)profile.Stride > regionStop) break;
            }

            var bytes = await ReadBytesAsync(attachmentId, address, profile.Stride, cancellationToken);
            if (bytes is null || bytes.Length < profile.Stride) break;
            var parsed = Pes2021PlayerRecordParser.TryParse(bytes, index, address, profile);
            if (!parsed.Success || parsed.Record is null
                || !Pes2021PlayerRecordValidator.Validate(parsed.Record, profile).Accept)
            {
                break;
            }

            count++;
        }

        return count;
    }

    private async Task<string> HashAnchorWindowAsync(
        AttachmentId attachmentId,
        string anchorAddressHex,
        Pes2021PlayerProfile profile,
        CancellationToken cancellationToken)
    {
        if (!ParseHex(anchorAddressHex, out var address)) return string.Empty;

        try
        {
            var first = await ReadBytesAsync(attachmentId, address, profile.Stride, cancellationToken);
            var second = await ReadBytesAsync(attachmentId, checked(address + (ulong)profile.Stride), profile.Stride, cancellationToken);
            var combined = new byte[profile.Stride * 2];
            if (first is not null) Buffer.BlockCopy(first, 0, combined, 0, Math.Min(first.Length, profile.Stride));
            if (second is not null) Buffer.BlockCopy(second, 0, combined, profile.Stride, Math.Min(second.Length, profile.Stride));
            return Convert.ToHexString(SHA256.HashData(combined)).ToLowerInvariant();
        }
        catch
        {
            return string.Empty;
        }
    }

    private async Task<byte[]?> ReadRawAsync(AttachmentId attachmentId, ulong address, int size, CancellationToken cancellationToken)
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

    private static PlayerAnchorConfidence ComputeConfidence(
        PlayerAnchorCandidate? winner,
        IReadOnlyList<PlayerAnchorCandidate> ordered,
        Pes2021PlayerAnchorValidation validation)
    {
        var maxScore = ordered.Count == 0 ? 0 : ordered.Max(c => c.Score);
        if (winner is null)
        {
            return new PlayerAnchorConfidence("low", maxScore, maxScore, new[] { "no_winner" });
        }

        var level = winner.Score >= validation.HighScore
            ? "high"
            : winner.Score >= validation.MediumScore ? "medium" : "low";
        return new PlayerAnchorConfidence(level, winner.Score, maxScore, winner.Reasons);
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
            else
            {
                var stateOk = false;
                foreach (var state in filter.States)
                {
                    if (string.Equals(region.State, state, StringComparison.OrdinalIgnoreCase)) { stateOk = true; break; }
                }
                if (!stateOk) { decision = "rejected"; reason = "state_mismatch"; }
            }

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

    private static byte[] EncodeUInt32LittleEndian(uint value)
    {
        Span<byte> span = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(span, value);
        return span.ToArray();
    }

    private static bool ParseHex(string text, out ulong value)
    {
        if (text.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            return ulong.TryParse(text.AsSpan(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out value);
        }

        return ulong.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
    }

    private static ulong ParseHex(string text)
        => ParseHex(text, out var v) ? v : ulong.MaxValue;
}
