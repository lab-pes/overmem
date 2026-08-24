using Overmem.Abstractions.Freezing;
using Overmem.Abstractions.Processes;

namespace Overmem.Application.Freezing;

public interface IProcessFreezeCoordinator
{
    Task<FreezeInfo> FreezeAsync(FreezeRequest request, CancellationToken cancellationToken = default);

    Task<bool> UnfreezeAsync(FreezeId freezeId, CancellationToken cancellationToken = default);

    Task<int> UnfreezeByAttachmentAsync(AttachmentId attachmentId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<FreezeInfo>> ListAsync(CancellationToken cancellationToken = default);
}