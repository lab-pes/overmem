using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Overmem.Abstractions.Memory;
using Overmem.Abstractions.Processes;
using Overmem.Application;

namespace Overmem.Extensions.Pes2021.ClubRelations;

public sealed class Pes2021ClubLayoutAnalyzer
{
    private readonly ProcessMemoryApplicationService _memoryService;

    public Pes2021ClubLayoutAnalyzer(ProcessMemoryApplicationService memoryService)
    {
        _memoryService = memoryService ?? throw new ArgumentNullException(nameof(memoryService));
    }

    public async Task<IReadOnlyList<Pes2021ClubLayoutCandidate>> AnalyzeAsync(
        AttachmentId attachmentId,
        IReadOnlyList<Pes2021ClubObservationRow> observations,
        IReadOnlyList<int> windowSizes,
        IReadOnlyDictionary<(int TeamId, int SecondaryId), int> controlOrdinals,
        int controlCaseCount,
        IReadOnlyList<MemoryRegionInfo>? regions,
        CancellationToken cancellationToken)
    {
        var candidates = new List<Pes2021ClubLayoutCandidate>();
        if (observations.Count == 0 || windowSizes.Count == 0)
        {
            return candidates;
        }

        var maxWindow = windowSizes.Max();
        var layoutByClub = new Dictionary<(int TeamId, int SecondaryId), List<(int Offset, int Value)>>();

        foreach (var row in observations)
        {
            if (!row.ClubRecordAddress.HasValue)
            {
                continue;
            }

            var address = row.ClubRecordAddress.Value;
            var windows = new List<Pes2021ClubLayoutWindow>();
            foreach (var size in windowSizes)
            {
                var window = await ReadWindowAsync(attachmentId, address, size, cancellationToken);
                if (window.Entries.Count > 0)
                {
                    windows.Add(window);
                }
            }

            if (windows.Count == 0)
            {
                continue;
            }

            int ordinal;
            if (controlOrdinals.TryGetValue((row.TeamId, row.SecondaryId), out var mapped))
            {
                ordinal = mapped;
            }
            else
            {
                ordinal = controlCaseCount;
            }

            var candidate = new Pes2021ClubLayoutCandidate(
                row.TeamId,
                row.SecondaryId,
                row.Notes,
                address,
                ordinal,
                windows,
                []);

            candidates.Add(candidate);

            var key = (row.TeamId, row.SecondaryId);
            if (!layoutByClub.TryGetValue(key, out var entries))
            {
                entries = new List<(int, int)>();
                layoutByClub[key] = entries;
            }

            foreach (var w in windows)
            {
                foreach (var e in w.Entries)
                {
                    entries.Add((e.Offset, e.AsInt32));
                }
            }
        }

        var fields = ClassifyOffsets(candidates);
        return candidates
            .Select(c => c with { FieldSummary = fields })
            .ToArray();
    }

    private async Task<Pes2021ClubLayoutWindow> ReadWindowAsync(
        AttachmentId attachmentId,
        ulong address,
        int size,
        CancellationToken cancellationToken)
    {
        var entries = new List<Pes2021ClubLayoutWindowEntry>();
        if (size <= 0)
        {
            return new Pes2021ClubLayoutWindow(size, entries);
        }

        var alignedSize = size & ~3;
        if (alignedSize <= 0)
        {
            return new Pes2021ClubLayoutWindow(size, entries);
        }

        var bytes = await TryReadAsync(attachmentId, address, alignedSize, cancellationToken);
        if (bytes is null || bytes.Length < alignedSize)
        {
            return new Pes2021ClubLayoutWindow(size, entries);
        }

        for (var i = 0; i < alignedSize; i += 4)
        {
            var span = bytes.AsSpan(i, 4);
            var u32 = BinaryPrimitives.ReadUInt32LittleEndian(span);
            var i32 = BinaryPrimitives.ReadInt32LittleEndian(span);
            var u16Lo = BinaryPrimitives.ReadUInt16LittleEndian(span.Slice(0, 2));
            var b = span[0];

            entries.Add(new Pes2021ClubLayoutWindowEntry(
                Offset: i,
                AsByte: b,
                AsUInt16: u16Lo,
                AsUInt32: u32,
                AsInt32: i32));
        }

        return new Pes2021ClubLayoutWindow(size, entries);
    }

    private async Task<byte[]?> TryReadAsync(AttachmentId attachmentId, ulong address, int size, CancellationToken cancellationToken)
    {
        try
        {
            var result = await _memoryService.ReadAsync(
                new ReadMemoryRequest(attachmentId, address, MemoryValueKind.Bytes, size),
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

    private static IReadOnlyList<Pes2021ClubLayoutField> ClassifyOffsets(IReadOnlyList<Pes2021ClubLayoutCandidate> candidates)
    {
        var byOffset = new SortedDictionary<int, List<int>>();
        foreach (var candidate in candidates)
        {
            foreach (var window in candidate.Windows)
            {
                foreach (var entry in window.Entries)
                {
                    if (!byOffset.TryGetValue(entry.Offset, out var list))
                    {
                        list = new List<int>();
                        byOffset[entry.Offset] = list;
                    }

                    list.Add(entry.AsInt32);
                }
            }
        }

        var fields = new List<Pes2021ClubLayoutField>();
        foreach (var pair in byOffset)
        {
            var offset = pair.Key;
            var values = pair.Value;
            var distinct = values.Distinct().ToArray();
            var distinctCount = distinct.Length;

            var stability = ClubLayoutFieldStability.Unknown;
            if (distinctCount == 1)
            {
                stability = values.Count > 1
                    ? ClubLayoutFieldStability.ConstantAcrossClubs
                    : ClubLayoutFieldStability.ConstantPerClub;
            }
            else if (distinctCount <= 3 && values.Count >= 6)
            {
                stability = ClubLayoutFieldStability.LeagueSpecific;
            }
            else if (distinctCount > values.Count / 2)
            {
                stability = ClubLayoutFieldStability.VariablePerClub;
            }

            fields.Add(new Pes2021ClubLayoutField(
                offset,
                stability,
                values.Count,
                distinctCount,
                distinct));
        }

        return fields;
    }
}
