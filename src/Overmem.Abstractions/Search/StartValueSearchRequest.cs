using Overmem.Abstractions.Memory;
using Overmem.Abstractions.Processes;

namespace Overmem.Abstractions.Search;

public sealed record StartValueSearchRequest(
    AttachmentId AttachmentId,
    MemoryValueKind ValueKind,
    string Value,
    int Size = 0,
    int Alignment = 1,
    int MaxResults = 1_000);