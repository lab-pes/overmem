using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace Overmem.Extensions.Pes2021.Players.FamilyDiscovery;

public sealed class FamilyClusteringEngine
{
    public IReadOnlyList<DiscoveredFamily> Cluster(IReadOnlyList<FamilyHit> hits, int candidateStride)
    {
        var families = new List<DiscoveredFamily>();
        
        // Group by RegionBase and ResultClass
        var groupedHits = hits.Where(h => h.Accepted).GroupBy(h => h.ResultClass);

        foreach (var group in groupedHits)
        {
            var classHits = group.OrderBy(h => h.Address).ToList();

            if (classHits.Count < 3)
                continue; // Skip small clusters

            // Group by Residue (Address % stride)
            var residueGroups = classHits.GroupBy(h => h.Address % (ulong)candidateStride);

            foreach (var rGroup in residueGroups)
            {
                var familyHits = rGroup.OrderBy(h => h.Address).ToList();

                if (familyHits.Count < 3)
                    continue;

                ulong regionBase = familyHits.First().Address; // Approximation for now
                ulong regionEnd = familyHits.Last().Address;

                families.Add(new DiscoveredFamily(
                    FamilyId: BuildDeterministicFamilyId(regionBase, candidateStride, (int)rGroup.Key),
                    Class: group.Key,
                    RegionBase: regionBase,
                    RegionEnd: regionEnd,
                    CandidateStride: candidateStride,
                    CandidateResidue: (int)rGroup.Key,
                    MatchedControls: familyHits.Select(h => h.PlayerId).Distinct().Count(),
                    ExactMatches: familyHits.Count(h => h.ResultClass == FamilyResultClass.ExactRecordCopy),
                    MaskedMatches: familyHits.Count(h => h.ResultClass == FamilyResultClass.MaskedRecordCopy),
                    IdOnlyMatches: familyHits.Count(h => h.ResultClass == FamilyResultClass.DenseIdTable),
                    NameMatches: 0,
                    NeighborConsistency: 1.0, // Simplified for now
                    Confidence: 1.0,
                    Reasons: Array.Empty<string>(),
                    Hits: familyHits
                ));
            }
        }

        return families;
    }

    /// <summary>
    /// Gera um FamilyId determinístico a partir de RegionBase, CandidateStride e CandidateResidue.
    /// O mesmo trio sempre produz o mesmo ID, garantindo estabilidade entre sessões.
    /// </summary>
    private static string BuildDeterministicFamilyId(ulong regionBase, int stride, int residue)
    {
        var input = $"fds:{regionBase:X16}:{stride}:{residue}";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(hash)[..32].ToLowerInvariant();
    }
}
