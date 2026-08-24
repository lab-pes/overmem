using Overmem.Abstractions.Processes;

namespace Overmem.Abstractions.Memory;

public sealed record ResolveModulePointerRequest(
    AttachmentId AttachmentId,
    string ModuleName,
    long BaseOffset,
    IReadOnlyList<long> Offsets);