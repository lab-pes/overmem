using Overmem.Abstractions.Memory;

namespace Overmem.Application.Tables;

public sealed record MemoryTableEntry(
    string EntryId,
    string Name,
    MemoryValueKind ValueKind,
    MemoryTableAddressKind AddressKind,
    ulong AbsoluteAddress = 0,
    ulong BaseAddress = 0,
    string? ModuleName = null,
    long BaseOffset = 0,
    IReadOnlyList<long>? Offsets = null,
    int Size = 0,
    int? RefreshIntervalMs = null,
    MemoryTableFreezeConfiguration? Freeze = null);