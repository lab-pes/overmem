namespace Overmem.Abstractions.Memory;

public sealed record WriteMemoryResult(
    ulong Address,
    MemoryValueKind ValueKind,
    int BytesWritten);