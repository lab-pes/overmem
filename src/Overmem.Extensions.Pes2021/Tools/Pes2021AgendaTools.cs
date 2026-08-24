using ModelContextProtocol.Server;
using Overmem.Abstractions.Processes;
using Overmem.Extensions.Pes2021;
using System.ComponentModel;

namespace Overmem.Extensions.Pes2021.Tools;

[McpServerToolType]
public sealed class Pes2021AgendaTools(Pes2021AgendaService agendaService)
{
    [McpServerTool(Name = "pes2021_agenda_guide"), Description("Read the PES 2021 Master League CT and return the agenda references, offsets, search priorities, and recommended commands.")]
    public Task<Pes2021AgendaGuide> AgendaGuide(
        [Description("Optional path to the PES 2021 Cheat Engine table. Defaults to the local #PC table if present.")] string? cheatTablePath = null,
        CancellationToken cancellationToken = default)
        => agendaService.GetGuideAsync(cheatTablePath, cancellationToken);

    [McpServerTool(Name = "pes2021_calendar_search_priorities"), Description("Return the agenda search priorities and fallback regions derived from the PES 2021 inspector.")]
    public Task<IReadOnlyList<Pes2021CalendarSearchPriority>> CalendarSearchPriorities(
        [Description("Optional path to the PES 2021 Cheat Engine table. Defaults to the local #PC table if present.")] string? cheatTablePath = null,
        CancellationToken cancellationToken = default)
        => agendaService.GetSearchPrioritiesAsync(cheatTablePath, cancellationToken);

    [McpServerTool(Name = "pes2021_find_calendar_base"), Description("Find and normalize the PES 2021 Master League calendar base from a visible date or season anchor.")]
    public Task<Pes2021CalendarBaseResult> FindCalendarBase(
        [Description("The attachment identifier returned by attach_process.")] Guid attachmentId,
        [Description("Visible year to search. Leave empty to try the season anchor years from the CT.")] int? year = null,
        [Description("Visible month to search. Defaults to 2 when omitted.")] int? month = null,
        [Description("Visible day to search. Defaults to 1 when omitted.")] int? day = null,
        [Description("Visible round to search. Defaults to 1 when omitted.")] int? roundValue = null,
        [Description("Competition code used by the date pattern. Defaults to 29 (Brasileirão Série A) when omitted.")] int? competitionCode = null,
        [Description("Optional module name to constrain the pattern scan.")] string? moduleName = null,
        [Description("Maximum pattern matches to consider per search pass.")] int maxResults = 100,
        CancellationToken cancellationToken = default)
        => agendaService.FindBaseAsync(
            new AttachmentId(attachmentId),
            year,
            month,
            day,
            roundValue,
            competitionCode,
            moduleName,
            maxResults,
            cancellationToken);

    [McpServerTool(Name = "pes2021_dump_calendar_date"), Description("Dump all fixtures on a specific PES 2021 Master League date using the cached or discovered calendar base.")]
    public Task<Pes2021CalendarDateReport> DumpCalendarDate(
        [Description("The attachment identifier returned by attach_process.")] Guid attachmentId,
        [Description("Calendar year to inspect.")] int year,
        [Description("Calendar month to inspect.")] int month,
        [Description("Calendar day to inspect.")] int day,
        [Description("Optional base address returned by pes2021_find_calendar_base.")] ulong? baseAddress = null,
        [Description("Maximum records to scan.")] int maxRecs = 13014,
        CancellationToken cancellationToken = default)
        => agendaService.DumpDateAsync(new AttachmentId(attachmentId), year, month, day, baseAddress, maxRecs, cancellationToken);

    [McpServerTool(Name = "pes2021_compare_calendar_dates"), Description("Compare two PES 2021 Master League dates using the same normalized main calendar base and summarize the competition families and round progression.")]
    public Task<Pes2021CalendarDateComparisonReport> CompareCalendarDates(
        [Description("The attachment identifier returned by attach_process.")] Guid attachmentId,
        [Description("First calendar year to inspect.")] int firstYear,
        [Description("First calendar month to inspect.")] int firstMonth,
        [Description("First calendar day to inspect.")] int firstDay,
        [Description("Second calendar year to inspect.")] int secondYear,
        [Description("Second calendar month to inspect.")] int secondMonth,
        [Description("Second calendar day to inspect.")] int secondDay,
        [Description("Optional base address returned by pes2021_find_calendar_base.")] ulong? baseAddress = null,
        [Description("Maximum records to scan.")] int maxRecs = 13014,
        CancellationToken cancellationToken = default)
        => agendaService.CompareDatesAsync(new AttachmentId(attachmentId), firstYear, firstMonth, firstDay, secondYear, secondMonth, secondDay, baseAddress, maxRecs, cancellationToken);

    [McpServerTool(Name = "pes2021_calendar_summary"), Description("Summarize the full PES 2021 Master League agenda by date and competition.")]
    public Task<Pes2021CalendarSummary> CalendarSummary(
        [Description("The attachment identifier returned by attach_process.")] Guid attachmentId,
        [Description("Optional base address returned by pes2021_find_calendar_base.")] ulong? baseAddress = null,
        [Description("Maximum records to scan.")] int maxRecs = 13014,
        CancellationToken cancellationToken = default)
        => agendaService.CalendarSummaryAsync(new AttachmentId(attachmentId), baseAddress, maxRecs, cancellationToken);

    [McpServerTool(Name = "pes2021_inspect_secondary_calendar_candidate"), Description("Score a candidate PES 2021 secondary calendar base, which is useful for fixtures and schedule-like agenda data.")]
    public Task<Pes2021SecondaryCalendarCandidateReport> InspectSecondaryCalendarCandidate(
        [Description("The attachment identifier returned by attach_process.")] Guid attachmentId,
        [Description("Candidate base address to inspect.")] ulong baseValue,
        [Description("Optional sample days to score. Leave empty to use the CT defaults.")] int[]? sampleDays = null,
        CancellationToken cancellationToken = default)
        => agendaService.InspectSecondaryCalendarCandidateAsync(new AttachmentId(attachmentId), baseValue, sampleDays, cancellationToken);

    [McpServerTool(Name = "pes2021_scan_secondary_calendar_candidates"), Description("Scan a raw address range for PES 2021 secondary calendar candidates.")]
    public Task<IReadOnlyList<Pes2021SecondaryCalendarCandidateReport>> ScanSecondaryCalendarCandidates(
        [Description("The attachment identifier returned by attach_process.")] Guid attachmentId,
        [Description("Inclusive start address for the scan.")] ulong startValue,
        [Description("Inclusive stop address for the scan.")] ulong stopValue,
        [Description("Scan step in bytes. Defaults to 0x10.")] ulong step = 0x10,
        [Description("Maximum number of candidates to keep.")] int maxResults = 10,
        [Description("Optional sample days to score. Leave empty to use the CT defaults.")] int[]? sampleDays = null,
        CancellationToken cancellationToken = default)
        => agendaService.ScanSecondaryCalendarCandidatesAsync(new AttachmentId(attachmentId), startValue, stopValue, step, maxResults, sampleDays, cancellationToken);

    [McpServerTool(Name = "pes2021_find_secondary_calendar_base_by_date"), Description("Find the PES 2021 secondary-calendar base by scanning for a visible date in the day-header events.")]
    public Task<Pes2021SecondaryCalendarBaseResult> FindSecondaryCalendarBaseByDate(
        [Description("The attachment identifier returned by attach_process.")] Guid attachmentId,
        [Description("Calendar year to search inside the secondary-calendar headers.")] int year,
        [Description("Calendar month to search inside the secondary-calendar headers.")] int month,
        [Description("Calendar day to search inside the secondary-calendar headers.")] int day,
        [Description("Optional module name to constrain the pattern scan.")] string? moduleName = null,
        [Description("Maximum raw date hits to consider before scoring candidate bases.")] int maxResults = 256,
        CancellationToken cancellationToken = default)
        => agendaService.FindSecondaryBaseByDateAsync(new AttachmentId(attachmentId), year, month, day, moduleName, maxResults, cancellationToken);

    [McpServerTool(Name = "pes2021_dump_secondary_calendar_day"), Description("Dump one PES 2021 secondary-calendar day entry, including header events, item list, and count.")]
    public Task<Pes2021SecondaryCalendarDayReport> DumpSecondaryCalendarDay(
        [Description("The attachment identifier returned by attach_process.")] Guid attachmentId,
        [Description("Calendar year to inspect.")] int year,
        [Description("Calendar month to inspect.")] int month,
        [Description("Calendar day to inspect.")] int day,
        [Description("Optional secondary-calendar base returned by pes2021_find_secondary_calendar_base_by_date.")] ulong? baseAddress = null,
        CancellationToken cancellationToken = default)
        => agendaService.DumpSecondaryDayAsync(new AttachmentId(attachmentId), year, month, day, baseAddress, cancellationToken);

    [McpServerTool(Name = "pes2021_scan_runtime_day_index_clusters"), Description("Scan the live PES 2021 process for runtime clusters that materialize a selected calendar day index in private heap/cache structures.")]
    public Task<Pes2021RuntimeCalendarScanReport> ScanRuntimeDayIndexClusters(
        [Description("The attachment identifier returned by attach_process.")] Guid attachmentId,
        [Description("Calendar year to inspect.")] int year,
        [Description("Calendar month to inspect.")] int month,
        [Description("Calendar day to inspect.")] int day,
        [Description("Maximum raw hits to keep from the exact day-index scan.")] int maxResults = 5000,
        [Description("Maximum byte gap allowed between hits inside the same cluster.")] int clusterGap = 0x1000,
        [Description("Number of bytes to decode from the start of each cluster.")] int previewBytes = 0x100,
        CancellationToken cancellationToken = default)
        => agendaService.ScanRuntimeDayIndexClustersAsync(new AttachmentId(attachmentId), year, month, day, maxResults, clusterGap, previewBytes, cancellationToken);

    [McpServerTool(Name = "pes2021_dump_runtime_day_payload_family"), Description("Scan readable private heap regions for the selected PES 2021 day index and return the focused runtime cache family, including optional preview record decode against the Master League main calendar array.")]
    public Task<Pes2021RuntimeCalendarFamilyReport> DumpRuntimeDayPayloadFamily(
        [Description("The attachment identifier returned by attach_process.")] Guid attachmentId,
        [Description("Calendar year to inspect.")] int year,
        [Description("Calendar month to inspect.")] int month,
        [Description("Calendar day to inspect.")] int day,
        [Description("Optional inclusive scan start address. Leave empty to scan every readable private heap region.")] ulong? startAddress = null,
        [Description("Optional exclusive scan stop address. Leave empty to scan every readable private heap region.")] ulong? stopAddress = null,
        [Description("Optional base address returned by pes2021_find_calendar_base. When omitted, the service will try to use the cached or discovered main calendar base to decode preview IDs into records.")] ulong? calendarBaseAddress = null,
        [Description("Preferred cluster strides to keep. Defaults to 472 and 528 when omitted.")] int[]? preferredStrides = null,
        [Description("Minimum number of raw day-index hits required for a cluster to be kept.")] int minHitCount = 3,
        [Description("Maximum byte gap allowed between hits inside the same cluster.")] int clusterGap = 0x1000,
        [Description("Number of bytes to decode from the start of each cluster.")] int previewBytes = 0x100,
        CancellationToken cancellationToken = default)
        => agendaService.DumpRuntimeDayPayloadFamilyAsync(
            new AttachmentId(attachmentId),
            year,
            month,
            day,
            startAddress,
            stopAddress,
            calendarBaseAddress,
            preferredStrides,
            minHitCount,
            clusterGap,
            previewBytes,
            cancellationToken);

    [McpServerTool(Name = "pes2021_compare_runtime_day_payload_family"), Description("Compare the focused runtime day-payload family for the selected PES 2021 date against the previous and next days, highlighting which preview IDs are unique to the current day.")]
    public Task<Pes2021RuntimeCalendarFamilyComparisonReport> CompareRuntimeDayPayloadFamily(
        [Description("The attachment identifier returned by attach_process.")] Guid attachmentId,
        [Description("Calendar year to inspect.")] int year,
        [Description("Calendar month to inspect.")] int month,
        [Description("Calendar day to inspect.")] int day,
        [Description("Optional inclusive scan start address. Leave empty to scan every readable private heap region.")] ulong? startAddress = null,
        [Description("Optional exclusive scan stop address. Leave empty to scan every readable private heap region.")] ulong? stopAddress = null,
        [Description("Optional base address returned by pes2021_find_calendar_base. When omitted, the service will try to use the cached or discovered main calendar base to decode preview IDs into records.")] ulong? calendarBaseAddress = null,
        [Description("Preferred cluster strides to keep. Defaults to 472 and 528 when omitted.")] int[]? preferredStrides = null,
        [Description("Minimum number of raw day-index hits required for a cluster to be kept.")] int minHitCount = 3,
        [Description("Maximum byte gap allowed between hits inside the same cluster.")] int clusterGap = 0x1000,
        [Description("Number of bytes to decode from the start of each cluster.")] int previewBytes = 0x100,
        CancellationToken cancellationToken = default)
        => agendaService.CompareRuntimeDayPayloadFamilyAsync(
            new AttachmentId(attachmentId),
            year,
            month,
            day,
            startAddress,
            stopAddress,
            calendarBaseAddress,
            preferredStrides,
            minHitCount,
            clusterGap,
            previewBytes,
            cancellationToken);

    [McpServerTool(Name = "pes2021_dump_runtime_day_payload_cluster_detail"), Description("Dump the detailed local Int32 windows around each sample hit of one focused PES 2021 runtime day-payload cluster.")]
    public Task<Pes2021RuntimeCalendarClusterDetailReport> DumpRuntimeDayPayloadClusterDetail(
        [Description("The attachment identifier returned by attach_process.")] Guid attachmentId,
        [Description("Calendar year to inspect.")] int year,
        [Description("Calendar month to inspect.")] int month,
        [Description("Calendar day to inspect.")] int day,
        [Description("Zero-based cluster ordinal from the focused family report.")] int clusterOrdinal,
        [Description("Optional inclusive scan start address. Leave empty to scan every readable private heap region.")] ulong? startAddress = null,
        [Description("Optional exclusive scan stop address. Leave empty to scan every readable private heap region.")] ulong? stopAddress = null,
        [Description("Optional base address returned by pes2021_find_calendar_base. When omitted, the service will try to use the cached or discovered main calendar base to decode preview IDs into records.")] ulong? calendarBaseAddress = null,
        [Description("Preferred cluster strides to keep. Defaults to 472 and 528 when omitted.")] int[]? preferredStrides = null,
        [Description("Minimum number of raw day-index hits required for a cluster to be kept.")] int minHitCount = 3,
        [Description("Maximum byte gap allowed between hits inside the same cluster.")] int clusterGap = 0x1000,
        [Description("Number of bytes to decode from the start of each cluster.")] int previewBytes = 0x100,
        [Description("How many Int32 values to capture before each hit address.")] int intsBeforeHit = 8,
        [Description("How many Int32 values to capture from each hit address onward.")] int intsAfterHit = 24,
        CancellationToken cancellationToken = default)
        => agendaService.DumpRuntimeDayPayloadClusterDetailAsync(
            new AttachmentId(attachmentId),
            year,
            month,
            day,
            clusterOrdinal,
            startAddress,
            stopAddress,
            calendarBaseAddress,
            preferredStrides,
            minHitCount,
            clusterGap,
            previewBytes,
            intsBeforeHit,
            intsAfterHit,
            cancellationToken);

    [McpServerTool(Name = "pes2021_analyze_runtime_day_payload_cluster"), Description("Analyze one focused PES 2021 runtime day-payload cluster into a reusable signature: common anchor prefix, unresolved preview IDs, frequent tail IDs, and contiguous tail runs per hit.")]
    public Task<Pes2021RuntimeCalendarClusterSignatureReport> AnalyzeRuntimeDayPayloadCluster(
        [Description("The attachment identifier returned by attach_process.")] Guid attachmentId,
        [Description("Calendar year to inspect.")] int year,
        [Description("Calendar month to inspect.")] int month,
        [Description("Calendar day to inspect.")] int day,
        [Description("Zero-based cluster ordinal from the focused family report.")] int clusterOrdinal,
        [Description("Optional inclusive scan start address. Leave empty to scan every readable private heap region.")] ulong? startAddress = null,
        [Description("Optional exclusive scan stop address. Leave empty to scan every readable private heap region.")] ulong? stopAddress = null,
        [Description("Optional base address returned by pes2021_find_calendar_base. When omitted, the service will try to use the cached or discovered main calendar base to decode preview IDs into records.")] ulong? calendarBaseAddress = null,
        [Description("Preferred cluster strides to keep. Defaults to 472 and 528 when omitted.")] int[]? preferredStrides = null,
        [Description("Minimum number of raw day-index hits required for a cluster to be kept.")] int minHitCount = 3,
        [Description("Maximum byte gap allowed between hits inside the same cluster.")] int clusterGap = 0x1000,
        [Description("Number of bytes to decode from the start of each cluster.")] int previewBytes = 0x100,
        [Description("How many Int32 values to capture before each hit address.")] int intsBeforeHit = 8,
        [Description("How many Int32 values to capture from each hit address onward.")] int intsAfterHit = 24,
        CancellationToken cancellationToken = default)
        => agendaService.AnalyzeRuntimeDayPayloadClusterAsync(
            new AttachmentId(attachmentId),
            year,
            month,
            day,
            clusterOrdinal,
            startAddress,
            stopAddress,
            calendarBaseAddress,
            preferredStrides,
            minHitCount,
            clusterGap,
            previewBytes,
            intsBeforeHit,
            intsAfterHit,
            cancellationToken);

    [McpServerTool(Name = "pes2021_classify_runtime_day_variant"), Description("Classify the selected PES 2021 day into a provisional runtime variant using the secondary-calendar day shape plus the focused runtime-family scan. This is heuristic and always returns explicit reasons.")]
    public Task<Pes2021RuntimeCalendarVariantReport> ClassifyRuntimeDayVariant(
        [Description("The attachment identifier returned by attach_process.")] Guid attachmentId,
        [Description("Calendar year to inspect.")] int year,
        [Description("Calendar month to inspect.")] int month,
        [Description("Calendar day to inspect.")] int day,
        [Description("Optional inclusive scan start address. Leave empty to scan every readable private heap region.")] ulong? startAddress = null,
        [Description("Optional exclusive scan stop address. Leave empty to scan every readable private heap region.")] ulong? stopAddress = null,
        [Description("Optional secondary-calendar base returned by pes2021_find_secondary_calendar_base_by_date.")] ulong? secondaryBaseAddress = null,
        [Description("Optional base address returned by pes2021_find_calendar_base. When omitted, the service will try to use the cached or discovered main calendar base to decode preview IDs into records.")] ulong? calendarBaseAddress = null,
        [Description("Preferred cluster strides to keep. Defaults to 472 and 528 when omitted.")] int[]? preferredStrides = null,
        [Description("Minimum number of raw day-index hits required for a cluster to be kept.")] int minHitCount = 3,
        [Description("Maximum byte gap allowed between hits inside the same cluster.")] int clusterGap = 0x1000,
        [Description("Number of bytes to decode from the start of each cluster.")] int previewBytes = 0x100,
        CancellationToken cancellationToken = default)
        => agendaService.ClassifyRuntimeDayVariantAsync(
            new AttachmentId(attachmentId),
            year,
            month,
            day,
            startAddress,
            stopAddress,
            secondaryBaseAddress,
            calendarBaseAddress,
            preferredStrides,
            minHitCount,
            clusterGap,
            previewBytes,
            cancellationToken);

    [McpServerTool(Name = "pes2021_inventory_annual_events"), Description("Inventory the special days of one PES 2021 season year from the main calendar plus secondary calendar, and attach any confirmed semantic labels already known for this save context.")]
    public Task<Pes2021AnnualCalendarEventInventoryReport> InventoryAnnualEvents(
        [Description("The attachment identifier returned by attach_process.")] Guid attachmentId,
        [Description("Season year to inventory.")] int year,
        [Description("Optional base address returned by pes2021_find_calendar_base.")] ulong? calendarBaseAddress = null,
        [Description("Optional secondary-calendar base returned by pes2021_find_secondary_calendar_base_by_date.")] ulong? secondaryBaseAddress = null,
        CancellationToken cancellationToken = default)
        => agendaService.InventoryAnnualEventsAsync(
            new AttachmentId(attachmentId),
            year,
            calendarBaseAddress,
            secondaryBaseAddress,
            cancellationToken);
}
