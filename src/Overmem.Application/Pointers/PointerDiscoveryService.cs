using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Overmem.Abstractions;
using Overmem.Abstractions.Memory;
using Overmem.Abstractions.Processes;
using Overmem.Runtime;
using Overmem.Runtime.Attachments;
using Overmem.Runtime.Diagnostics;

namespace Overmem.Application.Pointers;

public sealed class PointerDiscoveryService : IPointerDiscoveryService
{
    private const int ChunkSize = 64 * 1024;

    private readonly ISystemClock _clock;
    private readonly IProcessMemoryGateway _gateway;
    private readonly ILogger<PointerDiscoveryService> _logger;
    private readonly IOperationJournal _operationJournal;
    private readonly IAttachmentSessionRegistry _sessionRegistry;

    public PointerDiscoveryService(
        IProcessMemoryGateway gateway,
        IAttachmentSessionRegistry sessionRegistry,
        IOperationJournal operationJournal,
        ISystemClock clock,
        ILogger<PointerDiscoveryService> logger)
    {
        _gateway = gateway;
        _sessionRegistry = sessionRegistry;
        _operationJournal = operationJournal;
        _clock = clock;
        _logger = logger;
    }

    public PointerDiscoveryService(IProcessMemoryGateway gateway, IAttachmentSessionRegistry sessionRegistry)
        : this(
            gateway,
            sessionRegistry,
            new InMemoryOperationJournal(),
            SystemClock.Instance,
            NullLogger<PointerDiscoveryService>.Instance)
    {
    }

    public async Task<DiscoverPointersResult> DiscoverAsync(DiscoverPointersRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.MaxDepth <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(request), "MaxDepth must be greater than zero.");
        }

        if (request.MaxOffset < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(request), "MaxOffset must be zero or greater.");
        }

        if (request.MaxResults <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(request), "MaxResults must be greater than zero.");
        }

        var pointerSize = ResolvePointerSize(request.AttachmentId);
        var alignment = request.Alignment > 0 ? request.Alignment : pointerSize;
        if (alignment <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(request), "Alignment must be greater than zero.");
        }

        var modules = await _gateway.ListModulesAsync(request.AttachmentId, cancellationToken);
        var regions = await _gateway.ListRegionsAsync(request.AttachmentId, cancellationToken);
        var candidates = new List<PointerDiscoveryCandidate>();
        var frontier = new List<DiscoveryPath> { new(request.TargetAddress, []) };
        var seen = new HashSet<string>(StringComparer.Ordinal);

        for (var depth = 1; depth <= request.MaxDepth && frontier.Count > 0 && candidates.Count < request.MaxResults; depth++)
        {
            frontier = await DiscoverLevelAsync(
                request.AttachmentId,
                frontier,
                pointerSize,
                alignment,
                request.MaxOffset,
                request.MaxResults,
                regions,
                modules,
                seen,
                candidates,
                cancellationToken);
        }

        var filteredCandidates = candidates
            .Where(candidate => request.BaseModuleName is null || string.Equals(candidate.ModuleName, request.BaseModuleName, StringComparison.OrdinalIgnoreCase))
            .ToArray();

        var enrichedCandidates = request.RevalidateCandidates
            ? await RevalidateCandidatesAsync(request.AttachmentId, request.TargetAddress, filteredCandidates, cancellationToken)
            : filteredCandidates;

        var scoredCandidates = enrichedCandidates
            .Select(candidate => candidate with { Score = ComputeScore(candidate) })
            .ToArray();

        var orderedCandidates = scoredCandidates
            .OrderByDescending(candidate => candidate.Score)
            .ToArray();

        Record("discover_pointers", "Succeeded", request.AttachmentId, $"Target=0x{request.TargetAddress:X}; Results={orderedCandidates.Length}");
        _logger.LogInformation("Discovered {ResultCount} pointer candidates for attachment {AttachmentId} targeting 0x{TargetAddress:X}.", orderedCandidates.Length, request.AttachmentId, request.TargetAddress);

        return new DiscoverPointersResult(
            request.TargetAddress,
            request.MaxDepth,
            request.MaxOffset,
            alignment,
            orderedCandidates.Length,
            orderedCandidates);
    }

    private async Task<List<DiscoveryPath>> DiscoverLevelAsync(
        AttachmentId attachmentId,
        IReadOnlyList<DiscoveryPath> frontier,
        int pointerSize,
        int alignment,
        long maxOffset,
        int maxResults,
        IReadOnlyList<MemoryRegionInfo> regions,
        IReadOnlyList<ModuleInfo> modules,
        HashSet<string> seen,
        List<PointerDiscoveryCandidate> candidates,
        CancellationToken cancellationToken)
    {
        var nextFrontier = new List<DiscoveryPath>();
        var nextSeen = new HashSet<string>(StringComparer.Ordinal);
        var overlap = Math.Max(pointerSize - 1, 0);

        foreach (var region in regions.Where(region => region.IsReadable && region.RegionSize >= (ulong)pointerSize))
        {
            cancellationToken.ThrowIfCancellationRequested();

            for (ulong cursor = 0; cursor < region.RegionSize && candidates.Count < maxResults; cursor += (ulong)ChunkSize)
            {
                var remaining = region.RegionSize - cursor;
                var primaryLength = (int)Math.Min((ulong)ChunkSize, remaining);
                var bytesToRead = (int)Math.Min((ulong)(ChunkSize + overlap), remaining);

                byte[] buffer;
                try
                {
                    buffer = await ReadBytesAsync(attachmentId, region.BaseAddress + cursor, bytesToRead, cancellationToken);
                }
                catch (InvalidOperationException)
                {
                    continue;
                }

                if (buffer.Length < pointerSize)
                {
                    continue;
                }

                var scanLimit = Math.Min(primaryLength, buffer.Length - pointerSize + 1);
                for (var position = 0; position < scanLimit && candidates.Count < maxResults; position++)
                {
                    var absoluteAddress = region.BaseAddress + cursor + (ulong)position;
                    if (alignment > 1 && absoluteAddress % (ulong)alignment != 0)
                    {
                        continue;
                    }

                    var pointedAddress = ReadPointer(buffer, position, pointerSize);
                    foreach (var path in frontier)
                    {
                        if (!TryComputeOffset(path.TargetAddress, pointedAddress, maxOffset, out var offset))
                        {
                            continue;
                        }

                        var offsets = Prepend(offset, path.Offsets);
                        var signature = CreateSignature(absoluteAddress, offsets);
                        if (!seen.Add(signature))
                        {
                            continue;
                        }

                        candidates.Add(CreateCandidate(absoluteAddress, offsets, modules));
                        if (nextSeen.Add(signature))
                        {
                            nextFrontier.Add(new DiscoveryPath(absoluteAddress, offsets));
                        }

                        if (candidates.Count >= maxResults)
                        {
                            break;
                        }
                    }
                }
            }
        }

        return nextFrontier;
    }

    private static string CreateSignature(ulong baseAddress, IReadOnlyList<long> offsets)
        => $"{baseAddress:X16}:{string.Join(',', offsets)}";

    private static PointerDiscoveryCandidate CreateCandidate(ulong baseAddress, IReadOnlyList<long> offsets, IReadOnlyList<ModuleInfo> modules)
    {
        var module = modules.FirstOrDefault(candidateModule =>
            candidateModule.BaseAddress <= baseAddress &&
            baseAddress < candidateModule.BaseAddress + (ulong)candidateModule.Size);

        if (module is null)
        {
            return new PointerDiscoveryCandidate(baseAddress, offsets);
        }

        return new PointerDiscoveryCandidate(
            baseAddress,
            offsets,
            module.Name,
            checked((long)(baseAddress - module.BaseAddress)));
    }

    private async Task<PointerDiscoveryCandidate[]> RevalidateCandidatesAsync(
        AttachmentId attachmentId,
        ulong targetAddress,
        IReadOnlyList<PointerDiscoveryCandidate> candidates,
        CancellationToken cancellationToken)
    {
        var validatedCandidates = new List<PointerDiscoveryCandidate>(candidates.Count);
        foreach (var candidate in candidates)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var result = await _gateway.ResolvePointerAsync(new ResolvePointerRequest(attachmentId, candidate.BaseAddress, candidate.Offsets), cancellationToken);
                if (result.ResolvedAddress != targetAddress)
                {
                    continue;
                }

                validatedCandidates.Add(candidate with
                {
                    IsValidated = true,
                    ResolvedAddress = result.ResolvedAddress,
                });
            }
            catch (Exception exception) when (exception is InvalidOperationException or KeyNotFoundException)
            {
                _logger.LogDebug(exception, "Pointer candidate 0x{BaseAddress:X} could not be revalidated.", candidate.BaseAddress);
            }
        }

        return validatedCandidates.ToArray();
    }

    private void Record(string operationName, string outcome, AttachmentId attachmentId, string? detail = null)
        => _operationJournal.Record(new OperationLogEntry(
            Guid.NewGuid(),
            operationName,
            outcome,
            _clock.UtcNow,
            attachmentId.ToString(),
            detail));

    private async Task<byte[]> ReadBytesAsync(AttachmentId attachmentId, ulong address, int size, CancellationToken cancellationToken)
    {
        var result = await _gateway.ReadAsync(new ReadMemoryRequest(attachmentId, address, MemoryValueKind.Bytes, size), cancellationToken);
        return Convert.FromHexString(result.Value);
    }

    private int ResolvePointerSize(AttachmentId attachmentId)
    {
        var session = _sessionRegistry.ListActive().FirstOrDefault(candidate => candidate.AttachmentId == attachmentId);
        if (session is null)
        {
            throw new KeyNotFoundException($"Attachment '{attachmentId}' is not tracked by the runtime session registry.");
        }

        return session.Architecture switch
        {
            ProcessArchitecture.X86 => sizeof(uint),
            ProcessArchitecture.X64 => sizeof(ulong),
            _ => throw new InvalidOperationException("The target process architecture is unknown.")
        };
    }

    private static ulong ReadPointer(byte[] buffer, int position, int pointerSize)
        => pointerSize == sizeof(uint)
            ? BitConverter.ToUInt32(buffer, position)
            : BitConverter.ToUInt64(buffer, position);

    private static IReadOnlyList<long> Prepend(long offset, IReadOnlyList<long> offsets)
    {
        var values = new long[offsets.Count + 1];
        values[0] = offset;
        for (var index = 0; index < offsets.Count; index++)
        {
            values[index + 1] = offsets[index];
        }

        return values;
    }

    private static bool TryComputeOffset(ulong targetAddress, ulong pointedAddress, long maxOffset, out long offset)
    {
        if (maxOffset < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxOffset));
        }

        if (targetAddress >= pointedAddress)
        {
            var difference = targetAddress - pointedAddress;
            if (difference > (ulong)maxOffset)
            {
                offset = 0;
                return false;
            }

            offset = checked((long)difference);
            return true;
        }

        var negativeDifference = pointedAddress - targetAddress;
        if (negativeDifference > (ulong)maxOffset)
        {
            offset = 0;
            return false;
        }

        offset = -checked((long)negativeDifference);
        return true;
    }

    private sealed record DiscoveryPath(ulong TargetAddress, IReadOnlyList<long> Offsets);

    /// <summary>
    /// Heuristic score (higher = better candidate):
    ///  +10000 if the candidate chain was successfully revalidated
    ///  +5000  if the base falls inside a known module (static pointer)
    ///  -1000  per chain depth level (prefer shorter chains)
    ///  -1     per 8 bytes of total absolute offset (capped at -999, prefer small offsets)
    ///  +500   if all offsets are divisible by 4 (common struct member alignment)
    /// </summary>
    private static int ComputeScore(PointerDiscoveryCandidate candidate)
    {
        var score = 0;
        if (candidate.IsValidated) score += 10_000;
        if (candidate.ModuleName is not null) score += 5_000;
        score -= candidate.Offsets.Count * 1_000;
        var totalAbsOffset = candidate.Offsets.Sum(offset => Math.Abs(offset));
        score -= (int)Math.Min(999, totalAbsOffset / 8);
        if (candidate.Offsets.Count > 0 && candidate.Offsets.All(offset => offset % 4 == 0)) score += 500;
        return score;
    }
}