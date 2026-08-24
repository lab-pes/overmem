namespace Overmem.Abstractions.Memory;

public sealed record ReadMemoryResult(
    ulong Address,
    MemoryValueKind ValueKind,
    string Value,
    int BytesRead);