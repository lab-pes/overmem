using System;
using System.Collections.Generic;
using Overmem.Abstractions.Memory;

namespace Overmem.Extensions.Pes2021.Players.FamilyDiscovery;

/// <summary>
/// Filtra regiões de memória reportadas por VirtualQueryEx segundo uma <see cref="RegionPolicy"/>.
/// Adicionalmente, deduplica regiões pelo `BaseAddress` para evitar que a mesma janela
/// seja examinada duas vezes se o processo-alvo foi mapeado com overlap.
/// </summary>
public static class RegionPolicyFilter
{
    public static (IReadOnlyList<MemoryRegionInfo> Accepted, IReadOnlyList<MemoryRegionInfo> Rejected) Filter(
        IReadOnlyList<MemoryRegionInfo> regions,
        RegionPolicy policy)
    {
        var accepted = new List<MemoryRegionInfo>();
        var rejected = new List<MemoryRegionInfo>();
        var seenAddresses = new HashSet<ulong>();

        foreach (var region in regions)
        {
            if (!seenAddresses.Add(region.BaseAddress))
            {
                // Região já processada (deduplicação)
                continue;
            }

            if (Accepts(region, policy))
            {
                accepted.Add(region);
            }
            else
            {
                rejected.Add(region);
            }
        }

        return (accepted, rejected);
    }

    private static bool Accepts(MemoryRegionInfo region, RegionPolicy policy)
    {
        // 1. All - Aceita qualquer região que seja legível
        if (policy == RegionPolicy.All)
        {
            return region.IsReadable;
        }

        // Para outras políticas, a região DEVE ser legível. Se não for, descarta imediatamente.
        if (!region.IsReadable)
        {
            return false;
        }

        var isCommit = string.Equals(region.State, "MEM_COMMIT", StringComparison.OrdinalIgnoreCase);
        if (!isCommit)
        {
            return false;
        }

        var isPrivate = string.Equals(region.Type, "MEM_PRIVATE", StringComparison.OrdinalIgnoreCase) || 
                        string.Equals(region.Type, "Private", StringComparison.OrdinalIgnoreCase);
        var isMapped = string.Equals(region.Type, "MEM_MAPPED", StringComparison.OrdinalIgnoreCase) ||
                       string.Equals(region.Type, "Mapped", StringComparison.OrdinalIgnoreCase);

        // 2. DefaultPlayerArena (MEM_COMMIT + Private + RW)
        if (policy == RegionPolicy.DefaultPlayerArena)
        {
            return isPrivate && region.IsWritable && !region.IsExecutable;
        }

        // 3. IncludeMapped
        if (policy == RegionPolicy.IncludeMapped)
        {
            var validCategory = isPrivate || isMapped;
            return validCategory && region.IsWritable && !region.IsExecutable;
        }

        // 4. IncludeReadOnly
        if (policy == RegionPolicy.IncludeReadOnly)
        {
            var validCategory = isPrivate || isMapped;
            // RW ou RO, desde que não seja executável
            return validCategory && !region.IsExecutable;
        }

        // 5. IncludeExecutable
        if (policy == RegionPolicy.IncludeExecutable)
        {
            var validCategory = isPrivate || isMapped;
            // Qualquer proteção, inclusive executável
            return validCategory;
        }

        return false;
    }

    /// <summary>
    /// Constrói o diagnóstico inicial das regiões, separando aceitas e rejeitadas.
    /// Bytes Requested/Read serão atualizados pelo scanner.
    /// </summary>
    public static IReadOnlyList<FamilyRegionDiagnostic> BuildDiagnostics(
        IReadOnlyList<MemoryRegionInfo> regions,
        RegionPolicy policy)
    {
        var list = new List<FamilyRegionDiagnostic>(regions.Count);
        var seenAddresses = new HashSet<ulong>();

        foreach (var region in regions)
        {
            if (!seenAddresses.Add(region.BaseAddress))
            {
                continue; // Deduplicado silenciosamente do diagnóstico para evitar ruído
            }

            var decision = Accepts(region, policy) ? "examined" : "skipped";
            string? skipReason = null;

            if (decision == "skipped")
            {
                if (!region.IsReadable) skipReason = "not_readable";
                else if (!string.Equals(region.State, "MEM_COMMIT", StringComparison.OrdinalIgnoreCase)) skipReason = "not_committed";
                else if (policy == RegionPolicy.DefaultPlayerArena && !region.IsWritable) skipReason = "not_writable";
                else if (policy != RegionPolicy.IncludeExecutable && region.IsExecutable) skipReason = "executable_disallowed";
                else skipReason = "type_mismatch";
            }

            list.Add(new FamilyRegionDiagnostic(
                BaseAddress: $"0x{region.BaseAddress:X}",
                StopAddress: $"0x{region.BaseAddress + region.RegionSize:X}",
                Size: region.RegionSize,
                State: region.State,
                Type: region.Type,
                Protection: region.Protection,
                Decision: decision,
                SkipReason: skipReason,
                BytesRequested: 0,
                BytesRead: 0));
        }

        return list;
    }
}
