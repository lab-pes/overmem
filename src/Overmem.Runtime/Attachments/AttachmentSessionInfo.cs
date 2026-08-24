using Overmem.Abstractions.Processes;

namespace Overmem.Runtime.Attachments;

public sealed record AttachmentSessionInfo(
    AttachmentId AttachmentId,
    int ProcessId,
    string ProcessName,
    ProcessArchitecture Architecture,
    DateTimeOffset AttachedAtUtc,
    DateTimeOffset LastSeenAtUtc);