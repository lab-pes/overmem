namespace Overmem.Extensions.Pes2021.Fixtures;

/// <summary>
/// One competition entry as loaded from the competition map CSV. Conflicts and unresolved
/// entries are reported via <see cref="CatalogConflict"/> in the extraction result; a
/// <see cref="CompetitionMapEntry"/> itself does not carry conflict state.
/// </summary>
public sealed record CompetitionMapEntry(
    CompetitionId CompetitionId,
    string Name,
    string SourcePath,
    string SourceSha256);

/// <summary>
/// One team entry as loaded from the team map CSV. <see cref="TeamLiga"/> is the runtime
/// second u16 that composes <see cref="TeamKey"/>. Legacy catalogs may use
/// <c>secondary_id</c> or <c>league_id</c> as alias columns; those are normalized to
/// <c>teamLiga</c> at load time.
/// </summary>
public sealed record TeamMapEntry(
    TeamKey Key,
    string Name,
    string? ShortName,
    string? Source,
    string? EvidenceStatus,
    string SourcePath,
    string SourceSha256);

/// <summary>
/// Conflict reported when the same composite key appears with conflicting names in the
/// loaded catalog. Such keys never resolve to a name and always surface in the output.
/// </summary>
public sealed record CatalogConflict(
    TeamKey Key,
    IReadOnlyList<string> ConflictingNames,
    IReadOnlyList<string> SourcePaths);

/// <summary>
/// Confidence reported for an anchor discovery attempt. <see cref="Level"/> derives from
/// <see cref="Score"/> against <see cref="MaxScore"/> and the captured <see cref="Reasons"/>.
/// </summary>
public sealed record DiscoveryConfidence(
    string Level,
    int Score,
    int MaxScore,
    IReadOnlyList<string> Reasons);

/// <summary>
/// One candidate for the anchor address. The service returns them all (up to a cap) so the
/// caller can see why a tie was broken or refused.
/// </summary>
public sealed record AnchorCandidate(
    string Address,
    int Score,
    IReadOnlyList<string> Reasons,
    int PlausibleRunForward,
    int PlausibleRunBackward,
    int CompetitionRun,
    bool PartialRead,
    bool RegionCrossing);

/// <summary>
/// Outcome of an anchor discovery call. <see cref="AnchorAddress"/> is set when exactly one
/// candidate survives; otherwise it is null and the caller must decide.
/// </summary>
public sealed record FixtureAnchorResult(
    CalendarSession Session,
    CompetitionId CompetitionId,
    TeamKey? RequestedTeamKey,
    ushort RequestedTeamId,
    string? AnchorAddress,
    string? CompetitionBlockBaseAddress,
    string? CalendarArrayBaseAddress,
    int? AnchorIndex,
    DiscoveryConfidence Confidence,
    IReadOnlyList<AnchorCandidate> Candidates,
    ExtractionDiagnostics Diagnostics);

/// <summary>
/// Top-level payload of <c>pes2021_extract_competition_fixtures</c>. The schema version is
/// pinned to <c>pes2021.competition-fixtures.v1</c>; any change to the wire shape requires
/// a new schema version. The status is always <c>FIXTURES_ONLY</c> for v1: standings are not
/// derived from raw scores.
/// </summary>
public sealed record CompetitionFixtureExtractionResult(
    string SchemaVersion,
    FixtureExtractionStatus Status,
    string Warning,
    CalendarSession Session,
    CompetitionId CompetitionId,
    string? CompetitionName,
    NameResolutionStatus CompetitionNameStatus,
    string RecordIndexOrigin,
    int FixtureCount,
    int DistinctTeamCount,
    IReadOnlyList<TeamKey> UnresolvedTeamKeys,
    IReadOnlyList<CatalogConflict> CatalogConflicts,
    IReadOnlyList<Fixture> Fixtures,
    ExtractionDiagnostics Diagnostics)
{
    public const string CurrentSchemaVersion = "pes2021.competition-fixtures.v1";
    public const string CurrentWarning = "Raw scores do not prove that a fixture was completed. Do not derive standings from this payload.";
}
