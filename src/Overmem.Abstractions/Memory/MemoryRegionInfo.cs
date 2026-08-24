namespace Overmem.Abstractions.Memory;

public sealed record MemoryRegionInfo(
    ulong BaseAddress,
    ulong RegionSize,
    string State,
    string Protection,
    string Type,
    bool IsReadable,
    bool IsWritable,
    bool IsExecutable);