using Overmem.Abstractions.Memory;
using Overmem.Abstractions.Processes;

namespace Overmem.Abstractions.Search;

public sealed record StartUnknownValueSearchRequest(
    AttachmentId AttachmentId,
    MemoryValueKind ValueKind,
    int Size = 0,
    int Alignment = 1,
    int MaxResults = 1_000_000);
