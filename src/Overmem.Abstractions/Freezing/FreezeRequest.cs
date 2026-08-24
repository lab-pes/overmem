using Overmem.Abstractions.Memory;
using Overmem.Abstractions.Processes;

namespace Overmem.Abstractions.Freezing;

public sealed record FreezeRequest(
    AttachmentId AttachmentId,
    FreezeAddressSource AddressSource,
    MemoryValueKind ValueKind,
    string Value,
    int Size = 0,
    int IntervalMs = 25);