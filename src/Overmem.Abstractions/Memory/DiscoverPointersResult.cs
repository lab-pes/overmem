namespace Overmem.Abstractions.Memory;

public sealed record DiscoverPointersResult(
    ulong TargetAddress,
    int MaxDepth,
    long MaxOffset,
    int Alignment,
    int ResultCount,
    IReadOnlyList<PointerDiscoveryCandidate> Candidates);