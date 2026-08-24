namespace Overmem.Application.Tables;

public sealed record MemoryTableEntrySnapshot(
    string EntryId,
    string Name,
    ulong? ResolvedAddress,
    string? Value,
    string? ErrorMessage);