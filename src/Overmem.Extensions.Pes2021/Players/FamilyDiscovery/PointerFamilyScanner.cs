using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using System.Buffers.Binary;
using Overmem.Abstractions;
using Overmem.Abstractions.Processes;
using Overmem.Abstractions.Memory;

namespace Overmem.Extensions.Pes2021.Players.FamilyDiscovery;

public sealed class PointerFamilyScanner
{
    private readonly IProcessMemoryGateway _gateway;
    private const int MaxDepth = 4;
    private const int MaxNodes = 10000;

    public PointerFamilyScanner(IProcessMemoryGateway gateway)
    {
        _gateway = gateway;
    }

    public async Task<int> ScanForPointersAsync(AttachmentId attachmentId, DiscoveredFamily family, CancellationToken cancellationToken)
    {
        var regions = await _gateway.ListRegionsAsync(attachmentId, cancellationToken);
        var validRegions = regions.Where(r => r.IsReadable);
        
        var hitAddresses = new HashSet<ulong>(family.Hits.Select(h => h.Address));
        if (hitAddresses.Count == 0) return 0;

        var blockReader = new ResilientBlockReader(_gateway);
        var budget = FamilyScanBudget.Unlimited;
        int pointersFound = 0;

        foreach (var region in validRegions)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var readResult = await blockReader.ReadRegionAsync(attachmentId, region.BaseAddress, region.BaseAddress + region.RegionSize, 1024 * 1024, budget, cancellationToken);
            
            foreach (var block in readResult.Blocks)
            {
                var span = block.Data.AsSpan();
                for (int i = 0; i <= span.Length - 8; i += 8)
                {
                    ulong ptr = System.Buffers.Binary.BinaryPrimitives.ReadUInt64LittleEndian(span.Slice(i));
                    if (hitAddresses.Contains(ptr))
                    {
                        pointersFound++;
                    }
                }
            }
        }
        
        return pointersFound;
    }
}
