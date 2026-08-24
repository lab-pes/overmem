using Overmem.Abstractions.Memory;
using Overmem.Abstractions.Processes;

namespace Overmem.Abstractions.Search;

public sealed record ValueSearchSessionInfo(
    ValueSearchSessionId SessionId,
    AttachmentId AttachmentId,
    MemoryValueKind ValueKind,
    int Size,
    int Alignment,
    int ResultCount,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    bool IsUnknownStart = false);