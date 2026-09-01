using System.Collections.Generic;

namespace Overmem.Extensions.Pes2021.Players;

/// <summary>
/// Field types supported by the player-record profile. Only these are accepted by
/// <see cref="Pes2021PlayerProfileLoader"/>; anything else raises
/// <c>PES2021_PLAYER_PROFILE_INVALID</c>.
/// </summary>
public enum Pes2021PlayerFieldType
{
    U8,
    I8,
    U16Le,
    U32Le,
    I32Le,
    FixedAscii,
    I8X4,
}

/// <summary>
/// Display transform applied to a raw value. The list is closed; new transforms require a
/// profile-schema version bump.
/// </summary>
public enum Pes2021PlayerTransform
{
    None,
    RawMul100Eur,
    TrimAsciiZ,
    Bitfield,
}

/// <summary>
/// Epistemic labels used in the player-memory profile. They follow the labels defined in
/// <c>docs/pes2021/player-memory/feasibility-study.md</c> and <c>implementation-packages.md</c>.
/// </summary>
public enum Pes2021PlayerEvidenceStatus
{
    Confirmed,
    Candidate,
    Unknown,
    Refuted,
}

/// <summary>
/// Classification of a record by the session context in which it was observed.
/// </summary>
public enum Pes2021PlayerContext
{
    EditBaseCandidate,
    EditBaseConfirmed,
    MasterLeagueCandidate,
    MasterLeagueConfirmed,
    UiOrRuntimeCache,
    UnknownContext,
}

/// <summary>
/// Per-field validation rules for a player record. The parser rejects records that fall
/// outside these ranges.
/// </summary>
public sealed record Pes2021PlayerRecordValidation(
    byte MinimumHeight,
    byte MaximumHeight,
    byte MinimumWeight,
    byte MaximumWeight,
    uint MinimumPlayerId,
    uint MaximumPlayerId);

/// <summary>
/// One bitfield sub-field packed inside a byte container.
/// </summary>
public sealed record Pes2021PlayerBitField(
    string Name,
    int BitStart,
    int BitLength,
    Pes2021PlayerEvidenceStatus ReadStatus,
    Pes2021PlayerEvidenceStatus WriteStatus);

/// <summary>
/// Single field definition. Width is in bytes for scalar/fixed-width types and total bytes
/// for <see cref="Pes2021PlayerFieldType.FixedAscii"/> and <see cref="Pes2021PlayerFieldType.I8X4"/>.
/// </summary>
public sealed record Pes2021PlayerFieldDefinition(
    string Name,
    int Offset,
    int Width,
    Pes2021PlayerFieldType Type,
    string Signedness,
    string Endianness,
    Pes2021PlayerTransform Transform,
    Pes2021PlayerEvidenceStatus ReadStatus,
    Pes2021PlayerEvidenceStatus WriteStatus,
    IReadOnlyList<Pes2021PlayerContext> ValidContexts,
    bool SharedBitfield,
    IReadOnlyList<Pes2021PlayerBitField>? Bits,
    string? Notes);

/// <summary>
/// Description of how a player record is laid out. Stride is the size in bytes of one
/// record. <see cref="StartOffset"/> is the byte offset of the first valid record byte;
/// for the standard EDIT profile this is 0.
/// </summary>
public sealed record Pes2021PlayerRecordLayout(
    int Stride,
    int StartOffset,
    IReadOnlyList<Pes2021PlayerFieldDefinition> Fields);

/// <summary>
/// Description of how player-record regions are filtered before any anchor search or
/// extraction.
/// </summary>
public sealed record Pes2021PlayerRegionFilter(
    IReadOnlyList<string> States,
    IReadOnlyList<string> Types,
    bool RequireReadable,
    bool RequireWritable,
    bool AllowExecutable,
    int ChunkBytes);

/// <summary>
/// Tunables used by the anchor finder and the catalog scanner.
/// </summary>
public sealed record Pes2021PlayerAnchorValidation(
    int RecordsBefore,
    int RecordsAfter,
    int MinimumRun,
    int MinimumAnchorScore,
    int MediumScore,
    int HighScore,
    IReadOnlyList<uint> ControlPlayerIds);

/// <summary>
/// Tunables for catalog and discovery performance/budgeting.
/// </summary>
public sealed record Pes2021PlayerLimits(
    int DefaultBlockRecords,
    int MaxBlockRecords,
    int MaxRecordsReturned,
    int ScanBudgetMs);

/// <summary>
/// Source-of-evidence metadata for the profile. Carries the CT SHA-256 and the schema_v5
/// Lua SHA-256 so the profile is traceable to the studies.
/// </summary>
public sealed record Pes2021PlayerProfileSources(
    string? CtPath,
    string? CtSha256,
    string? SchemaV5LuaSha256);

/// <summary>
/// A loaded PES 2021 player-record profile. <see cref="Sha256"/> is computed over the
/// original JSON bytes. <see cref="ProfileId"/> + <see cref="ProfileVersion"/> are the
/// semantic identity; <see cref="Sha256"/> is the byte-level identity.
/// </summary>
public sealed record Pes2021PlayerProfile(
    string SchemaVersion,
    string ProfileId,
    string ProfileVersion,
    Pes2021PlayerEvidenceStatus EvidenceStatus,
    IReadOnlyList<string> ProcessNames,
    Pes2021PlayerRecordLayout RecordLayout,
    Pes2021PlayerRecordValidation RecordValidation,
    Pes2021PlayerRegionFilter RegionFilter,
    Pes2021PlayerAnchorValidation AnchorValidation,
    Pes2021PlayerLimits Limits,
    Pes2021PlayerProfileSources Sources,
    string Sha256,
    string SourcePath)
{
    public int Stride => RecordLayout.Stride;
}
