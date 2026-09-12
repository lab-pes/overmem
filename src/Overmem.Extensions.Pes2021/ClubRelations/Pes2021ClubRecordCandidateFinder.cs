using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Text;

namespace Overmem.Extensions.Pes2021.ClubRelations;

public sealed record Pes2021ClubRecordCandidate(
    int TeamId,
    int SecondaryId,
    string Name,
    ulong RegionBaseAddress,
    ulong NameMatchAddress,
    long NameRelativeOffset,
    ulong? IdMatchAddress,
    long? IdRelativeOffset,
    int Score);

public sealed record Pes2021ClubRecordCandidateHit(
    Pes2021ClubRecordCandidate Candidate,
    string ControlCase);

public static class Pes2021ClubRecordCandidateFinder
{
    private const int MaxWindowBytes = 0x1000;

    public static IReadOnlyList<Pes2021ClubRecordCandidateHit> FindCandidates(
        IReadOnlyList<RegionBlockSnapshot> regions,
        IReadOnlyList<Pes2021ClubCatalogRow> catalog,
        IReadOnlyDictionary<(int TeamId, int SecondaryId), string> controlCases)
    {
        var results = new List<Pes2021ClubRecordCandidateHit>();
        if (regions.Count == 0 || catalog.Count == 0)
        {
            return results;
        }

        var bestPerIdentity = new Dictionary<(int TeamId, int SecondaryId), Pes2021ClubRecordCandidate>();
        var rowsByTeamId = new Dictionary<int, List<Pes2021ClubCatalogRow>>();
        foreach (var row in catalog)
        {
            if (string.IsNullOrEmpty(row.Name))
            {
                continue;
            }

            if (!rowsByTeamId.TryGetValue(row.TeamId, out var rows))
            {
                rows = new List<Pes2021ClubCatalogRow>();
                rowsByTeamId[row.TeamId] = rows;
            }

            rows.Add(row);
        }

        foreach (var snapshot in regions)
        {
            var buffer = snapshot.ConcatenatedBuffer;
            if (buffer.Length == 0)
            {
                continue;
            }

            ScanRegionForCandidates(buffer, snapshot.Region.BaseAddress, rowsByTeamId, bestPerIdentity);
        }

        foreach (var pair in bestPerIdentity.OrderBy(pair => pair.Key.TeamId).ThenBy(pair => pair.Key.SecondaryId))
        {
            var key = (pair.Value.TeamId, pair.Value.SecondaryId);
            if (!controlCases.TryGetValue(key, out var controlCase))
            {
                controlCase = "C0";
            }

            results.Add(new Pes2021ClubRecordCandidateHit(pair.Value, controlCase));
        }

        return results;
    }

    private static void ScanRegionForCandidates(
        byte[] buffer,
        ulong regionBase,
        Dictionary<int, List<Pes2021ClubCatalogRow>> rowsByTeamId,
        Dictionary<(int TeamId, int SecondaryId), Pes2021ClubRecordCandidate> bestPerIdentity)
    {
        var span = buffer.AsSpan();
        var limit = span.Length;
        var lastNameEnd = -1;

        for (var i = 0; i + 2 <= limit; i++)
        {
            var value = BinaryPrimitives.ReadUInt16LittleEndian(span.Slice(i, 2));
            if (!rowsByTeamId.TryGetValue(value, out var rows))
            {
                continue;
            }

            foreach (var row in rows)
            {
                var nameBytes = Encoding.UTF8.GetBytes(row.Name);
                var nameLen = nameBytes.Length;
                if (nameLen == 0)
                {
                    continue;
                }

                var windowStart = Math.Max(0, i - MaxWindowBytes);
                var windowEnd = Math.Min(limit - nameLen, i + MaxWindowBytes);
                for (var start = windowStart; start <= windowEnd; start++)
                {
                    if (start + nameLen > limit)
                    {
                        break;
                    }

                    var matched = true;
                    for (var j = 0; j < nameLen; j++)
                    {
                        if (buffer[start + j] != nameBytes[j])
                        {
                            matched = false;
                            break;
                        }
                    }

                    if (!matched)
                    {
                        continue;
                    }

                    var relativeOffset = start - i;
                    var distance = Math.Abs(relativeOffset);
                    var score = 30;
                    score += Math.Max(0, 30 - (int)(distance / 32));
                    var identity = (row.TeamId, row.SecondaryId);

                    if (bestPerIdentity.TryGetValue(identity, out var existing)
                        && existing.Score >= score)
                    {
                        continue;
                    }

                    bestPerIdentity[identity] = new Pes2021ClubRecordCandidate(
                        row.TeamId,
                        row.SecondaryId,
                        row.Name,
                        regionBase,
                        regionBase + (ulong)start,
                        start,
                        regionBase + (ulong)i,
                        relativeOffset,
                        score);

                    lastNameEnd = start + nameLen;
                    break;
                }
            }

            if (lastNameEnd >= 0 && i > lastNameEnd)
            {
                lastNameEnd = -1;
            }
        }
    }
}
