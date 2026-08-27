using System.Collections.Generic;

namespace Overmem.Extensions.Pes2021.Fixtures;

/// <summary>
/// Profile-driven description of how a single PES 2021 calendar record is laid out. The
/// offsets and types here come from the profile JSON (schema
/// <c>pes2021.fixture-profile.v1</c>) and are validated at load time. Anything patch
/// specific must live in the profile; constants in services are not allowed.
/// </summary>
public sealed record Pes2021RecordLayout(
    int Stride,
    int CompetitionIdOffset,
    int RoundOffset,
    int YearOffset,
    int MonthOffset,
    int DayOffset,
    int HomeTeamIdOffset,
    int HomeTeamLigaOffset,
    int AwayTeamIdOffset,
    int AwayTeamLigaOffset,
    int HomeScoreOffset,
    int AwayScoreOffset);

/// <summary>
/// Per-field validation rules for a calendar record. The parser rejects records that fall
/// outside these ranges, with stable rejection reasons.
/// </summary>
public sealed record Pes2021RecordValidation(
    ushort MinimumYear,
    ushort MaximumYear,
    byte MinimumRound,
    byte MaximumRound,
    IReadOnlyList<ushort> TeamIdSentinels);

/// <summary>
/// Description of how calendar regions are filtered before any anchor search or extraction.
/// Mirrors <see cref="Overmem.Abstractions.Memory.MemoryRegionInfo"/> but is profile-driven.
/// </summary>
public sealed record Pes2021RegionFilter(
    IReadOnlyList<string> States,
    IReadOnlyList<string> Types,
    bool RequireReadable,
    bool RequireWritable,
    bool AllowExecutable,
    int ChunkBytes);

/// <summary>
/// Tunables used by the anchor finder: how many records to look at on each side of a hit,
/// minimum run lengths and confidence thresholds. The thresholds are interpreted by the
/// anchor finder and surfaced in <see cref="DiscoveryConfidence"/>.
/// </summary>
public sealed record Pes2021AnchorValidation(
    int RecordsBefore,
    int RecordsAfter,
    int MinimumPlausibleRun,
    int MinimumCompetitionRun,
    int MediumScore,
    int HighScore);

/// <summary>
/// Strategy the profile requests for finding the array base. <see cref="Strategy"/> is the
/// name from the profile JSON; the supporting fields depend on the strategy.
/// </summary>
public sealed record Pes2021Normalization(
    NormalizationStrategy Strategy,
    int? KnownSeasonStartIndex,
    IReadOnlyList<int> ValidationSampleIndices);

/// <summary>
/// Tunables for the block reader and the per-competition extraction stop policy. They come
/// straight from the profile so the user can tune them per patch without code changes.
/// </summary>
public sealed record Pes2021CalendarLimits(
    int DefaultBlockRecords,
    int MaxBlockRecords,
    int RecordLimit,
    int MaxConsecutiveNonCompetitionRecords);

/// <summary>
/// Default optional catalog paths declared by the profile. They are resolved relative to the
/// profile directory unless an absolute path is supplied. They are only used when no explicit
/// catalog paths are passed to the service.
/// </summary>
public sealed record Pes2021ProfileMaps(
    string? CompetitionMapPath,
    string? TeamMapPath);

/// <summary>
/// A loaded PES 2021 fixture profile. <see cref="Sha256"/> is computed over the original JSON
/// bytes so any later content change invalidates the cache key. The combination of
/// <see cref="ProfileId"/> and <see cref="ProfileVersion"/> is the semantic identity;
/// <see cref="Sha256"/> is the byte-level identity.
/// </summary>
public sealed record Pes2021FixtureProfile(
    string SchemaVersion,
    string ProfileId,
    string ProfileVersion,
    string EvidenceStatus,
    IReadOnlyList<string> ProcessNames,
    Pes2021RecordLayout RecordLayout,
    Pes2021CalendarLimits Calendar,
    Pes2021RecordValidation RecordValidation,
    Pes2021RegionFilter RegionFilter,
    Pes2021AnchorValidation AnchorValidation,
    Pes2021Normalization Normalization,
    Pes2021ProfileMaps Maps,
    string Sha256,
    string SourcePath)
{
    public int Stride => RecordLayout.Stride;
}
