using Overmem.Abstractions.Processes;

namespace Overmem.Abstractions.Memory;

public sealed record WriteMemoryRequest(
    AttachmentId AttachmentId,
    ulong Address,
    MemoryValueKind ValueKind,
    string Value,
    int Size = 0);