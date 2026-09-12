using System.Collections.Generic;
using System.Linq;

namespace Overmem.Extensions.Pes2021.Players.FamilyDiscovery;

public enum SlotClassification
{
    ValidPlayer,
    EmptyReserved,
    InvalidRecord,
    Unreadable,
    PartialRead,
    Hole,
    AmbiguousRecord,
    NonPlayerBoundary,
}

public sealed class SlotClassifier
{
    public IReadOnlyList<SlotClassification> ClassifyFamilySlots(DiscoveredFamily family, ulong maxRegionAddress)
    {
        var classifications = new List<SlotClassification>();
        ulong current = family.RegionBase;

        while (current + (ulong)family.CandidateStride <= maxRegionAddress)
        {
            var hitsInSlot = family.Hits.Where(h => h.Address >= current && h.Address < current + (ulong)family.CandidateStride).ToList();

            if (hitsInSlot.Any(h => h.ResultClass == FamilyResultClass.ExactRecordCopy || h.ResultClass == FamilyResultClass.MaskedRecordCopy))
            {
                classifications.Add(SlotClassification.ValidPlayer);
            }
            else if (hitsInSlot.Any(h => h.ResultClass == FamilyResultClass.AmbiguousFamily))
            {
                classifications.Add(SlotClassification.AmbiguousRecord);
            }
            else
            {
                classifications.Add(SlotClassification.Hole);
            }

            current += (ulong)family.CandidateStride;
        }

        return classifications;
    }
}
