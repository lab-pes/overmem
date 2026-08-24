namespace Overmem.Abstractions.Freezing;

public abstract record FreezeAddressSource;

public sealed record AbsoluteAddressSource(ulong Address) : FreezeAddressSource;

public sealed record PointerAddressSource(ulong BaseAddress, IReadOnlyList<long> Offsets) : FreezeAddressSource;

public sealed record ModulePointerAddressSource(string ModuleName, long BaseOffset, IReadOnlyList<long> Offsets) : FreezeAddressSource;