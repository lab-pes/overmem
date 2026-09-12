using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using Overmem.Abstractions;
using Overmem.Abstractions.Processes;

namespace Overmem.Extensions.Pes2021.Players.FamilyDiscovery;

public sealed class PlayerTeamRelationScanner
{
    private readonly IProcessMemoryGateway _gateway;

    public PlayerTeamRelationScanner(IProcessMemoryGateway gateway)
    {
        _gateway = gateway;
    }

    public async Task<int> ScanForRelationsAsync(AttachmentId attachmentId, DiscoveredFamily family, CancellationToken cancellationToken)
    {
        var blockReader = new ResilientBlockReader(_gateway);
        var budget = FamilyScanBudget.Unlimited;
        int relationsFound = 0;

        // Scan only the specific region of the family
        var readResult = await blockReader.ReadRegionAsync(attachmentId, family.RegionBase, family.RegionEnd, 1024 * 1024, budget, cancellationToken);
        
        var hitIds = new HashSet<uint>();
        foreach (var hit in family.Hits)
        {
            if (hit.PlayerId.HasValue)
            {
                hitIds.Add(hit.PlayerId.Value);
            }
        }

        if (hitIds.Count == 0) return 0;

        foreach (var block in readResult.Blocks)
        {
            var span = block.Data.AsSpan();
            for (int i = 0; i <= span.Length - 8; i += 4)
            {
                uint val1 = System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(span.Slice(i));
                uint val2 = System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(span.Slice(i + 4));

                // Heuristic: If val2 is a known player ID and val1 is a plausible Team ID (1 to 999999)
                if (hitIds.Contains(val2) && val1 > 0 && val1 < 1000000)
                {
                    relationsFound++;
                }
            }
        }

        return relationsFound;
    }
}
