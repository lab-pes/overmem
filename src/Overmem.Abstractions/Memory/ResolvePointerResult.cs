namespace Overmem.Abstractions.Memory;

public sealed record ResolvePointerResult(
    ulong BaseAddress,
    IReadOnlyList<long> Offsets,
    ulong ResolvedAddress);