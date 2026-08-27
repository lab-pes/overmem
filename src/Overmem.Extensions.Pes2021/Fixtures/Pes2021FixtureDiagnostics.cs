using System.Collections.Generic;
using Overmem.Abstractions.Processes;

namespace Overmem.Extensions.Pes2021.Fixtures;

/// <summary>
/// Per-extraction counters, timings, and warnings. The collector never reads memory; it only
/// aggregates values reported by the reader, parser, anchor finder, resolver, and cache.
/// </summary>
public sealed record ExtractionDiagnostics(
    CacheDisposition CacheDisposition,
    int RegionsEnumerated,
    int RegionsAccepted,
    int RegionsRejected,
    ulong BytesRequested,
    ulong BytesRead,
    int ReadCalls,
    int BlocksRead,
    int RecordsDecoded,
    int RecordsAccepted,
    int RecordsRejected,
    IReadOnlyDictionary<string, int> RejectionReasons,
    IReadOnlyDictionary<string, double> StageDurationMs,
    IReadOnlyList<RegionDiagnostic> Regions,
    IReadOnlyList<string> Warnings);

/// <summary>
/// One accepted or rejected memory region for the diagnostics payload.
/// </summary>
public sealed record RegionDiagnostic(
    string BaseAddress,
    string StopAddress,
    ulong Size,
    string State,
    string Type,
    string Protection,
    bool Readable,
    bool Writable,
    bool Executable,
    string Decision,
    string? Reason);

/// <summary>
/// Process instance identity used as part of the cache key. <see cref="AttachmentId"/> is the
/// public identity of the attach session; the PID and start time disambiguate between two
/// attaches to the same PID across PES restarts.
/// </summary>
public sealed record ProcessInstanceIdentity(
    AttachmentId AttachmentId,
    int ProcessId,
    DateTimeOffset? ProcessStartedAtUtc,
    string ProcessName);

/// <summary>
/// Identity of a calendar session: process, profile, bases, validated sample. Used both as
/// the cache value and as the session block of the extraction result.
/// </summary>
public sealed record CalendarSession(
    ProcessInstanceIdentity Process,
    string ProfileId,
    string ProfileVersion,
    string ProfileSha256,
    int RecordStride,
    int RecordLimit,
    string? CalendarArrayBaseAddress,
    string CompetitionBlockBaseAddress,
    string AnchorAddress,
    int? AnchorIndex,
    string ValidationSampleSha256,
    DateTimeOffset ValidatedAtUtc,
    CacheDisposition CacheDisposition);

/// <summary>
/// Stable rejection reasons emitted by the parser, block reader, anchor finder and resolver.
/// They appear both as counters in <see cref="ExtractionDiagnostics.RejectionReasons"/> and
/// in logs; their text must not change without a major version bump of the schema.
/// </summary>
public static class FixtureRejectionReasons
{
    public const string WrongCompetition = "wrong_competition";
    public const string InvalidDate = "invalid_date";
    public const string SentinelTeam = "sentinel_team";
    public const string TeamMismatch = "team_mismatch";
    public const string TeamLigaMismatch = "team_liga_mismatch";
    public const string StrideSequenceTooShort = "stride_sequence_too_short";
    public const string OutsideRegion = "outside_region";
    public const string PartialRead = "partial_read";
    public const string ProfileConstraint = "profile_constraint";
    public const string AmbiguousNormalization = "ambiguous_normalization";
}
