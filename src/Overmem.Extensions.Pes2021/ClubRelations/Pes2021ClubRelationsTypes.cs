using System;

namespace Overmem.Extensions.Pes2021.ClubRelations;

public enum ClubObservationSource
{
    PlayerBlock,
    ClubRecord,
    CalendarRecord,
    StaticTable,
    UiComparison
}

public enum ClubObservationStatus
{
    ConfirmedRuntime,
    ConfirmedCalendar,
    Candidate,
    Unknown
}

public enum ClubUnresolvedReason
{
    AnchorNotFound,
    CatalogCollision,
    CatalogUnreadable,
    RegionUnreadable,
    RestartNotObserved,
    NameNotLocated,
    IdNotLocated,
    InconsistentAcrossRuns
}

public sealed record Pes2021ClubCatalogRow(
    int TeamId,
    int SecondaryId,
    string Name,
    string ShortName,
    string? CityOrStadium,
    ulong Address,
    ulong RegionBase,
    ulong RegionOffset,
    string SourcePath,
    string SourceSha256);

public sealed record Pes2021CompetitionMapEntry(int CompetitionId, string Name);

public sealed record Pes2021ClubObservationRow(
    Guid RunId,
    string ControlCase,
    string UiClub,
    string UiLeague,
    string UiCountry,
    int? PlayerId,
    string? PlayerName,
    int TeamId,
    int SecondaryId,
    ulong? ClubRecordAddress,
    int? CountryIdRaw,
    int? CompetitionIdRaw,
    string Source,
    string Status,
    string Notes);

public sealed record Pes2021ClubUnresolvedRow(
    Guid RunId,
    int TeamId,
    int SecondaryId,
    string? Name,
    string Reason,
    string Notes);

public sealed record Pes2021RegionSnapshotRow(
    Guid RunId,
    ulong RegionBaseAddress,
    ulong RegionSize,
    string RegionState,
    string RegionProtection,
    string RegionType,
    bool IsReadable,
    bool IsWritable,
    bool IsExecutable,
    bool IsIncluded);

public sealed record Pes2021RegionBlockRow(
    Guid RunId,
    ulong RegionBaseAddress,
    int BlockIndex,
    ulong BlockOffset,
    int BlockBytes,
    string Sha256);

public sealed record Pes2021ClubScanResult(
    Guid RunId,
    int ProcessId,
    DateTimeOffset ProcessStartedAtUtc,
    string ProcessName,
    string CatalogPath,
    string CatalogSha256,
    string CompetitionMapPath,
    string CompetitionMapSha256,
    int AnchorSantosFound,
    int AnchorAthleticoParanaenseFound,
    int AnchorRosarioCentralFound,
    int RegionTotal,
    int RegionReadablePrivate,
    int BlockCount,
    int ObservationCount,
    int UnresolvedCount,
    long ScanDurationMs,
    string OutputDirectory);
