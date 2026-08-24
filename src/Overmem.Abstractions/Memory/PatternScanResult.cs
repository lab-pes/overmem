namespace Overmem.Abstractions.Memory;

public sealed record PatternScanResult(
    string Pattern,
    string? ModuleName,
    IReadOnlyList<ulong> Addresses);