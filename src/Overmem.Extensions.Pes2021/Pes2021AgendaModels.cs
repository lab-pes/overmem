namespace Overmem.Extensions.Pes2021;

public sealed record Pes2021CalendarSearchPriority(
    string Tier,
    string Label,
    string Note);

public sealed record Pes2021CalendarReference(
    string Scope,
    string Description,
    string? Address,
    string? VariableType,
    IReadOnlyList<int> Offsets);

public sealed record Pes2021AgendaGuide(
    string CheatTablePath,
    bool CheatTableFound,
    string? InspectorScriptPath,
    bool InspectorScriptFound,
    string? CompetitionMapPath,
    bool CompetitionMapFound,
    int RecordStride,
    int SecondaryDayStride,
    int SecondaryCountOffset,
    int SecondaryItemsStart,
    int SecondaryItemsEnd,
    int SecondaryHeaderMaxEvents,
    int SecondaryHeaderEventSize,
    int SecondaryScoreThreshold,
    IReadOnlyList<int> SeasonAnchorYears,
    IReadOnlyList<int> SecondarySampleDays,
    IReadOnlyList<Pes2021CalendarSearchPriority> SearchPriorities,
    IReadOnlyList<Pes2021CalendarReference> References,
    IReadOnlyList<string> RecommendedCommands);

public sealed record Pes2021CalendarBaseCandidate(
    ulong MatchAddress,
    ulong CandidateBaseAddress,
    int ValidationScore,
    int StrongScore,
    int ClusterSpan);

public sealed record Pes2021CalendarBaseResult(
    Guid AttachmentId,
    ulong SelectedMatchAddress,
    ulong SelectedBaseAddress,
    ulong NormalizedBaseAddress,
    int AnchorIndex,
    int ValidationScore,
    int StrongScore,
    int ClusterSpan,
    string SearchPattern,
    string SearchMode,
    int AnchorYear,
    int CompetitionCode,
    int RoundValue,
    IReadOnlyList<Pes2021CalendarBaseCandidate> Candidates);

public sealed record Pes2021CalendarRecordSnapshot(
    int Index,
    ulong Address,
    int CompetitionCode,
    string CompetitionName,
    int Round,
    int Year,
    int Month,
    int Day,
    int HomeId,
    int HomeLiga,
    int AwayId,
    int AwayLiga,
    int HomeScore,
    int AwayScore,
    bool IsPlaceholder,
    string EventId);

public sealed record Pes2021CompetitionDateSummary(
    int CompetitionCode,
    string CompetitionName,
    int MatchCount,
    string Rounds);

public sealed record Pes2021CalendarDateReport(
    ulong BaseAddress,
    int Year,
    int Month,
    int Day,
    int TotalCompetitions,
    int TotalMatches,
    string SourceRole,
    string Visibility,
    string StopState,
    IReadOnlyList<Pes2021CompetitionDateSummary> Competitions,
    IReadOnlyList<Pes2021CalendarRecordSnapshot> Matches);

public sealed record Pes2021CompetitionDateComparison(
    int CompetitionCode,
    string CompetitionName,
    int FirstMatchCount,
    string FirstRounds,
    int SecondMatchCount,
    string SecondRounds);

public sealed record Pes2021CalendarDateComparisonReport(
    ulong BaseAddress,
    Pes2021CalendarDateReport FirstDate,
    Pes2021CalendarDateReport SecondDate,
    IReadOnlyList<Pes2021CompetitionDateComparison> Competitions);

public sealed record Pes2021CalendarSummaryEntry(
    string Date,
    int MatchCount,
    int CompetitionCount);

public sealed record Pes2021CalendarSummary(
    ulong BaseAddress,
    int TotalDates,
    int TotalMatches,
    IReadOnlyList<Pes2021CalendarSummaryEntry> Dates);

public sealed record Pes2021SecondaryDaySummary(
    int DayIndex,
    uint? Count,
    int? ItemCount,
    bool TerminatorFound,
    string HeaderState);

public sealed record Pes2021SecondaryCalendarCandidateReport(
    ulong BaseAddress,
    int Score,
    int PlausibleCounts,
    int MatchedCounts,
    int TerminatorDays,
    int HeaderDays,
    bool SpecialDayOk,
    IReadOnlyList<Pes2021SecondaryDaySummary> DaySummaries,
    IReadOnlyList<string> Issues);

public sealed record Pes2021SecondaryCalendarBaseCandidate(
    ulong HitAddress,
    ulong CandidateBaseAddress,
    int SlotIndex,
    int Score,
    int PlausibleCounts,
    int MatchedCounts,
    int TerminatorDays,
    int HeaderDays,
    bool SpecialDayOk);

public sealed record Pes2021SecondaryCalendarBaseResult(
    Guid AttachmentId,
    int Year,
    int Month,
    int Day,
    int DayIndex,
    ulong HitAddress,
    ulong BaseAddress,
    int SlotIndex,
    int Score,
    IReadOnlyList<Pes2021SecondaryCalendarBaseCandidate> Candidates);

public sealed record Pes2021SecondaryCalendarHeaderEvent(
    int SlotIndex,
    int Year,
    int Month,
    int Day,
    ushort Type,
    short Value);

public sealed record Pes2021SecondaryCalendarDayReport(
    ulong BaseAddress,
    int DayIndex,
    int Year,
    int Month,
    int Day,
    uint Count,
    string SourceRole,
    string Visibility,
    string StopState,
    IReadOnlyList<Pes2021SecondaryCalendarHeaderEvent> HeaderEvents,
    IReadOnlyList<ushort> Items);

public sealed record Pes2021RuntimeCalendarClusterReport(
    ulong ClusterStartAddress,
    ulong ClusterEndAddress,
    ulong RegionBaseAddress,
    ulong RegionSize,
    string RegionType,
    string RegionProtection,
    bool RegionIsWritable,
    bool RegionIsExecutable,
    int HitCount,
    int TypicalStride,
    IReadOnlyList<ulong> SampleAddresses,
    IReadOnlyList<int> PreviewInt32);

public sealed record Pes2021RuntimeCalendarScanReport(
    Guid AttachmentId,
    int DayIndex,
    int Year,
    int Month,
    int Day,
    string SearchPattern,
    int TotalHits,
    int ClusterCount,
    IReadOnlyList<Pes2021RuntimeCalendarClusterReport> Clusters);

public sealed record Pes2021RuntimeCalendarPreviewRecord(
    int Value,
    bool Resolved,
    Pes2021CalendarRecordSnapshot? Record);

public sealed record Pes2021RuntimeCalendarFamilyClusterReport(
    ulong ClusterStartAddress,
    ulong ClusterEndAddress,
    ulong RegionBaseAddress,
    ulong RegionSize,
    string RegionType,
    string RegionProtection,
    bool RegionIsWritable,
    bool RegionIsExecutable,
    int HitCount,
    int TypicalStride,
    IReadOnlyList<ulong> SampleAddresses,
    IReadOnlyList<int> PreviewInt32,
    IReadOnlyList<Pes2021RuntimeCalendarPreviewRecord> PreviewRecords);

public sealed record Pes2021RuntimeCalendarFamilyReport(
    Guid AttachmentId,
    int DayIndex,
    int Year,
    int Month,
    int Day,
    ulong ScanStartAddress,
    ulong ScanStopAddress,
    int TotalHits,
    int ClusterCount,
    IReadOnlyList<int> PreferredStrides,
    IReadOnlyList<Pes2021RuntimeCalendarFamilyClusterReport> Clusters);

public sealed record Pes2021RuntimeCalendarValueDiff(
    int Value,
    bool Resolved,
    Pes2021CalendarRecordSnapshot? Record);

public sealed record Pes2021RuntimeCalendarDayMarker(
    int DayIndex,
    int Year,
    int Month,
    int Day);

public sealed record Pes2021RuntimeCalendarFamilyClusterDiffReport(
    int ClusterOrdinal,
    int TypicalStride,
    ulong? PreviousClusterStartAddress,
    ulong CurrentClusterStartAddress,
    ulong? NextClusterStartAddress,
    IReadOnlyList<int> PreviousPreviewInt32,
    IReadOnlyList<int> CurrentPreviewInt32,
    IReadOnlyList<int> NextPreviewInt32,
    IReadOnlyList<Pes2021RuntimeCalendarValueDiff> AddedVsPrevious,
    IReadOnlyList<Pes2021RuntimeCalendarValueDiff> RemovedVsPrevious,
    IReadOnlyList<Pes2021RuntimeCalendarValueDiff> AddedVsNext,
    IReadOnlyList<Pes2021RuntimeCalendarValueDiff> RemovedVsNext,
    string PreviousMatchStrategy,
    int PreviousSharedPreviewValueCount,
    string NextMatchStrategy,
    int NextSharedPreviewValueCount);

public sealed record Pes2021RuntimeCalendarFamilyComparisonReport(
    Guid AttachmentId,
    Pes2021RuntimeCalendarDayMarker PreviousDay,
    Pes2021RuntimeCalendarDayMarker CurrentDay,
    Pes2021RuntimeCalendarDayMarker NextDay,
    ulong ScanStartAddress,
    ulong ScanStopAddress,
    IReadOnlyList<int> PreferredStrides,
    IReadOnlyList<Pes2021RuntimeCalendarFamilyClusterDiffReport> Clusters);

public sealed record Pes2021RuntimeCalendarHitWindowEntry(
    int RelativeOffset,
    int Value);

public sealed record Pes2021RuntimeCalendarHitWindowReport(
    ulong HitAddress,
    IReadOnlyList<Pes2021RuntimeCalendarHitWindowEntry> Values);

public sealed record Pes2021RuntimeCalendarClusterDetailReport(
    Guid AttachmentId,
    int DayIndex,
    int Year,
    int Month,
    int Day,
    int ClusterOrdinal,
    int TypicalStride,
    ulong ClusterStartAddress,
    ulong ClusterEndAddress,
    ulong RegionBaseAddress,
    ulong RegionSize,
    IReadOnlyList<ulong> SampleAddresses,
    IReadOnlyList<int> PreviewInt32,
    IReadOnlyList<Pes2021RuntimeCalendarPreviewRecord> PreviewRecords,
    IReadOnlyList<Pes2021RuntimeCalendarHitWindowReport> HitWindows);

public sealed record Pes2021RuntimeCalendarValueFrequency(
    int Value,
    int Count);

public sealed record Pes2021RuntimeCalendarSequenceRun(
    int StartValue,
    int EndValue,
    int Length,
    IReadOnlyList<int> Values);

public sealed record Pes2021RuntimeCalendarHitSignature(
    ulong HitAddress,
    IReadOnlyList<int> AnchorValues,
    IReadOnlyList<int> TailValues,
    IReadOnlyList<Pes2021RuntimeCalendarSequenceRun> TailRuns);

public sealed record Pes2021RuntimeCalendarClusterSignatureReport(
    Guid AttachmentId,
    int DayIndex,
    int Year,
    int Month,
    int Day,
    int ClusterOrdinal,
    int TypicalStride,
    ulong ClusterStartAddress,
    ulong ClusterEndAddress,
    IReadOnlyList<int> CommonAnchorPrefix,
    IReadOnlyList<int> UnresolvedPreviewValues,
    IReadOnlyList<Pes2021RuntimeCalendarValueFrequency> FrequentTailValues,
    IReadOnlyList<Pes2021RuntimeCalendarHitSignature> HitSignatures);

public sealed record Pes2021RuntimeCalendarVariantReport(
    Guid AttachmentId,
    int DayIndex,
    int Year,
    int Month,
    int Day,
    string VariantKey,
    string Confidence,
    string SemanticEventKey,
    string SemanticEventConfidence,
    uint SecondaryCount,
    int SecondaryItemCount,
    IReadOnlyList<ushort> SecondaryHeaderEventTypes,
    int TotalHits,
    int ClusterCount,
    IReadOnlyList<int> DominantStrides,
    bool HasSpecial472Family,
    Pes2021RuntimeCalendarTemporalComparisonSummary TemporalComparison,
    string SourceRole,
    string Visibility,
    string StopState,
    IReadOnlyList<string> SemanticReasons,
    IReadOnlyList<string> Reasons);

public sealed record Pes2021RuntimeCalendarTemporalComparisonSummary(
    int ComparedClusterCount,
    int PreviousMatchedClusterCount,
    int NextMatchedClusterCount,
    int StableClusterCount,
    int IsolatedClusterCount,
    int PreviousPreviewOverlapMatchCount,
    int NextPreviewOverlapMatchCount,
    int PreviousFallbackMatchCount,
    int NextFallbackMatchCount);

public sealed record Pes2021AnnualCalendarEventEntry(
    string Date,
    int DayIndex,
    int MainMatchCount,
    int PreviousDayMainMatchCount,
    int NextDayMainMatchCount,
    uint SecondaryCount,
    int SecondaryItemCount,
    IReadOnlyList<ushort> SecondaryHeaderEventTypes,
    string SemanticEventKey,
    string SemanticEventConfidence,
    IReadOnlyList<string> SemanticReasons,
    string InventoryPatternKey,
    string InventoryPatternConfidence,
    string DayRole,
    string SourceRole,
    string Visibility,
    string StopState,
    IReadOnlyList<string> InventoryPatternReasons);

public sealed record Pes2021AnnualCalendarEventInventoryReport(
    Guid AttachmentId,
    int Year,
    ulong MainCalendarBaseAddress,
    ulong SecondaryCalendarBaseAddress,
    int TotalSpecialDays,
    IReadOnlyList<Pes2021AnnualCalendarEventEntry> Days);
