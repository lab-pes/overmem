using System.Collections.Generic;
using Overmem.Extensions.Pes2021.Fixtures;

namespace Overmem.Extensions.Pes2021.Players;

/// <summary>
/// Process instance identity used as part of the player-memory cache key. Changing any
/// field produces a new entry; this is what guarantees that a PES restart invalidates the
/// previous session.
/// </summary>
public sealed record PlayerProcessInstanceIdentity(
    Overmem.Abstractions.Processes.AttachmentId AttachmentId,
    int ProcessId,
    System.DateTimeOffset? ProcessStartedAtUtc,
    string ProcessName);

/// <summary>
/// Identity of a player session: process, profile, anchor, validated sample. Both used as
/// the cache value and as the session block of the discovery result.
/// </summary>
public sealed record PlayerSession(
    PlayerProcessInstanceIdentity Process,
    string ProfileId,
    string ProfileVersion,
    string ProfileSha256,
    int RecordStride,
    string ArenaBaseAddress,
    string ArenaStopAddress,
    string AnchorAddress,
    uint AnchorPlayerId,
    string AnchorPlayerNameFingerprint,
    string ValidationSampleSha256,
    System.DateTimeOffset ValidatedAtUtc,
    Overmem.Extensions.Pes2021.Fixtures.CacheDisposition CacheDisposition);

/// <summary>
/// Identity key for <see cref="Pes2021PlayerSessionCache"/>. Bundles every field the cache
/// needs to distinguish two attaches to the same PID.
/// </summary>
public sealed record PlayerSessionCacheKey(
    Overmem.Abstractions.Processes.AttachmentId AttachmentId,
    int ProcessId,
    System.DateTimeOffset? ProcessStartedAtUtc,
    string ProfileId,
    string ProfileVersion,
    string ProfileSha256);

/// <summary>
/// Cached value associated with a key. Mirrors <see cref="PlayerSession"/> plus a
/// <see cref="Disposition"/> so callers can audit reuse vs rediscover.
/// </summary>
public sealed record PlayerSessionCacheEntry(
    CacheDisposition Disposition,
    string ArenaBaseAddress,
    string ArenaStopAddress,
    string AnchorAddress,
    uint AnchorPlayerId,
    string AnchorPlayerNameFingerprint,
    string ValidationSampleSha256,
    System.DateTimeOffset ValidatedAtUtc);

/// <summary>
/// One accepted or rejected memory region for the player-memory diagnostics payload.
/// </summary>
public sealed record PlayerRegionDiagnostic(
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
/// Aggregated counters, timings, and warnings for a discovery run. The collector never
/// reads memory; it only aggregates values reported by the reader, parser, anchor finder,
/// region scanner, and cache.
/// </summary>
public sealed record PlayerDiscoveryDiagnostics(
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
    int DuplicatePlayerIds,
    int AmbiguousResolutions,
    IReadOnlyDictionary<string, int> RejectionReasons,
    IReadOnlyDictionary<string, double> StageDurationMs,
    IReadOnlyList<PlayerRegionDiagnostic> Regions,
    IReadOnlyList<string> Warnings);

/// <summary>
/// Single anchor candidate surfaced by the finder. <see cref="Score"/> is the composite
/// score from cheap validation + neighbor scoring. <see cref="Reasons"/> lists every
/// contributing factor.
/// </summary>
public sealed record PlayerAnchorCandidate(
    string Address,
    uint PlayerId,
    string Fingerprint,
    int Score,
    IReadOnlyList<string> Reasons,
    int PlausibleRunForward,
    int PlausibleRunBackward);

/// <summary>
/// Confidence level for an anchor result. Mirrors the fixture convention.
/// </summary>
public sealed record PlayerAnchorConfidence(
    string Level,
    int Score,
    int MaxScore,
    IReadOnlyList<string> Reasons);

/// <summary>
/// Anchor finder result. <see cref="AnchorAddress"/> is null when no candidate survives
/// scoring; <see cref="Ambiguous"/> is true when ties are not resolved by the profile
/// allowlist.
/// </summary>
public sealed record PlayerAnchorResult(
    PlayerSession Session,
    uint PlayerId,
    string? AnchorAddress,
    int AnchorIndex,
    bool Ambiguous,
    PlayerAnchorConfidence Confidence,
    IReadOnlyList<PlayerAnchorCandidate> Candidates,
    PlayerDiscoveryDiagnostics Diagnostics);

/// <summary>
/// Catalog scan result for the EDIT-base arena.
/// </summary>
public sealed record PlayerDiscoveryResult(
    PlayerSession Session,
    IReadOnlyList<DecodedPlayerRecord> Players,
    PlayerDiscoveryDiagnostics Diagnostics)
{
    public PlayerArenaCoverage? ArenaCoverage { get; init; }
}

/// <summary>
/// Territorial accounting for the single EDIT arena selected by the validated anchor.
/// Empty reserved slots are part of the arena capacity but are not player snapshots.
/// </summary>
public sealed record PlayerArenaCoverage(
    string RegionBaseAddress,
    string RegionStopAddress,
    string FirstRecordAddress,
    string ArenaBaseAddress,
    string ArenaStopAddress,
    int RecordStride,
    int AnchorSlotIndex,
    int PopulatedSlots,
    int EmptyReservedSlots,
    int TheoreticalSlots,
    int UnaccountedSlots,
    string? EmptyRecordSha256,
    string BoundaryClassification);
