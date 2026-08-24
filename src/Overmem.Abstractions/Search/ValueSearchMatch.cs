namespace Overmem.Abstractions.Search;

public sealed record ValueSearchMatch(
    ulong Address,
    string Value);