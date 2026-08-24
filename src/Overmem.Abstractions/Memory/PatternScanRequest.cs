using Overmem.Abstractions.Processes;

namespace Overmem.Abstractions.Memory;

public sealed record PatternScanRequest(
    AttachmentId AttachmentId,
    string Pattern,
    string? ModuleName = null,
    int MaxResults = 100);