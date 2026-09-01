using System.Collections.Generic;

namespace Overmem.Extensions.Pes2021.Players;

/// <summary>
/// A single decoded field on a player record. Carries both the raw stored value and an
/// optional display value. <see cref="Warnings"/> surface non-fatal decoder notes that do
/// not invalidate the record (for example, a name with no embedded NUL terminator within
/// the expected width).
/// </summary>
public sealed record DecodedFieldValue(
    string Name,
    long? RawLong,
    string? RawString,
    double? Display,
    Pes2021PlayerEvidenceStatus EvidenceStatus,
    Pes2021PlayerTransform Transform,
    IReadOnlyList<string> Warnings);

/// <summary>
/// Decoded snapshot of a single player record. Carries the raw 380 bytes so downstream
/// consumers can verify, fingerprint, and hash the original bytes without re-reading.
/// </summary>
public sealed record DecodedPlayerRecord(
    ulong Address,
    int RecordIndex,
    uint PlayerId,
    string? PlayerName,
    string? ClubShirtName,
    string? NationalShirtName,
    IReadOnlyList<DecodedFieldValue> Fields,
    byte[] RawRecord,
    string RawRecordSha256,
    IReadOnlyList<string> Warnings);

/// <summary>
/// Result of a single record parse attempt. <see cref="Success"/> is true only when all
/// cheap validation checks pass and every required field was decoded. Failures carry a
/// stable reason and the field offset for diagnostics.
/// </summary>
public sealed record PlayerRecordParseResult(
    bool Success,
    DecodedPlayerRecord? Record,
    string? RejectionReason,
    int? RejectionOffset,
    IReadOnlyList<string> Warnings);

/// <summary>
/// Result of <see cref="Pes2021PlayerRecordValidator"/>. Always carries a score and the
/// list of contributing reasons; success is a derived property for callers that prefer a
/// boolean.
/// </summary>
public sealed record PlayerRecordValidationResult(
    bool Accept,
    int Score,
    int MaxScore,
    IReadOnlyList<string> Reasons);