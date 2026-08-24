using Overmem.Abstractions.Memory;

namespace Overmem.Application.Pointers;

public interface IPointerDiscoveryService
{
    Task<DiscoverPointersResult> DiscoverAsync(DiscoverPointersRequest request, CancellationToken cancellationToken = default);
}