namespace Overmem.Extensions.Pes2021.Fixtures;

/// <summary>
/// Decoded calendar record before any name resolution. The parser does not consult catalogs,
/// only the profile-driven layout. Every numeric field is a <c>u16</c>/<c>u8</c> from the
/// profile's record layout; sentinels are preserved so the resolver can report them.
/// </summary>
public sealed record RawCalendarRecord(
    int RecordIndex,
    ulong Address,
    CompetitionId CompetitionId,
    byte Round,
    ushort Year,
    byte Month,
    byte Day,
    TeamKey Home,
    TeamKey Away,
    byte HomeScoreRaw,
    byte AwayScoreRaw);

/// <summary>
/// One team in a <see cref="Fixture"/>, enriched with whatever name resolution could provide.
/// When <see cref="Name"/> is <c>null</c> the <see cref="ResolutionStatus"/> tells the caller
/// exactly why (no catalog entry, ambiguous or conflicting keys, etc.).
/// </summary>
public sealed record FixtureParticipant(
    TeamKey Key,
    string? Name,
    NameResolutionStatus ResolutionStatus,
    string? ResolutionSource);

/// <summary>
/// Decoded fixture enriched with name resolution and the raw score pair. Raw scores are
/// preserved without inference: a <c>0–0</c> pair does not prove a fixture was played.
/// </summary>
public sealed record Fixture(
    int RecordIndex,
    string Address,
    CompetitionId CompetitionId,
    byte Round,
    DateOnly Date,
    FixtureParticipant Home,
    FixtureParticipant Away,
    byte HomeScoreRaw,
    byte AwayScoreRaw,
    RawScoreState ScoreState);
