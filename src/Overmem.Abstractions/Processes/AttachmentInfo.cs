namespace Overmem.Abstractions.Processes;

public sealed record AttachmentInfo(
    AttachmentId AttachmentId,
    int ProcessId,
    string ProcessName,
    ProcessArchitecture Architecture);