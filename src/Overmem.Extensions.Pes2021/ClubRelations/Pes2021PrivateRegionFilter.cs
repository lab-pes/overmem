using System;
using System.Collections.Generic;
using System.Linq;
using Overmem.Abstractions.Memory;

namespace Overmem.Extensions.Pes2021.ClubRelations;

public static class Pes2021PrivateRegionFilter
{
    public const ulong DefaultMaxRegionSize = 32UL * 1024 * 1024;

    public static IReadOnlyList<MemoryRegionInfo> FilterReadablePrivate(
        IReadOnlyList<MemoryRegionInfo> regions,
        ulong maxRegionSize = DefaultMaxRegionSize,
        IReadOnlyList<ulong>? preferredRegionBases = null)
    {
        var preferredSet = preferredRegionBases is null || preferredRegionBases.Count == 0
            ? null
            : new HashSet<ulong>(preferredRegionBases);

        var filtered = new List<MemoryRegionInfo>(regions.Count);
        foreach (var region in regions)
        {
            if (region is null)
            {
                continue;
            }

            if (!region.IsReadable)
            {
                continue;
            }

            if (region.IsExecutable)
            {
                continue;
            }

            var type = region.Type ?? string.Empty;
            if (!type.Contains("Private", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var protection = region.Protection ?? string.Empty;
            if (protection.Contains("Guard", StringComparison.OrdinalIgnoreCase)
                || protection.Contains("NoAccess", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (preferredSet is not null)
            {
                if (!preferredSet.Contains(region.BaseAddress))
                {
                    continue;
                }
            }
            else if (region.RegionSize > maxRegionSize)
            {
                continue;
            }

            filtered.Add(region);
        }

        return filtered.OrderBy(r => r.BaseAddress).ToArray();
    }
}
