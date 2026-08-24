namespace Overmem.Abstractions.Memory;

public sealed record PointerDiscoveryCandidate(
    ulong BaseAddress,
    IReadOnlyList<long> Offsets,
    string? ModuleName = null,
    long? ModuleRelativeBaseOffset = null,
    bool IsValidated = false,
    ulong? ResolvedAddress = null,
    int Score = 0);