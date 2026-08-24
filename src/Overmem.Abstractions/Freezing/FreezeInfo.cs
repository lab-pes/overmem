using Overmem.Abstractions.Memory;
using Overmem.Abstractions.Processes;

namespace Overmem.Abstractions.Freezing;

public sealed record FreezeInfo(
    FreezeId FreezeId,
    AttachmentId AttachmentId,
    FreezeAddressSource AddressSource,
    MemoryValueKind ValueKind,
    string Value,
    int Size,
    int IntervalMs,
    FreezeStatus Status,
    string? ErrorMessage = null);