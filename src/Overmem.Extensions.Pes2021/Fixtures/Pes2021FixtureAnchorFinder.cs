using System.Buffers.Binary;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Overmem.Abstractions;
using Overmem.Abstractions.Memory;
using Overmem.Abstractions.Processes;
using Overmem.Runtime;

namespace Overmem.Extensions.Pes2021.Fixtures;

/// <summary>
/// Discovers the PES 2021 calendar anchor for a given competition/team pair. The finder
/// only walks regions that pass the profile's strict filter (committed, private, readable,
/// writable, non-executable by default), scans with <c>stride - 1</c> byte overlap so a hit
/// cannot straddle two chunks, and scores every candidate so the caller can audit the
/// result. It never falls back to "the lowest address wins" on ties; ambiguous ties produce
/// <see cref="FixtureAnchorResult.AnchorAddress"/> equal to <c>null</c>.
/// </summary>
public sealed class Pes2021FixtureAnchorFinder
{
    private readonly IProcessMemoryGateway _gateway;
    private readonly ISystemClock _clock;

    public Pes2021FixtureAnchorFinder(IProcessMemoryGateway gateway, ISystemClock clock)
    {
        _gateway = gateway;
        _clock = clock;
    }

    /// <summary>
    /// Runs anchor discovery for the given competition and team. The result always contains
    /// the diagnostic counters and the candidate list; <see cref="FixtureAnchorResult.AnchorAddress"/>
    /// is set only when exactly one candidate survives scoring.
    /// </summary>
    public async Task<FixtureAnchorResult> FindAsync(
        AttachmentId attachmentId,
        ProcessInstanceIdentity process,
        Pes2021FixtureProfile profile,
        CompetitionId competitionId,
        ushort teamId,
        ushort? teamLiga,
        IReadOnlyList<MemoryRegionInfo>? regions,
        IReadOnlyDictionary<TeamKey, int>? teamKeyFrequencies,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        var allRegions = regions ?? await _gateway.ListRegionsAsync(attachmentId, cancellationToken);
        var (acceptedRegions, rejectedRegions) = FilterRegions(allRegions, profile.RegionFilter);
        var regionsEnumerated = allRegions.Count;
        var regionsAccepted = acceptedRegions.Count;
        var regionsRejected = rejectedRegions.Count;
        var regionDiagnostics = BuildRegionDiagnostics(allRegions, profile.RegionFilter);
        var readCalls = 0;
        var bytesRequested = 0UL;
        var bytesRead = 0UL;

        var requestedKey = teamLiga.HasValue
            ? new TeamKey(teamId, teamLiga.Value)
            : (TeamKey?)null;

        var competitionBytes = EncodeUInt16LittleEndian(competitionId.Value);
        var stride = profile.Stride;

        var candidates = new Dictionary<ulong, AnchorCandidate>();
        var rejectionReasons = new Dictionary<string, int>();

        foreach (var region in acceptedRegions)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var regionStart = region.BaseAddress;
            var regionStop = checked(region.BaseAddress + region.RegionSize);
            if (region.RegionSize < (ulong)stride)
            {
                continue;
            }

            var chunkBytes = Math.Min(profile.RegionFilter.ChunkBytes, (int)Math.Min((long)region.RegionSize, int.MaxValue));
            if (chunkBytes <= 0)
            {
                continue;
            }

            var overlap = stride - 1;
            var cursor = regionStart;
            byte[] previousTail = [];
            while (cursor < regionStop)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var remaining = (long)regionStop - (long)cursor;
                var primaryLength = (int)Math.Min((long)chunkBytes, remaining);
                var bytesToRequest = (int)Math.Min((long)(chunkBytes + overlap), remaining);
                if (bytesToRequest <= 0)
                {
                    break;
                }

                bytesRequested += (ulong)bytesToRequest;
                var buffer = await ReadBytesAsync(attachmentId, cursor, bytesToRequest, cancellationToken);
                readCalls++;
                if (buffer is null)
                {
                    Increment(rejectionReasons, FixtureRejectionReasons.PartialRead);
                    break;
                }

                bytesRead += (ulong)buffer.Length;
                var combined = Combine(previousTail, buffer);
                var searchWindow = combined.Length - (stride - 1);
                if (searchWindow <= 0)
                {
                    previousTail = combined;
                    cursor = checked(cursor + (ulong)primaryLength);
                    if (primaryLength == 0)
                    {
                        break;
                    }

                    continue;
                }

                for (var offset = 0; offset < searchWindow; offset++)
                {
                    if (combined[offset] != competitionBytes[0]
                        || combined[offset + 1] != competitionBytes[1])
                    {
                        continue;
                    }

                    var recordAddress = checked(cursor + (ulong)offset - (ulong)previousTail.Length);
                    if (candidates.ContainsKey(recordAddress))
                    {
                        continue;
                    }

                    var slice = new byte[stride];
                    Array.Copy(combined, offset, slice, 0, stride);
                    var candidate = await ScoreCandidateAsync(
                        attachmentId,
                        region,
                        recordAddress,
                        competitionId,
                        requestedKey,
                        teamId,
                        slice,
                        profile,
                        cancellationToken);
                    if (candidate is not null)
                    {
                        candidates[recordAddress] = candidate;
                    }
                    else
                    {
                        Increment(rejectionReasons, FixtureRejectionReasons.TeamMismatch);
                    }
                }

                var overlapStart = Math.Max(0, combined.Length - overlap);
                previousTail = new byte[overlap];
                Array.Copy(combined, overlapStart, previousTail, 0, overlap);
                cursor = checked(cursor + (ulong)primaryLength);
                if (primaryLength == 0)
                {
                    break;
                }
            }
        }

        var ordered = candidates.Values
            .OrderByDescending(candidate => candidate.Score)
            .ThenBy(candidate => candidate.Address, StringComparer.Ordinal)
            .ToList();

        var winner = PickWinner(ordered, profile.AnchorValidation, rejectionReasons);
        var confidence = ComputeConfidence(winner, ordered, profile.AnchorValidation, rejectionReasons);
        stopwatch.Stop();

        var validationSampleSha256 = winner is null
            ? string.Empty
            : await HashAnchorWindowAsync(attachmentId, winner.Address, profile, cancellationToken);

        var session = new CalendarSession(
            process,
            profile.ProfileId,
            profile.ProfileVersion,
            profile.Sha256,
            profile.Stride,
            profile.Calendar.RecordLimit,
            CalendarArrayBaseAddress: TryComputeArrayBase(winner, profile),
            CompetitionBlockBaseAddress: winner?.Address ?? string.Empty,
            AnchorAddress: winner?.Address ?? string.Empty,
            AnchorIndex: 0,
            ValidationSampleSha256: validationSampleSha256,
            ValidatedAtUtc: _clock.UtcNow,
            CacheDisposition: CacheDisposition.Discovered);

        var stageDurationMs = new Dictionary<string, double>
        {
            ["anchor_discovery"] = stopwatch.Elapsed.TotalMilliseconds,
        };

        var diagnostics = new ExtractionDiagnostics(
            CacheDisposition: CacheDisposition.Discovered,
            RegionsEnumerated: regionsEnumerated,
            RegionsAccepted: regionsAccepted,
            RegionsRejected: regionsRejected,
            BytesRequested: bytesRequested,
            BytesRead: bytesRead,
            ReadCalls: readCalls,
            BlocksRead: readCalls,
            RecordsDecoded: candidates.Count,
            RecordsAccepted: candidates.Count,
            RecordsRejected: 0,
            RejectionReasons: rejectionReasons,
            StageDurationMs: stageDurationMs,
            Regions: regionDiagnostics,
            Warnings: []);

        return new FixtureAnchorResult(
            Session: session,
            CompetitionId: competitionId,
            RequestedTeamKey: requestedKey,
            RequestedTeamId: teamId,
            AnchorAddress: winner?.Address,
            CompetitionBlockBaseAddress: winner?.Address,
            CalendarArrayBaseAddress: TryComputeArrayBase(winner, profile),
            AnchorIndex: 0,
            Confidence: confidence,
            Candidates: ordered,
            Diagnostics: diagnostics);
    }

    private async Task<AnchorCandidate?> ScoreCandidateAsync(
        AttachmentId attachmentId,
        MemoryRegionInfo region,
        ulong candidateAddress,
        CompetitionId competitionId,
        TeamKey? requestedKey,
        ushort teamId,
        byte[] recordBytes,
        Pes2021FixtureProfile profile,
        CancellationToken cancellationToken)
    {
        var parseResult = Pes2021CalendarRecordParser.TryParse(recordBytes, 0, candidateAddress, profile);
        if (!parseResult.Success || parseResult.Record is null)
        {
            return null;
        }

        var parsed = parseResult.Record;
        if (parsed.CompetitionId.Value != competitionId.Value)
        {
            return null;
        }

        if (parsed.Home.TeamId != teamId && parsed.Away.TeamId != teamId)
        {
            return null;
        }

        if (requestedKey.HasValue)
        {
            var key = requestedKey.Value;
            var homeMatches = parsed.Home.TeamId == key.TeamId && parsed.Home.TeamLiga == key.TeamLiga;
            var awayMatches = parsed.Away.TeamId == key.TeamId && parsed.Away.TeamLiga == key.TeamLiga;
            if (!homeMatches && !awayMatches)
            {
                return null;
            }
        }

        var reasons = new List<string>
        {
            "competition_match",
            "team_match",
        };

        if (requestedKey.HasValue)
        {
            reasons.Add("teamLiga_match");
        }

        var score = 0;
        var recordsAfter = profile.AnchorValidation.RecordsAfter;
        var recordsBefore = profile.AnchorValidation.RecordsBefore;
        var minRun = profile.AnchorValidation.MinimumPlausibleRun;
        var minComp = profile.AnchorValidation.MinimumCompetitionRun;
        var stride = profile.Stride;

        var competitionRun = 1;
        for (var offset = stride; offset <= recordsAfter * stride; offset += stride)
        {
            var slice = await ReadSliceAsync(attachmentId, candidateAddress + (ulong)offset, stride, cancellationToken);
            if (slice is null)
            {
                reasons.Add($"partial_read_aft_{offset / stride}");
                break;
            }

            var inner = Pes2021CalendarRecordParser.TryParse(slice, 0, candidateAddress + (ulong)offset, profile);
            if (!inner.Success || inner.Record is null)
            {
                break;
            }

            if (inner.Record.CompetitionId.Value == competitionId.Value)
            {
                competitionRun++;
                score++;
            }
            else if (competitionRun < minComp)
            {
                reasons.Add("competition_run_too_short");
                return null;
            }
            else
            {
                reasons.Add("competition_run_satisfied");
                break;
            }
        }

        var forwardRun = 0;
        for (var offset = stride; offset <= recordsAfter * stride; offset += stride)
        {
            var slice = await ReadSliceAsync(attachmentId, candidateAddress + (ulong)offset, stride, cancellationToken);
            if (slice is null)
            {
                break;
            }

            var inner = Pes2021CalendarRecordParser.TryParse(slice, 0, candidateAddress + (ulong)offset, profile);
            if (!inner.Success || inner.Record is null)
            {
                break;
            }

            if (DateOnly.TryParseExact(
                    string.Create(CultureInfo.InvariantCulture, $"{inner.Record.Year:D4}-{inner.Record.Month:D2}-{inner.Record.Day:D2}"),
                    "yyyy-MM-dd",
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.None,
                    out _))
            {
                forwardRun++;
            }
            else
            {
                break;
            }
        }

        var backwardRun = 0;
        for (var offset = stride; offset <= recordsBefore * stride; offset += stride)
        {
            if (candidateAddress < (ulong)offset)
            {
                break;
            }

            var slice = await ReadSliceAsync(attachmentId, candidateAddress - (ulong)offset, stride, cancellationToken);
            if (slice is null)
            {
                break;
            }

            var inner = Pes2021CalendarRecordParser.TryParse(slice, 0, candidateAddress - (ulong)offset, profile);
            if (!inner.Success || inner.Record is null)
            {
                break;
            }

            if (DateOnly.TryParseExact(
                    string.Create(CultureInfo.InvariantCulture, $"{inner.Record.Year:D4}-{inner.Record.Month:D2}-{inner.Record.Day:D2}"),
                    "yyyy-MM-dd",
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.None,
                    out _))
            {
                backwardRun++;
            }
            else
            {
                break;
            }
        }

        if (forwardRun + backwardRun < minRun - 1)
        {
            reasons.Add("stride_sequence_too_short");
            return null;
        }

        score += 3; // anchor exact
        score += 2; // competition run observed
        if (forwardRun > 0)
        {
            score++;
        }

        if (backwardRun > 0)
        {
            score++;
        }

        var regionStop = checked(region.BaseAddress + region.RegionSize);
        var regionCrossing = (candidateAddress + (ulong)((recordsAfter + 1) * stride)) > regionStop;
        var partialRead = false;

        var regionStopAddress = string.Create(CultureInfo.InvariantCulture, $"0x{regionStop:X}");

        return new AnchorCandidate(
            Address: string.Create(CultureInfo.InvariantCulture, $"0x{candidateAddress:X}"),
            Score: score,
            Reasons: reasons,
            PlausibleRunForward: forwardRun,
            PlausibleRunBackward: backwardRun,
            CompetitionRun: competitionRun,
            PartialRead: partialRead,
            RegionCrossing: regionCrossing);
    }

    private async Task<string> HashAnchorWindowAsync(
        AttachmentId attachmentId,
        string anchorAddressHex,
        Pes2021FixtureProfile profile,
        CancellationToken cancellationToken)
    {
        if (!TryParseHex(anchorAddressHex, out var address))
        {
            return string.Empty;
        }

        var windowBytes = profile.Stride * 2;
        var first = await ReadSliceAsync(attachmentId, address, profile.Stride, cancellationToken);
        var second = await ReadSliceAsync(attachmentId, checked(address + (ulong)profile.Stride), profile.Stride, cancellationToken);
        var combined = new byte[windowBytes];
        if (first is not null)
        {
            Buffer.BlockCopy(first, 0, combined, 0, Math.Min(first.Length, profile.Stride));
        }

        if (second is not null)
        {
            Buffer.BlockCopy(second, 0, combined, profile.Stride, Math.Min(second.Length, profile.Stride));
        }

        return Convert.ToHexString(SHA256.HashData(combined)).ToLowerInvariant();
    }

    private async Task<byte[]?> ReadSliceAsync(AttachmentId attachmentId, ulong address, int stride, CancellationToken cancellationToken)
    {
        try
        {
            var result = await _gateway.ReadAsync(
                new ReadMemoryRequest(attachmentId, address, Overmem.Abstractions.Memory.MemoryValueKind.Bytes, stride),
                cancellationToken);
            return Convert.FromHexString(result.Value);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            return null;
        }
    }

    private async Task<byte[]?> ReadBytesAsync(AttachmentId attachmentId, ulong address, int size, CancellationToken cancellationToken)
    {
        try
        {
            var result = await _gateway.ReadAsync(
                new ReadMemoryRequest(attachmentId, address, Overmem.Abstractions.Memory.MemoryValueKind.Bytes, size),
                cancellationToken);
            return Convert.FromHexString(result.Value);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            return null;
        }
    }

    private static AnchorCandidate? PickWinner(
        List<AnchorCandidate> ordered,
        Pes2021AnchorValidation validation,
        Dictionary<string, int> rejectionReasons)
    {
        if (ordered.Count == 0)
        {
            Increment(rejectionReasons, "no_candidate");
            return null;
        }

        var top = ordered[0];
        var tied = ordered.FindAll(candidate => candidate.Score == top.Score);
        if (tied.Count > 1)
        {
            Increment(rejectionReasons, "ambiguous_tie");
            return null;
        }

        if (top.Score < validation.MediumScore)
        {
            Increment(rejectionReasons, "score_below_medium");
            return null;
        }

        return top;
    }

    private static DiscoveryConfidence ComputeConfidence(
        AnchorCandidate? winner,
        IReadOnlyList<AnchorCandidate> ordered,
        Pes2021AnchorValidation validation,
        Dictionary<string, int> rejectionReasons)
    {
        var maxScore = ordered.Count == 0 ? 0 : ordered.Max(candidate => candidate.Score);
        if (winner is null)
        {
            return new DiscoveryConfidence(
                Level: "low",
                Score: maxScore,
                MaxScore: maxScore,
                Reasons: ["no_winner"]);
        }

        var level = winner.Score >= validation.HighScore
            ? "high"
            : winner.Score >= validation.MediumScore
                ? "medium"
                : "low";

        return new DiscoveryConfidence(level, winner.Score, maxScore, winner.Reasons);
    }

    private static string? TryComputeArrayBase(AnchorCandidate? winner, Pes2021FixtureProfile profile)
    {
        if (winner is null)
        {
            return null;
        }

        if (profile.Normalization.Strategy == NormalizationStrategy.CompetitionBlockOnly)
        {
            return null;
        }

        if (!TryParseHex(winner.Address, out var anchorAddress))
        {
            return null;
        }

        var startIndex = profile.Normalization.KnownSeasonStartIndex;
        if (startIndex is null || startIndex.Value <= 0)
        {
            return null;
        }

        var baseAddress = checked(anchorAddress - (ulong)startIndex.Value * (ulong)profile.Stride);
        return string.Create(CultureInfo.InvariantCulture, $"0x{baseAddress:X}");
    }

    private static (List<MemoryRegionInfo> Accepted, List<MemoryRegionInfo> Rejected) FilterRegions(
        IReadOnlyList<MemoryRegionInfo> regions,
        Pes2021RegionFilter filter)
    {
        var accepted = new List<MemoryRegionInfo>();
        var rejected = new List<MemoryRegionInfo>();
        foreach (var region in regions)
        {
            if (filter.RequireReadable && !region.IsReadable)
            {
                rejected.Add(region);
                continue;
            }

            if (filter.RequireWritable && !region.IsWritable)
            {
                rejected.Add(region);
                continue;
            }

            if (!filter.AllowExecutable && region.IsExecutable)
            {
                rejected.Add(region);
                continue;
            }

            var stateOk = false;
            foreach (var state in filter.States)
            {
                if (string.Equals(region.State, state, StringComparison.OrdinalIgnoreCase))
                {
                    stateOk = true;
                    break;
                }
            }

            if (!stateOk)
            {
                rejected.Add(region);
                continue;
            }

            var typeOk = false;
            foreach (var type in filter.Types)
            {
                if (string.Equals(region.Type, type, StringComparison.OrdinalIgnoreCase))
                {
                    typeOk = true;
                    break;
                }
            }

            if (!typeOk)
            {
                rejected.Add(region);
                continue;
            }

            accepted.Add(region);
        }

        return (accepted, rejected);
    }

    private static IReadOnlyList<RegionDiagnostic> BuildRegionDiagnostics(
        IReadOnlyList<MemoryRegionInfo> regions,
        Pes2021RegionFilter filter)
    {
        var list = new List<RegionDiagnostic>(regions.Count);
        foreach (var region in regions)
        {
            var decision = "accepted";
            string? reason = null;
            if (filter.RequireReadable && !region.IsReadable)
            {
                decision = "rejected";
                reason = "not_readable";
            }
            else if (filter.RequireWritable && !region.IsWritable)
            {
                decision = "rejected";
                reason = "not_writable";
            }
            else if (!filter.AllowExecutable && region.IsExecutable)
            {
                decision = "rejected";
                reason = "executable_disallowed";
            }
            else
            {
                var stateOk = false;
                foreach (var state in filter.States)
                {
                    if (string.Equals(region.State, state, StringComparison.OrdinalIgnoreCase))
                    {
                        stateOk = true;
                        break;
                    }
                }

                if (!stateOk)
                {
                    decision = "rejected";
                    reason = "state_mismatch";
                }
            }

            list.Add(new RegionDiagnostic(
                BaseAddress: string.Create(CultureInfo.InvariantCulture, $"0x{region.BaseAddress:X}"),
                StopAddress: string.Create(CultureInfo.InvariantCulture, $"0x{(region.BaseAddress + region.RegionSize):X}"),
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

    private static void Increment(Dictionary<string, int> counters, string key)
    {
        if (counters.TryGetValue(key, out var current))
        {
            counters[key] = current + 1;
        }
        else
        {
            counters[key] = 1;
        }
    }

    private static byte[] EncodeUInt16LittleEndian(ushort value)
    {
        Span<byte> span = stackalloc byte[2];
        BinaryPrimitives.WriteUInt16LittleEndian(span, value);
        return span.ToArray();
    }

    private static byte[] Combine(byte[] head, byte[] tail)
    {
        var combined = new byte[head.Length + tail.Length];
        Buffer.BlockCopy(head, 0, combined, 0, head.Length);
        Buffer.BlockCopy(tail, 0, combined, head.Length, tail.Length);
        return combined;
    }

    private static bool TryParseHex(string text, out ulong value)
    {
        if (text.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            return ulong.TryParse(text.AsSpan(2), System.Globalization.NumberStyles.HexNumber, CultureInfo.InvariantCulture, out value);
        }

        return ulong.TryParse(text, System.Globalization.NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
    }
}
