using Overmem.Abstractions.Processes;

namespace Overmem.Abstractions.Memory;

public sealed record ReadMemoryRequest(
    AttachmentId AttachmentId,
    ulong Address,
    MemoryValueKind ValueKind,
    int Size = 0);