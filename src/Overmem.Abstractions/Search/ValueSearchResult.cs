using Overmem.Abstractions.Memory;

namespace Overmem.Abstractions.Search;

public sealed record ValueSearchResult(
    ValueSearchSessionId SessionId,
    MemoryValueKind ValueKind,
    ValueSearchComparison Comparison,
    int ResultCount,
    IReadOnlyList<ValueSearchMatch> Matches);