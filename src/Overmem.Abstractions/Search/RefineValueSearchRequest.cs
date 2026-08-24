namespace Overmem.Abstractions.Search;

public sealed record RefineValueSearchRequest(
    ValueSearchSessionId SessionId,
    ValueSearchComparison Comparison,
    string? Value = null,
    string? SecondaryValue = null);