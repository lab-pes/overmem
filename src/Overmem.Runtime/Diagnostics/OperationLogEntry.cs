namespace Overmem.Runtime.Diagnostics;

public sealed record OperationLogEntry(
    Guid OperationId,
    string Name,
    string Outcome,
    DateTimeOffset TimestampUtc,
    string? AttachmentId = null,
    string? Detail = null);