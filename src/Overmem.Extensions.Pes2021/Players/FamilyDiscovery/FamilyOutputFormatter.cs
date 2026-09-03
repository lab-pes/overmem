using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace Overmem.Extensions.Pes2021.Players.FamilyDiscovery;

public enum OutputMode
{
    Summary,
    Compact,
    Full,
    Hits,
    Coverage
}

public static class FamilyOutputFormatter
{
    public static string Format(FamilyDiscoveryResult result, OutputMode mode)
    {
        var options = new JsonSerializerOptions { WriteIndented = true };

        return mode switch
        {
            OutputMode.Summary => JsonSerializer.Serialize(new
            {
                result.Diagnostics.FamiliesDiscovered,
                result.Diagnostics.TotalHits,
                result.Diagnostics.AcceptedHits,
                Families = result.Families.Select(f => new { f.FamilyId, f.Class, f.MatchedControls })
            }, options),
            
            OutputMode.Compact => JsonSerializer.Serialize(result.Families.Select(f => new
            {
                f.FamilyId,
                f.Class,
                RegionBase = $"0x{f.RegionBase:X}",
                f.CandidateStride,
                Hits = f.Hits.Select(h => new { Address = $"0x{h.Address:X}", h.PlayerId, h.PlayerName })
            }), options),

            OutputMode.Full => JsonSerializer.Serialize(result, options),

            OutputMode.Hits => JsonSerializer.Serialize(result.AllHits.Select(h => new 
            { 
                Address = $"0x{h.Address:X}", 
                h.PlayerId, 
                h.PlayerName, 
                h.ResultClass, 
                h.Accepted,
                h.Reasons
            }), options),

            OutputMode.Coverage => JsonSerializer.Serialize(result.Diagnostics.Regions, options),

            _ => throw new ArgumentOutOfRangeException(nameof(mode))
        };
    }
}
