using System.Collections.Generic;
using System.Linq;

namespace Overmem.Extensions.Pes2021.Players.FamilyDiscovery;

public sealed record SessionComparisonReport(
    IReadOnlyList<DiscoveredFamily> PersistentFamilies,
    IReadOnlyList<DiscoveredFamily> NewFamilies,
    IReadOnlyList<DiscoveredFamily> DisappearedFamilies,
    IReadOnlyList<PlayerChange> PlayerChanges
);

public sealed record PlayerChange(
    uint PlayerId,
    ulong OldAddress,
    ulong NewAddress,
    string ChangeType // "Moved", "Added", "Removed"
);

public sealed class SessionComparator
{
    public SessionComparisonReport Compare(FamilyDiscoveryResult before, FamilyDiscoveryResult after)
    {
        var persistent = new List<DiscoveredFamily>();
        var newFams = new List<DiscoveredFamily>();
        var disappeared = new List<DiscoveredFamily>();
        var changes = new List<PlayerChange>();

        // Find persistent families (match by stride and structure, not necessarily address)
        foreach (var famAfter in after.Families)
        {
            var matchedBefore = before.Families.FirstOrDefault(f => f.CandidateStride == famAfter.CandidateStride && f.Class == famAfter.Class);
            if (matchedBefore != null)
            {
                persistent.Add(famAfter);
                
                // Track player movements within persistent families
                var afterHits = famAfter.Hits.Where(h => h.Accepted).ToDictionary(h => h.PlayerId.GetValueOrDefault());
                var beforeHits = matchedBefore.Hits.Where(h => h.Accepted).ToDictionary(h => h.PlayerId.GetValueOrDefault());

                foreach (var kvp in afterHits)
                {
                    if (beforeHits.TryGetValue(kvp.Key, out var oldHit))
                    {
                        if (oldHit.Address != kvp.Value.Address)
                        {
                            changes.Add(new PlayerChange(kvp.Key, oldHit.Address, kvp.Value.Address, "Moved"));
                        }
                    }
                    else
                    {
                        changes.Add(new PlayerChange(kvp.Key, 0, kvp.Value.Address, "Added"));
                    }
                }

                foreach (var kvp in beforeHits)
                {
                    if (!afterHits.ContainsKey(kvp.Key))
                    {
                        changes.Add(new PlayerChange(kvp.Key, kvp.Value.Address, 0, "Removed"));
                    }
                }
            }
            else
            {
                newFams.Add(famAfter);
            }
        }

        foreach (var famBefore in before.Families)
        {
            if (!persistent.Any(f => f.CandidateStride == famBefore.CandidateStride && f.Class == famBefore.Class))
            {
                disappeared.Add(famBefore);
            }
        }

        return new SessionComparisonReport(persistent, newFams, disappeared, changes);
    }
}
