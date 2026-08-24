namespace Overmem.Application.Tables;

public sealed record MemoryTableFreezeConfiguration(string Value, int IntervalMs = 25);