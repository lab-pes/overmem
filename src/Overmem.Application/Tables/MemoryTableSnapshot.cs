namespace Overmem.Application.Tables;

public sealed record MemoryTableSnapshot(
    string SchemaVersion,
    string Name,
    IReadOnlyList<MemoryTableEntrySnapshot> Entries);