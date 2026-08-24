namespace Overmem.Application.Tables;

public sealed record MemoryTableDocument(
    string SchemaVersion,
    string Name,
    IReadOnlyList<MemoryTableEntry> Entries)
{
    public const string CurrentSchemaVersion = "1.0";
}