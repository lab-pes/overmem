using System.Collections.Generic;
using Overmem.Abstractions;

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

    public void ScanForPointers(DiscoveredFamily family)
    {
        // Implementation for scanning pointers to known family hits.
        // Needs a full scan over memory looking for address values (x64 canonical).
        // Max depth is 4. Max nodes is 10000.
        // Requires multiple hits to confirm a relation.
    }
}
