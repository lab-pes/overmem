using Overmem.Abstractions.Processes;

namespace Overmem.Abstractions.Memory;

public sealed record ResolvePointerRequest(
    AttachmentId AttachmentId,
    ulong BaseAddress,
    IReadOnlyList<long> Offsets);