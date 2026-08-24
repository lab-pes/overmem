using Overmem.Abstractions.Search;

namespace Overmem.Search;

public interface IValueSearchService
{
    Task<ValueSearchResult> StartExactSearchAsync(StartValueSearchRequest request, CancellationToken cancellationToken = default);

    Task<ValueSearchResult> StartUnknownSearchAsync(StartUnknownValueSearchRequest request, CancellationToken cancellationToken = default);

    Task<ValueSearchResult> RefineAsync(RefineValueSearchRequest request, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ValueSearchSessionInfo>> ListSessionsAsync(CancellationToken cancellationToken = default);

    Task<ValueSearchResult> GetResultsAsync(ValueSearchSessionId sessionId, CancellationToken cancellationToken = default);

    Task<bool> CloseSessionAsync(ValueSearchSessionId sessionId, CancellationToken cancellationToken = default);
}