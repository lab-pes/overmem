namespace Overmem.Abstractions.Memory;

public sealed record ModuleInfo(string Name, ulong BaseAddress, int Size);