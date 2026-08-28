using System.Collections.Generic;
using Overmem.Abstractions.Memory;

namespace Overmem.Extensions.Pes2021.ClubRelations;

public sealed record RegionBlockSnapshot(
    MemoryRegionInfo Region,
    IReadOnlyList<RegionBlockRead> Blocks,
    byte[] ConcatenatedBuffer);

public sealed class Pes2021RegionSnapshotCache
{
    private readonly List<RegionBlockSnapshot> _snapshots = new();

    public void Add(RegionBlockSnapshot snapshot) => _snapshots.Add(snapshot);

    public IReadOnlyList<RegionBlockSnapshot> Snapshots => _snapshots;

    public int TotalRegionCount => _snapshots.Count;

    public int TotalBlockCount
    {
        get
        {
            var total = 0;
            foreach (var snapshot in _snapshots)
            {
                total += snapshot.Blocks.Count;
            }

            return total;
        }
    }
}
