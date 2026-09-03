using Microsoft.Extensions.DependencyInjection;
using Overmem.Abstractions.Cli;
using Overmem.Abstractions.Processes;
using Overmem.Application;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Overmem.Extensions.Pes2021.Cli;

public sealed record Pes2021FindCalendarBaseCliCommand(
    ProcessSelector Selector,
    int? Year,
    int? Month,
    int? Day,
    int? RoundValue,
    int? CompetitionCode,
    string? ModuleName,
    int MaxResults) : CliCommand;

// --- Family Discovery System Commands ---

public sealed record Pes2021DiscoverPlayerFamiliesCliCommand(
    ProcessSelector Selector,
    string? ProfilePath,
    string Policy = "DefaultPlayerArena",
    long MaxBytes = 0,
    int TimeoutMs = 0,
    string OutputMode = "Summary") : CliCommand;

public sealed record Pes2021InventoryPlayerHitsCliCommand(
    ProcessSelector Selector,
    string? ProfilePath,
    string Policy = "DefaultPlayerArena") : CliCommand;

public sealed record Pes2021ComparePlayerSessionsCliCommand(
    string BeforeCatalogPath,
    string AfterCatalogPath) : CliCommand;

public sealed record Pes2021ExportFamilyCatalogCliCommand(
    ProcessSelector Selector,
    string OutputPath,
    string? ProfilePath) : CliCommand;

// --- Calendar Commands ---

public sealed record Pes2021DumpCalendarDateCliCommand(
    ProcessSelector Selector,
    int Year,
    int Month,
    int Day,
    ulong? BaseAddress,
    int MaxRecords) : CliCommand;

public sealed record Pes2021CompareCalendarDatesCliCommand(
    ProcessSelector Selector,
    int FirstYear,
    int FirstMonth,
    int FirstDay,
    int SecondYear,
    int SecondMonth,
    int SecondDay,
    ulong? BaseAddress,
    int MaxRecords) : CliCommand;

public sealed record Pes2021CalendarSummaryCliCommand(
    ProcessSelector Selector,
    ulong? BaseAddress,
    int MaxRecords) : CliCommand;

public sealed record Pes2021InventoryAnnualEventsCliCommand(
    ProcessSelector Selector,
    int Year,
    ulong? CalendarBaseAddress,
    ulong? SecondaryBaseAddress) : CliCommand;

public sealed record Pes2021FindSecondaryCalendarBaseByDateCliCommand(
    ProcessSelector Selector,
    int Year,
    int Month,
    int Day,
    string? ModuleName,
    int MaxResults) : CliCommand;

public sealed record Pes2021DumpSecondaryCalendarDayCliCommand(
    ProcessSelector Selector,
    int Year,
    int Month,
    int Day,
    ulong? BaseAddress) : CliCommand;

public sealed record Pes2021ScanRuntimeDayIndexClustersCliCommand(
    ProcessSelector Selector,
    int Year,
    int Month,
    int Day,
    int MaxResults,
    int ClusterGap,
    int PreviewBytes) : CliCommand;

public sealed record Pes2021DumpRuntimeDayPayloadFamilyCliCommand(
    ProcessSelector Selector,
    int Year,
    int Month,
    int Day,
    ulong? StartAddress,
    ulong? StopAddress,
    ulong? CalendarBaseAddress,
    int[] PreferredStrides,
    int MinHitCount,
    int ClusterGap,
    int PreviewBytes) : CliCommand;

public sealed record Pes2021CompareRuntimeDayPayloadFamilyCliCommand(
    ProcessSelector Selector,
    int Year,
    int Month,
    int Day,
    ulong? StartAddress,
    ulong? StopAddress,
    ulong? CalendarBaseAddress,
    int[] PreferredStrides,
    int MinHitCount,
    int ClusterGap,
    int PreviewBytes) : CliCommand;

public sealed record Pes2021DumpRuntimeDayPayloadClusterDetailCliCommand(
    ProcessSelector Selector,
    int Year,
    int Month,
    int Day,
    int ClusterOrdinal,
    ulong? StartAddress,
    ulong? StopAddress,
    ulong? CalendarBaseAddress,
    int[] PreferredStrides,
    int MinHitCount,
    int ClusterGap,
    int PreviewBytes,
    int IntsBeforeHit,
    int IntsAfterHit) : CliCommand;

public sealed record Pes2021AnalyzeRuntimeDayPayloadClusterCliCommand(
    ProcessSelector Selector,
    int Year,
    int Month,
    int Day,
    int ClusterOrdinal,
    ulong? StartAddress,
    ulong? StopAddress,
    ulong? CalendarBaseAddress,
    int[] PreferredStrides,
    int MinHitCount,
    int ClusterGap,
    int PreviewBytes,
    int IntsBeforeHit,
    int IntsAfterHit) : CliCommand;

public sealed record Pes2021ClassifyRuntimeDayVariantCliCommand(
    ProcessSelector Selector,
    int Year,
    int Month,
    int Day,
    ulong? StartAddress,
    ulong? StopAddress,
    ulong? SecondaryBaseAddress,
    ulong? CalendarBaseAddress,
    int[] PreferredStrides,
    int MinHitCount,
    int ClusterGap,
    int PreviewBytes) : CliCommand;

public sealed record Pes2021FindFixtureAnchorCliCommand(
    ProcessSelector Selector,
    int CompetitionId,
    int TeamId,
    int? TeamLiga,
    string? ProfilePath,
    ulong? ScanStartAddress,
    ulong? ScanStopAddress,
    int? BlockRecords,
    ulong? MaxScanBytes,
    string? OutputFile) : CliCommand;

public sealed record Pes2021ExtractCompetitionFixturesCliCommand(
    ProcessSelector Selector,
    int CompetitionId,
    int? TeamId,
    int? TeamLiga,
    ulong? CalendarBaseAddress,
    ulong? CompetitionBlockBaseAddress,
    ulong? AnchorAddress,
    string? ProfileFile,
    string? CompetitionMapFile,
    string? TeamMapFile,
    int? BlockRecords,
    int? RecordLimit,
    string? OutputFile) : CliCommand;

public sealed record Pes2021FindPlayerAnchorCliCommand(
    ProcessSelector Selector,
    uint ControlPlayerId,
    string? ProfileFile,
    string? OutputFile) : CliCommand;

public sealed record Pes2021ScanPlayersCliCommand(
    ProcessSelector Selector,
    uint ControlPlayerId,
    string? ProfileFile,
    string? OutputFile,
    int MaxRecords) : CliCommand;

public sealed record Pes2021QueryPlayerCliCommand(
    ProcessSelector Selector,
    uint PlayerId,
    string? ProfileFile) : CliCommand;

public sealed record Pes2021ExportPlayerCatalogCliCommand(
    ProcessSelector Selector,
    uint ControlPlayerId,
    string? ProfileFile,
    string OutputFile) : CliCommand;

public sealed record Pes2021StrideScanPlayersCliCommand(
    ProcessSelector Selector,
    ulong StartAddress,
    ulong StopAddress,
    int Stride,
    int MaxRecords) : CliCommand;

public sealed record Pes2021ScanAllArenasCliCommand(
    ProcessSelector Selector,
    int Stride,
    int MaxRecordsPerArena,
    ulong MinRegionSize) : CliCommand;
