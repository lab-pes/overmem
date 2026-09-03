using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Overmem.Abstractions;
using Overmem.Abstractions.Memory;
using Overmem.Abstractions.Processes;

namespace Overmem.Extensions.Pes2021.Players.FamilyDiscovery;

/// <summary>
/// Procura fingerprints de múltiplos controles em uma única passagem pela memória.
/// Diferente do scanner antigo, não restringe alinhamento ao inicio do stride e não usa ReadAsync 
/// individualmente, operando totalmente em blocos para performance e tolerância a falhas.
/// </summary>
public sealed class MultiAnchorScanner
{
    private readonly IProcessMemoryGateway _gateway;

    public MultiAnchorScanner(IProcessMemoryGateway gateway)
    {
        _gateway = gateway;
    }

    public async Task<MultiAnchorScanResult> ScanAsync(
        AttachmentId attachmentId,
        FingerprintSet fingerprints,
        Pes2021PlayerProfile profile,
        RegionPolicy policy,
        FamilyScanBudget budget,
        IReadOnlyList<MemoryRegionInfo>? regions,
        CancellationToken cancellationToken)
    {
        var allRegions = regions ?? await _gateway.ListRegionsAsync(attachmentId, cancellationToken);
        var (acceptedRegions, _) = RegionPolicyFilter.Filter(allRegions, policy);
        var regionDiagnostics = RegionPolicyFilter.BuildDiagnostics(allRegions, policy).ToList();

        var stride = profile.Stride;
        var hits = new List<FamilyHit>();
        var seenAddresses = new HashSet<ulong>();

        var regionsEnumerated = allRegions.Count;
        var regionsExamined = acceptedRegions.Count;
        var regionsSkipped = allRegions.Count - acceptedRegions.Count;
        ulong bytesRequested = 0;
        ulong bytesRead = 0;
        ulong bytesSkippedUnreadable = 0;

        int regionsProcessedCount = 0;

        foreach (var region in acceptedRegions)
        {
            if (budget.MaxRegions > 0 && regionsProcessedCount >= budget.MaxRegions)
                break;

            cancellationToken.ThrowIfCancellationRequested();

            var regionStart = region.BaseAddress;
            var regionStop = checked(region.BaseAddress + region.RegionSize);
            
            // Chunk size
            var chunkBytes = profile.RegionFilter.ChunkBytes;
            if (chunkBytes <= 0) chunkBytes = 1 << 20;

            var overlap = stride - 1;
            var cursor = regionStart;
            byte[] previousTail = Array.Empty<byte>();

            while (cursor < regionStop)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (budget.MaxBytes > 0 && (long)bytesRequested >= budget.MaxBytes)
                    goto LimitReached;

                var remaining = (long)regionStop - (long)cursor;
                var primaryLength = (int)Math.Min(chunkBytes, remaining);
                var bytesToRequest = (int)Math.Min((long)(chunkBytes + overlap), remaining);
                
                if (bytesToRequest <= 0) break;

                bytesRequested += (ulong)bytesToRequest;
                var buffer = await ReadBytesAsync(attachmentId, cursor, bytesToRequest, cancellationToken);
                
                if (buffer != null)
                {
                    bytesRead += (ulong)buffer.Length;
                    var combined = Combine(previousTail, buffer);
                    var newHits = ProcessChunk(combined, cursor, previousTail.Length, fingerprints, profile, seenAddresses);
                    hits.AddRange(newHits);

                    var overlapStart = Math.Max(0, combined.Length - overlap);
                    previousTail = new byte[overlap];
                    Array.Copy(combined, overlapStart, previousTail, 0, overlap);
                }
                else
                {
                    bytesSkippedUnreadable += (ulong)bytesToRequest;
                    previousTail = Array.Empty<byte>(); // quebrou a contiguidade
                }

                cursor = checked(cursor + (ulong)primaryLength);
                if (primaryLength == 0) break;
            }

            regionsProcessedCount++;
        }

LimitReached:
        var acceptedHits = hits.Count(h => h.Accepted);
        var rejectedHits = hits.Count(h => !h.Accepted);

        var diagnostics = new FamilyDiscoveryDiagnostics(
            RegionsEnumerated: regionsEnumerated,
            RegionsExamined: regionsExamined,
            RegionsSkipped: regionsSkipped,
            BytesRequested: bytesRequested,
            BytesRead: bytesRead,
            BytesSkippedUnreadable: bytesSkippedUnreadable,
            TotalHits: hits.Count,
            AcceptedHits: acceptedHits,
            RejectedHits: rejectedHits,
            FamiliesDiscovered: 0, 
            AmbiguousFamilies: 0,
            RejectionReasons: new Dictionary<string, int>(),
            StageDurationMs: new Dictionary<string, double>(),
            Regions: regionDiagnostics);

        return new MultiAnchorScanResult(hits, diagnostics);
    }

    private List<FamilyHit> ProcessChunk(
        byte[] chunk, 
        ulong cursor, 
        int previousTailLength, 
        FingerprintSet set, 
        Pes2021PlayerProfile profile,
        HashSet<ulong> seenAddresses)
    {
        var newHits = new List<FamilyHit>();
        var stride = profile.Stride;

        if (chunk.Length < stride)
            return newHits;

        var playerIdOffset = profile.RecordLayout.Fields.Single(f => f.Name == "playerId").Offset;

        // Itera por cada offset possível no chunk onde caiba um stride completo
        for (var offset = 0; offset <= chunk.Length - stride; offset++)
        {
            var absoluteAddress = checked(cursor + (ulong)offset - (ulong)previousTailLength);

            if (seenAddresses.Contains(absoluteAddress))
                continue;

            // Busca IDs
            for (var fpIndex = 0; fpIndex < set.Fingerprints.Count; fpIndex++)
            {
                var fp = set.Fingerprints[fpIndex];
                var idStart = offset + playerIdOffset;

                // Match rápido de Player ID (4 bytes LE)
                if (chunk[idStart] == fp.IdBytes[0] &&
                    chunk[idStart + 1] == fp.IdBytes[1] &&
                    chunk[idStart + 2] == fp.IdBytes[2] &&
                    chunk[idStart + 3] == fp.IdBytes[3])
                {
                    seenAddresses.Add(absoluteAddress);
                    
                    var hit = EvaluateCandidate(chunk, offset, absoluteAddress, fp, profile);
                    newHits.Add(hit);
                    break; 
                }
            }
        }

        return newHits;
    }

    private FamilyHit EvaluateCandidate(
        byte[] chunk, 
        int offset, 
        ulong absoluteAddress, 
        PlayerFingerprint control,
        Pes2021PlayerProfile profile)
    {
        var mask = control.Mask;
        var maskedControl = control.MaskedRecord;

        if (maskedControl == null)
        {
             return new FamilyHit(
                Address: absoluteAddress,
                PlayerId: control.PlayerId,
                PlayerName: control.PlayerName,
                ResultClass: FamilyResultClass.RefutedFalsePositive,
                Score: 0,
                Reasons: new[] { "control_record_missing" },
                Accepted: false);
        }

        var match = true;
        for (var i = 0; i < profile.Stride; i++)
        {
            var maskedMemoryByte = (byte)(chunk[offset + i] & mask[i]);
            if (maskedMemoryByte != maskedControl[i])
            {
                match = false;
                break;
            }
        }

        if (match)
        {
            return new FamilyHit(
                Address: absoluteAddress,
                PlayerId: control.PlayerId,
                PlayerName: control.PlayerName,
                ResultClass: FamilyResultClass.MaskedRecordCopy,
                Score: 10,
                Reasons: new[] { "masked_fingerprint_match" },
                Accepted: true);
        }
        else
        {
            return new FamilyHit(
                Address: absoluteAddress,
                PlayerId: control.PlayerId,
                PlayerName: control.PlayerName,
                ResultClass: FamilyResultClass.RefutedFalsePositive,
                Score: 0,
                Reasons: new[] { "masked_fingerprint_mismatch" },
                Accepted: false);
        }
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

    private static byte[] Combine(byte[] head, byte[] tail)
    {
        var combined = new byte[head.Length + tail.Length];
        Buffer.BlockCopy(head, 0, combined, 0, head.Length);
        Buffer.BlockCopy(tail, 0, combined, head.Length, tail.Length);
        return combined;
    }
}

public sealed record MultiAnchorScanResult(
    IReadOnlyList<FamilyHit> Hits,
    FamilyDiscoveryDiagnostics Diagnostics);
