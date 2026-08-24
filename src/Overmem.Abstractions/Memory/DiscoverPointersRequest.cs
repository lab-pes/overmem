using Overmem.Abstractions.Processes;

namespace Overmem.Abstractions.Memory;

public sealed record DiscoverPointersRequest(
    AttachmentId AttachmentId,
    ulong TargetAddress,
    int MaxDepth = 2,
    long MaxOffset = 0,
    int Alignment = 0,
    int MaxResults = 100,
    string? BaseModuleName = null,
    bool RevalidateCandidates = true);