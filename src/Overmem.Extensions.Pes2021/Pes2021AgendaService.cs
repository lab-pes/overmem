using Overmem.Abstractions.Memory;
using Overmem.Abstractions.Processes;
using Overmem.Application;
using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Globalization;
using System.Text;

namespace Overmem.Extensions.Pes2021;

public sealed class Pes2021AgendaService(ProcessMemoryApplicationService memoryService)
{
    private const int MaxPlausibleCompetitionCode = 0x3FFF;
    private readonly Lazy<IReadOnlyDictionary<int, string>> _competitionMap = new(
        () => Pes2021AgendaProfile.LoadCompetitionMap());
    private readonly ConcurrentDictionary<AttachmentId, Pes2021CalendarBaseResult> _baseCache = new();
    private readonly ConcurrentDictionary<AttachmentId, Pes2021SecondaryCalendarBaseResult> _secondaryBaseCache = new();

    public Task<Pes2021AgendaGuide> GetGuideAsync(string? cheatTablePath = null, CancellationToken cancellationToken = default)
        => Task.FromResult(Pes2021AgendaProfile.LoadGuide(cheatTablePath));

    public Task<IReadOnlyList<Pes2021CalendarSearchPriority>> GetSearchPrioritiesAsync(string? cheatTablePath = null, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<Pes2021CalendarSearchPriority>>(Pes2021AgendaProfile.LoadGuide(cheatTablePath).SearchPriorities);

    public async Task<Pes2021CalendarBaseResult> FindBaseAsync(
        AttachmentId attachmentId,
        int? year = null,
        int? month = null,
        int? day = null,
        int? roundValue = null,
        int? competitionCode = null,
        string? moduleName = null,
        int maxResults = 100,
        CancellationToken cancellationToken = default)
    {
        maxResults = Math.Max(1, maxResults);
        var years = year.HasValue ? [year.Value] : Pes2021AgendaProfile.SeasonAnchorYears;
        var targetMonth = month ?? 2;
        var targetDay = day ?? 1;
        var targetRound = roundValue ?? 1;
        var targetCompetitionCode = competitionCode ?? 29;
        Pes2021CalendarBaseResult? best = null;

        foreach (var anchorYear in years)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var result = await FindBaseForDateAsync(
                attachmentId,
                anchorYear,
                targetMonth,
                targetDay,
                targetRound,
                targetCompetitionCode,
                moduleName,
                maxResults,
                cancellationToken);

            if (result is not null)
            {
                best = result;
                break;
            }
        }

        if (best is null)
        {
            throw new InvalidOperationException("No validated Master League calendar base was found for the PES 2021 agenda.");
        }

        _baseCache[attachmentId] = best;
        return best;
    }

    public async Task<Pes2021CalendarDateReport> DumpDateAsync(
        AttachmentId attachmentId,
        int year,
        int month,
        int day,
        ulong? baseAddress = null,
        int maxRecs = 13014,
        CancellationToken cancellationToken = default)
    {
        var resolvedBase = await ResolveBaseAddressAsync(attachmentId, baseAddress, cancellationToken);
        var targetKey = FormatDateKey(year, month, day);
        var matches = new List<Pes2021CalendarRecordSnapshot>();
        var competitionCounts = new Dictionary<int, CompetitionBuilder>();

        for (var index = 0; index < maxRecs; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var record = await TryReadRecordAsync(attachmentId, resolvedBase + (ulong)(index * Pes2021AgendaProfile.RecordStride), index, cancellationToken);
            if (record is null || !IsPlausibleRecord(record))
            {
                continue;
            }

            if (FormatDateKey(record.Year, record.Month, record.Day) != targetKey)
            {
                continue;
            }

            matches.Add(record);
            var builder = GetOrCreateBuilder(competitionCounts, record.CompetitionCode, record.CompetitionName);
            builder.MatchCount++;
            builder.Rounds.Add(record.Round);
        }

        var competitions = competitionCounts.Values
            .Select(builder => new Pes2021CompetitionDateSummary(
                builder.CompetitionCode,
                builder.CompetitionName,
                builder.MatchCount,
                FormatRounds(builder.Rounds)))
            .OrderBy(item => item.CompetitionCode)
            .ToArray();

        var orderedMatches = matches
            .OrderBy(match => match.CompetitionCode)
            .ThenBy(match => match.Round)
            .ThenBy(match => match.Index)
            .ToArray();

        return new Pes2021CalendarDateReport(
            resolvedBase,
            year,
            month,
            day,
            competitions.Length,
            orderedMatches.Length,
            "main-backed",
            "visible",
            "unknown",
            competitions,
            orderedMatches);
    }

    public async Task<Pes2021CalendarDateComparisonReport> CompareDatesAsync(
        AttachmentId attachmentId,
        int firstYear,
        int firstMonth,
        int firstDay,
        int secondYear,
        int secondMonth,
        int secondDay,
        ulong? baseAddress = null,
        int maxRecs = 13014,
        CancellationToken cancellationToken = default)
    {
        var resolvedBase = await ResolveBaseAddressAsync(attachmentId, baseAddress, cancellationToken);
        var firstDate = await DumpDateAsync(attachmentId, firstYear, firstMonth, firstDay, resolvedBase, maxRecs, cancellationToken);
        var secondDate = await DumpDateAsync(attachmentId, secondYear, secondMonth, secondDay, resolvedBase, maxRecs, cancellationToken);

        var comparisons = firstDate.Competitions
            .Concat(secondDate.Competitions)
            .GroupBy(item => item.CompetitionCode)
            .Select(group =>
            {
                var first = firstDate.Competitions.FirstOrDefault(item => item.CompetitionCode == group.Key);
                var second = secondDate.Competitions.FirstOrDefault(item => item.CompetitionCode == group.Key);
                var competitionName = ResolveComparisonCompetitionName(first, second);

                return new Pes2021CompetitionDateComparison(
                    group.Key,
                    competitionName,
                    first?.MatchCount ?? 0,
                    first?.Rounds ?? string.Empty,
                    second?.MatchCount ?? 0,
                    second?.Rounds ?? string.Empty);
            })
            .OrderBy(item => item.CompetitionCode)
            .ToArray();

        return new Pes2021CalendarDateComparisonReport(
            resolvedBase,
            firstDate,
            secondDate,
            comparisons);
    }

    public async Task<Pes2021CalendarSummary> CalendarSummaryAsync(
        AttachmentId attachmentId,
        ulong? baseAddress = null,
        int maxRecs = 13014,
        CancellationToken cancellationToken = default)
    {
        var resolvedBase = await ResolveBaseAddressAsync(attachmentId, baseAddress, cancellationToken);
        var dates = new Dictionary<string, SummaryBuilder>(StringComparer.Ordinal);
        var totalMatches = 0;

        for (var index = 0; index < maxRecs; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var record = await TryReadRecordAsync(attachmentId, resolvedBase + (ulong)(index * Pes2021AgendaProfile.RecordStride), index, cancellationToken);
            if (record is null || !IsPlausibleRecord(record))
            {
                continue;
            }

            totalMatches++;
            var dateKey = FormatDateKey(record.Year, record.Month, record.Day);
            var builder = GetOrCreateSummaryBuilder(dates, dateKey, record.Year, record.Month, record.Day);
            builder.MatchCount++;
            builder.Competitions.Add(record.CompetitionCode);
        }

        var entries = dates
            .OrderBy(pair => pair.Key, StringComparer.Ordinal)
            .Select(pair => new Pes2021CalendarSummaryEntry(
                pair.Value.Date,
                pair.Value.MatchCount,
                pair.Value.Competitions.Count))
            .ToArray();

        return new Pes2021CalendarSummary(
            resolvedBase,
            entries.Length,
            totalMatches,
            entries);
    }

    public async Task<Pes2021SecondaryCalendarCandidateReport> InspectSecondaryCalendarCandidateAsync(
        AttachmentId attachmentId,
        ulong baseValue,
        IReadOnlyList<int>? sampleDays = null,
        CancellationToken cancellationToken = default)
    {
        var score = await ScoreSecondaryCalendarBaseAsync(attachmentId, baseValue, sampleDays, cancellationToken);
        return score.Report;
    }

    public async Task<IReadOnlyList<Pes2021SecondaryCalendarCandidateReport>> ScanSecondaryCalendarCandidatesAsync(
        AttachmentId attachmentId,
        ulong startValue,
        ulong stopValue,
        ulong step = 0x10,
        int maxResults = 10,
        IReadOnlyList<int>? sampleDays = null,
        CancellationToken cancellationToken = default)
    {
        if (startValue > stopValue)
        {
            throw new ArgumentException("The scan start address must be less than or equal to the stop address.", nameof(startValue));
        }

        if (step == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(step), "The scan step must be greater than zero.");
        }

        maxResults = Math.Max(1, maxResults);
        var results = new List<Pes2021SecondaryCalendarCandidateReport>();
        var scanned = 0;

        for (var address = startValue; address <= stopValue; address += step)
        {
            cancellationToken.ThrowIfCancellationRequested();
            scanned++;

            var score = await ScoreSecondaryCalendarBaseAsync(attachmentId, address, sampleDays, cancellationToken);
            if (score.Score >= Pes2021AgendaProfile.SecondaryScoreThreshold)
            {
                results.Add(score.Report);
                results.Sort(CompareSecondaryReports);
                if (results.Count > maxResults)
                {
                    results.RemoveAt(results.Count - 1);
                }
            }

            if (address + step < address)
            {
                break;
            }

            if (scanned % 256 == 0)
            {
                await Task.Yield();
            }
        }

        return results;
    }

    public async Task<Pes2021SecondaryCalendarBaseResult> FindSecondaryBaseByDateAsync(
        AttachmentId attachmentId,
        int year,
        int month,
        int day,
        string? moduleName = null,
        int maxResults = 256,
        CancellationToken cancellationToken = default)
    {
        maxResults = Math.Max(1, maxResults);
        var dayIndex = ComputeSecondaryDayIndex(year, month, day);
        var pattern = BuildSecondaryDatePattern(year, month, day);
        var scanResult = await memoryService.ScanPatternAsync(
            new PatternScanRequest(attachmentId, pattern, moduleName, maxResults),
            cancellationToken);

        if (scanResult.Addresses.Count == 0)
        {
            throw new InvalidOperationException($"No secondary-calendar date hit was found for {year:D4}-{month:D2}-{day:D2}.");
        }

        var candidates = new List<Pes2021SecondaryCalendarBaseCandidate>();
        foreach (var hitAddress in scanResult.Addresses)
        {
            cancellationToken.ThrowIfCancellationRequested();

            for (var slotIndex = 0; slotIndex < Pes2021AgendaProfile.SecondaryHeaderMaxEvents; slotIndex++)
            {
                var adjustment = checked((ulong)slotIndex * (ulong)Pes2021AgendaProfile.SecondaryHeaderEventSize);
                if (hitAddress < adjustment + ((ulong)dayIndex * (ulong)Pes2021AgendaProfile.SecondaryDayStride))
                {
                    continue;
                }

                var candidateBaseAddress = hitAddress
                    - ((ulong)dayIndex * (ulong)Pes2021AgendaProfile.SecondaryDayStride)
                    - adjustment;

                var report = await InspectSecondaryCalendarCandidateAsync(
                    attachmentId,
                    candidateBaseAddress,
                    cancellationToken: cancellationToken);

                candidates.Add(new Pes2021SecondaryCalendarBaseCandidate(
                    hitAddress,
                    candidateBaseAddress,
                    slotIndex,
                    report.Score,
                    report.PlausibleCounts,
                    report.MatchedCounts,
                    report.TerminatorDays,
                    report.HeaderDays,
                    report.SpecialDayOk));
            }
        }

        var best = candidates
            .OrderByDescending(candidate => candidate.Score)
            .ThenByDescending(candidate => candidate.MatchedCounts)
            .ThenByDescending(candidate => candidate.HeaderDays)
            .ThenBy(candidate => candidate.CandidateBaseAddress)
            .FirstOrDefault();

        if (best is null)
        {
            throw new InvalidOperationException("No secondary-calendar base candidate could be evaluated.");
        }

        var result = new Pes2021SecondaryCalendarBaseResult(
            attachmentId.Value,
            year,
            month,
            day,
            dayIndex,
            best.HitAddress,
            best.CandidateBaseAddress,
            best.SlotIndex,
            best.Score,
            candidates
                .OrderByDescending(candidate => candidate.Score)
                .ThenByDescending(candidate => candidate.MatchedCounts)
                .ThenByDescending(candidate => candidate.HeaderDays)
                .ThenBy(candidate => candidate.CandidateBaseAddress)
                .Take(32)
                .ToArray());

        _secondaryBaseCache[attachmentId] = result;
        return result;
    }

    public async Task<Pes2021SecondaryCalendarDayReport> DumpSecondaryDayAsync(
        AttachmentId attachmentId,
        int year,
        int month,
        int day,
        ulong? baseAddress = null,
        CancellationToken cancellationToken = default)
    {
        var resolvedBase = await ResolveSecondaryBaseAddressAsync(attachmentId, year, month, day, baseAddress, cancellationToken);
        var dayIndex = ComputeSecondaryDayIndex(year, month, day);
        var dayBase = resolvedBase + ((ulong)dayIndex * (ulong)Pes2021AgendaProfile.SecondaryDayStride);
        var block = await ReadBytesAsync(attachmentId, dayBase, Pes2021AgendaProfile.SecondaryDayStride, cancellationToken);
        if (block is null || block.Length < Pes2021AgendaProfile.SecondaryDayStride)
        {
            throw new InvalidOperationException($"The secondary-calendar day block for {year:D4}-{month:D2}-{day:D2} could not be read.");
        }

        var count = BinaryPrimitives.ReadUInt32LittleEndian(block.AsSpan(Pes2021AgendaProfile.SecondaryCountOffset, sizeof(uint)));
        var headerEvents = ReadSecondaryHeaderEvents(block);
        var (items, _) = ReadSecondaryCalendarItemsFromBlock(block);

        return new Pes2021SecondaryCalendarDayReport(
            resolvedBase,
            dayIndex,
            year,
            month,
            day,
            count,
            "secondary-backed",
            count > 0 || (items?.Count ?? 0) > 0 || headerEvents.Count > 0 ? "visible" : "hidden",
            "unknown",
            headerEvents,
            items ?? []);
    }

    public async Task<Pes2021RuntimeCalendarScanReport> ScanRuntimeDayIndexClustersAsync(
        AttachmentId attachmentId,
        int year,
        int month,
        int day,
        int maxResults = 5000,
        int clusterGap = 0x1000,
        int previewBytes = 0x100,
        CancellationToken cancellationToken = default)
    {
        maxResults = Math.Max(1, maxResults);
        clusterGap = Math.Max(1, clusterGap);
        previewBytes = Math.Clamp(previewBytes, 16, 0x800);

        var dayIndex = ComputeSecondaryDayIndex(year, month, day);
        var regions = await memoryService.ListRegionsAsync(attachmentId, cancellationToken);
        var scanRegions = regions
            .Where(static region => region.IsReadable && region.RegionSize >= sizeof(int))
            .OrderBy(static region => region.BaseAddress)
            .ToArray();

        var hits = new List<ulong>();
        foreach (var region in scanRegions)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var remainingHits = maxResults - hits.Count;
            if (remainingHits <= 0)
            {
                break;
            }

            var regionHits = await ScanInt32InRegionAsync(attachmentId, region, dayIndex, null, null, cancellationToken);
            if (regionHits.Count == 0)
            {
                continue;
            }

            hits.AddRange(regionHits.Take(remainingHits));
        }

        hits.Sort();
        var clusters = BuildRuntimeClusters(hits, regions, clusterGap);
        var reports = new List<Pes2021RuntimeCalendarClusterReport>(clusters.Count);

        foreach (var cluster in clusters)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var previewBytesBuffer = await ReadClusterPreviewBytesAsync(
                attachmentId,
                cluster.StartAddress,
                cluster.Region.BaseAddress,
                cluster.Region.RegionSize,
                previewBytes,
                cancellationToken);
            reports.Add(new Pes2021RuntimeCalendarClusterReport(
                cluster.StartAddress,
                cluster.EndAddress,
                cluster.Region.BaseAddress,
                cluster.Region.RegionSize,
                cluster.Region.Type,
                cluster.Region.Protection,
                cluster.Region.IsWritable,
                cluster.Region.IsExecutable,
                cluster.Addresses.Count,
                cluster.TypicalStride,
                cluster.Addresses.Take(16).ToArray(),
                DecodeInt32Preview(previewBytesBuffer)));
        }

        var orderedReports = reports
            .OrderByDescending(report => report.HitCount)
            .ThenByDescending(report => report.RegionIsWritable && !report.RegionIsExecutable && string.Equals(report.RegionType, "Private", StringComparison.OrdinalIgnoreCase))
            .ThenBy(report => report.ClusterStartAddress)
            .ToArray();

        return new Pes2021RuntimeCalendarScanReport(
            attachmentId.Value,
            dayIndex,
            year,
            month,
            day,
            BuildInt32Pattern(dayIndex),
            hits.Count,
            orderedReports.Length,
            orderedReports);
    }

    public async Task<Pes2021RuntimeCalendarFamilyReport> DumpRuntimeDayPayloadFamilyAsync(
        AttachmentId attachmentId,
        int year,
        int month,
        int day,
        ulong? startAddress = null,
        ulong? stopAddress = null,
        ulong? calendarBaseAddress = null,
        int[]? preferredStrides = null,
        int minHitCount = 3,
        int clusterGap = 0x1000,
        int previewBytes = 0x100,
        CancellationToken cancellationToken = default)
    {
        minHitCount = Math.Max(1, minHitCount);
        clusterGap = Math.Max(1, clusterGap);
        previewBytes = Math.Clamp(previewBytes, 16, 0x800);

        var dayIndex = ComputeSecondaryDayIndex(year, month, day);
        var strideSet = (preferredStrides is { Length: > 0 } ? preferredStrides : [472, 528])
            .Where(static value => value > 0)
            .Distinct()
            .OrderBy(static value => value)
            .ToArray();

        var regions = await memoryService.ListRegionsAsync(attachmentId, cancellationToken);
        var scanRegions = FilterRuntimeScanRegions(regions, startAddress, stopAddress).ToArray();
        if (scanRegions.Length == 0)
        {
            throw new InvalidOperationException("No readable private runtime regions matched the requested scan window.");
        }

        var hits = new List<ulong>();
        var effectiveStarts = new List<ulong>(scanRegions.Length);
        var effectiveStops = new List<ulong>(scanRegions.Length);
        foreach (var region in scanRegions)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var regionStart = region.BaseAddress;
            var regionStop = checked(region.BaseAddress + region.RegionSize);
            var effectiveStart = startAddress.HasValue ? Math.Max(regionStart, startAddress.Value) : regionStart;
            var effectiveStop = stopAddress.HasValue ? Math.Min(regionStop, stopAddress.Value) : regionStop;
            if (effectiveStop <= effectiveStart)
            {
                continue;
            }

            effectiveStarts.Add(effectiveStart);
            effectiveStops.Add(effectiveStop);

            var regionHits = await ScanInt32InRegionAsync(attachmentId, region, dayIndex, effectiveStart, effectiveStop, cancellationToken);
            hits.AddRange(regionHits);
        }

        hits.Sort();
        var clusters = BuildRuntimeClusters(hits, scanRegions, clusterGap);
        var filteredClusters = clusters
            .Where(cluster => cluster.Addresses.Count >= minHitCount)
            .ToArray();

        if (strideSet.Length > 0)
        {
            var preferredClusters = filteredClusters
                .Where(cluster => strideSet.Contains(cluster.TypicalStride))
                .ToArray();
            if (preferredClusters.Length > 0)
            {
                filteredClusters = preferredClusters;
            }
        }

        var resolvedBase = calendarBaseAddress;
        if (resolvedBase is null)
        {
            try
            {
                resolvedBase = await ResolveBaseAddressAsync(attachmentId, null, cancellationToken);
            }
            catch
            {
                resolvedBase = null;
            }
        }

        var reports = new List<Pes2021RuntimeCalendarFamilyClusterReport>(filteredClusters.Length);
        foreach (var cluster in filteredClusters)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var previewBytesBuffer = await ReadClusterPreviewBytesAsync(
                attachmentId,
                cluster.StartAddress,
                cluster.Region.BaseAddress,
                cluster.Region.RegionSize,
                previewBytes,
                cancellationToken);
            var previewValues = DecodeInt32Preview(previewBytesBuffer);
            var previewRecords = await DecodeRuntimePreviewRecordsAsync(attachmentId, resolvedBase, previewValues, cancellationToken);

            reports.Add(new Pes2021RuntimeCalendarFamilyClusterReport(
                cluster.StartAddress,
                cluster.EndAddress,
                cluster.Region.BaseAddress,
                cluster.Region.RegionSize,
                cluster.Region.Type,
                cluster.Region.Protection,
                cluster.Region.IsWritable,
                cluster.Region.IsExecutable,
                cluster.Addresses.Count,
                cluster.TypicalStride,
                cluster.Addresses.Take(16).ToArray(),
                previewValues,
                previewRecords));
        }

        var orderedReports = reports
            .OrderByDescending(report => strideSet.Contains(report.TypicalStride))
            .ThenByDescending(report => report.HitCount)
            .ThenBy(report => report.ClusterStartAddress)
            .ToArray();

        return new Pes2021RuntimeCalendarFamilyReport(
            attachmentId.Value,
            dayIndex,
            year,
            month,
            day,
            effectiveStarts.Count == 0 ? 0UL : effectiveStarts.Min(),
            effectiveStops.Count == 0 ? 0UL : effectiveStops.Max(),
            hits.Count,
            orderedReports.Length,
            strideSet,
            orderedReports);
    }

    public async Task<Pes2021RuntimeCalendarFamilyComparisonReport> CompareRuntimeDayPayloadFamilyAsync(
        AttachmentId attachmentId,
        int year,
        int month,
        int day,
        ulong? startAddress = null,
        ulong? stopAddress = null,
        ulong? calendarBaseAddress = null,
        int[]? preferredStrides = null,
        int minHitCount = 3,
        int clusterGap = 0x1000,
        int previewBytes = 0x100,
        CancellationToken cancellationToken = default)
    {
        var currentDate = new DateOnly(year, month, day);
        var previousDate = currentDate.AddDays(-1);
        var nextDate = currentDate.AddDays(1);

        var previous = await DumpRuntimeDayPayloadFamilyAsync(
            attachmentId,
            previousDate.Year,
            previousDate.Month,
            previousDate.Day,
            startAddress,
            stopAddress,
            calendarBaseAddress,
            preferredStrides,
            minHitCount,
            clusterGap,
            previewBytes,
            cancellationToken);

        var current = await DumpRuntimeDayPayloadFamilyAsync(
            attachmentId,
            currentDate.Year,
            currentDate.Month,
            currentDate.Day,
            startAddress,
            stopAddress,
            calendarBaseAddress,
            preferredStrides,
            minHitCount,
            clusterGap,
            previewBytes,
            cancellationToken);

        var next = await DumpRuntimeDayPayloadFamilyAsync(
            attachmentId,
            nextDate.Year,
            nextDate.Month,
            nextDate.Day,
            startAddress,
            stopAddress,
            calendarBaseAddress,
            preferredStrides,
            minHitCount,
            clusterGap,
            previewBytes,
            cancellationToken);

        return BuildRuntimeDayPayloadFamilyComparisonReport(attachmentId.Value, previous, current, next);
    }

    private static Pes2021RuntimeCalendarFamilyComparisonReport BuildRuntimeDayPayloadFamilyComparisonReport(
        Guid attachmentId,
        Pes2021RuntimeCalendarFamilyReport previous,
        Pes2021RuntimeCalendarFamilyReport current,
        Pes2021RuntimeCalendarFamilyReport next)
    {
        var previousMatches = MatchClustersByPreviewSimilarity(current.Clusters, previous.Clusters);
        var nextMatches = MatchClustersByPreviewSimilarity(current.Clusters, next.Clusters);
        var diffs = new List<Pes2021RuntimeCalendarFamilyClusterDiffReport>(current.Clusters.Count);

        for (var clusterOrdinal = 0; clusterOrdinal < current.Clusters.Count; clusterOrdinal++)
        {
            var currentCluster = current.Clusters[clusterOrdinal];
            previousMatches.TryGetValue(clusterOrdinal, out var previousMatch);
            nextMatches.TryGetValue(clusterOrdinal, out var nextMatch);
            var previousCluster = previousMatch?.Cluster;
            var nextCluster = nextMatch?.Cluster;

            diffs.Add(new Pes2021RuntimeCalendarFamilyClusterDiffReport(
                clusterOrdinal,
                currentCluster.TypicalStride,
                previousCluster?.ClusterStartAddress,
                currentCluster.ClusterStartAddress,
                nextCluster?.ClusterStartAddress,
                previousCluster?.PreviewInt32 ?? [],
                currentCluster.PreviewInt32,
                nextCluster?.PreviewInt32 ?? [],
                BuildValueDiff(currentCluster.PreviewRecords, previousCluster?.PreviewInt32),
                BuildValueDiff(previousCluster?.PreviewRecords ?? [], currentCluster.PreviewInt32),
                BuildValueDiff(currentCluster.PreviewRecords, nextCluster?.PreviewInt32),
                BuildValueDiff(nextCluster?.PreviewRecords ?? [], currentCluster.PreviewInt32),
                previousMatch?.Strategy ?? "unmatched",
                previousMatch?.SharedPreviewValueCount ?? 0,
                nextMatch?.Strategy ?? "unmatched",
                nextMatch?.SharedPreviewValueCount ?? 0));
        }

        return new Pes2021RuntimeCalendarFamilyComparisonReport(
            attachmentId,
            new Pes2021RuntimeCalendarDayMarker(previous.DayIndex, previous.Year, previous.Month, previous.Day),
            new Pes2021RuntimeCalendarDayMarker(current.DayIndex, current.Year, current.Month, current.Day),
            new Pes2021RuntimeCalendarDayMarker(next.DayIndex, next.Year, next.Month, next.Day),
            current.ScanStartAddress,
            current.ScanStopAddress,
            current.PreferredStrides,
            diffs);
    }

    public async Task<Pes2021RuntimeCalendarClusterDetailReport> DumpRuntimeDayPayloadClusterDetailAsync(
        AttachmentId attachmentId,
        int year,
        int month,
        int day,
        int clusterOrdinal,
        ulong? startAddress = null,
        ulong? stopAddress = null,
        ulong? calendarBaseAddress = null,
        int[]? preferredStrides = null,
        int minHitCount = 3,
        int clusterGap = 0x1000,
        int previewBytes = 0x100,
        int intsBeforeHit = 8,
        int intsAfterHit = 24,
        CancellationToken cancellationToken = default)
    {
        intsBeforeHit = Math.Max(0, intsBeforeHit);
        intsAfterHit = Math.Max(1, intsAfterHit);

        var family = await DumpRuntimeDayPayloadFamilyAsync(
            attachmentId,
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

        if (clusterOrdinal < 0 || clusterOrdinal >= family.Clusters.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(clusterOrdinal), $"The cluster ordinal {clusterOrdinal} is outside the available range 0..{family.Clusters.Count - 1}.");
        }

        var cluster = family.Clusters[clusterOrdinal];
        var hitWindows = new List<Pes2021RuntimeCalendarHitWindowReport>(cluster.SampleAddresses.Count);
        foreach (var hitAddress in cluster.SampleAddresses)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var bytesBefore = intsBeforeHit * sizeof(int);
            var readAddress = hitAddress >= (ulong)bytesBefore ? hitAddress - (ulong)bytesBefore : hitAddress;
            var relativeBase = hitAddress >= (ulong)bytesBefore ? -bytesBefore : 0;
            var totalBytes = (intsBeforeHit + intsAfterHit) * sizeof(int);
            var buffer = await ReadBytesAsync(attachmentId, readAddress, totalBytes, cancellationToken) ?? [];
            var values = new List<Pes2021RuntimeCalendarHitWindowEntry>(buffer.Length / sizeof(int));

            for (var index = 0; index <= buffer.Length - sizeof(int); index += sizeof(int))
            {
                values.Add(new Pes2021RuntimeCalendarHitWindowEntry(
                    relativeBase + index,
                    BinaryPrimitives.ReadInt32LittleEndian(buffer.AsSpan(index, sizeof(int)))));
            }

            hitWindows.Add(new Pes2021RuntimeCalendarHitWindowReport(hitAddress, values));
        }

        return new Pes2021RuntimeCalendarClusterDetailReport(
            attachmentId.Value,
            family.DayIndex,
            family.Year,
            family.Month,
            family.Day,
            clusterOrdinal,
            cluster.TypicalStride,
            cluster.ClusterStartAddress,
            cluster.ClusterEndAddress,
            cluster.RegionBaseAddress,
            cluster.RegionSize,
            cluster.SampleAddresses,
            cluster.PreviewInt32,
            cluster.PreviewRecords,
            hitWindows);
    }

    public async Task<Pes2021RuntimeCalendarClusterSignatureReport> AnalyzeRuntimeDayPayloadClusterAsync(
        AttachmentId attachmentId,
        int year,
        int month,
        int day,
        int clusterOrdinal,
        ulong? startAddress = null,
        ulong? stopAddress = null,
        ulong? calendarBaseAddress = null,
        int[]? preferredStrides = null,
        int minHitCount = 3,
        int clusterGap = 0x1000,
        int previewBytes = 0x100,
        int intsBeforeHit = 8,
        int intsAfterHit = 24,
        CancellationToken cancellationToken = default)
    {
        var detail = await DumpRuntimeDayPayloadClusterDetailAsync(
            attachmentId,
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

        var commonAnchorPrefix = detail.HitWindows.Count == 0
            ? []
            : detail.HitWindows
                .Select(window => (IReadOnlyList<int>)window.Values.Where(entry => entry.RelativeOffset >= 0).Select(entry => entry.Value).ToArray())
                .Aggregate((left, right) => IntersectOrderedPrefix(left, right));

        var unresolvedPreviewValues = detail.PreviewRecords
            .Where(record => !record.Resolved)
            .Select(record => record.Value)
            .Distinct()
            .OrderBy(static value => value)
            .ToArray();

        var hitSignatures = detail.HitWindows
            .Select(window =>
            {
                var ordered = window.Values
                    .Where(entry => entry.RelativeOffset >= 0)
                    .OrderBy(entry => entry.RelativeOffset)
                    .Select(entry => entry.Value)
                    .ToArray();
                var anchorValues = ordered.Take(commonAnchorPrefix.Count).ToArray();
                var tailValues = ordered.Skip(anchorValues.Length).ToArray();
                return new Pes2021RuntimeCalendarHitSignature(
                    window.HitAddress,
                    anchorValues,
                    tailValues,
                    BuildSequenceRuns(tailValues));
            })
            .ToArray();

        var frequentTailValues = hitSignatures
            .SelectMany(signature => signature.TailValues.Distinct())
            .GroupBy(static value => value)
            .Select(group => new Pes2021RuntimeCalendarValueFrequency(group.Key, group.Count()))
            .OrderByDescending(item => item.Count)
            .ThenBy(item => item.Value)
            .ToArray();

        return new Pes2021RuntimeCalendarClusterSignatureReport(
            attachmentId.Value,
            detail.DayIndex,
            detail.Year,
            detail.Month,
            detail.Day,
            detail.ClusterOrdinal,
            detail.TypicalStride,
            detail.ClusterStartAddress,
            detail.ClusterEndAddress,
            commonAnchorPrefix,
            unresolvedPreviewValues,
            frequentTailValues,
            hitSignatures);
    }

    public async Task<Pes2021RuntimeCalendarVariantReport> ClassifyRuntimeDayVariantAsync(
        AttachmentId attachmentId,
        int year,
        int month,
        int day,
        ulong? startAddress = null,
        ulong? stopAddress = null,
        ulong? secondaryBaseAddress = null,
        ulong? calendarBaseAddress = null,
        int[]? preferredStrides = null,
        int minHitCount = 3,
        int clusterGap = 0x1000,
        int previewBytes = 0x100,
        CancellationToken cancellationToken = default)
    {
        var secondary = await DumpSecondaryDayAsync(
            attachmentId,
            year,
            month,
            day,
            secondaryBaseAddress,
            cancellationToken);

        var family = await DumpRuntimeDayPayloadFamilyAsync(
            attachmentId,
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

        var currentDate = new DateOnly(year, month, day);
        var previousDate = currentDate.AddDays(-1);
        var nextDate = currentDate.AddDays(1);
        var previousFamily = await DumpRuntimeDayPayloadFamilyAsync(
            attachmentId,
            previousDate.Year,
            previousDate.Month,
            previousDate.Day,
            startAddress,
            stopAddress,
            calendarBaseAddress,
            preferredStrides,
            minHitCount,
            clusterGap,
            previewBytes,
            cancellationToken);
        var nextFamily = await DumpRuntimeDayPayloadFamilyAsync(
            attachmentId,
            nextDate.Year,
            nextDate.Month,
            nextDate.Day,
            startAddress,
            stopAddress,
            calendarBaseAddress,
            preferredStrides,
            minHitCount,
            clusterGap,
            previewBytes,
            cancellationToken);
        var temporalComparison = BuildRuntimeDayPayloadFamilyComparisonReport(
            attachmentId.Value,
            previousFamily,
            family,
            nextFamily);
        var temporalSummary = BuildTemporalComparisonSummary(temporalComparison);

        var dominantStrides = family.Clusters
            .GroupBy(static cluster => cluster.TypicalStride)
            .OrderByDescending(group => group.Count())
            .ThenBy(group => group.Key)
            .Select(group => group.Key)
            .Take(8)
            .ToArray();

        var headerTypes = secondary.HeaderEvents
            .Select(item => item.Type)
            .Distinct()
            .OrderBy(static value => value)
            .ToArray();

        var hasSpecial472Family = family.Clusters.Any(static cluster => cluster.TypicalStride >= 470 && cluster.TypicalStride <= 474);
        var only528LikeClusters = family.Clusters.All(static cluster => cluster.TypicalStride >= 520 && cluster.TypicalStride <= 536);
        var reasons = new List<string>
        {
            $"secondary_count={secondary.Count}",
            $"secondary_items={secondary.Items.Count}",
            $"cluster_count={family.ClusterCount}",
            $"total_hits={family.TotalHits}",
            $"dominant_strides={string.Join(",", dominantStrides)}",
            $"temporal_prev_matches={temporalSummary.PreviousMatchedClusterCount}",
            $"temporal_next_matches={temporalSummary.NextMatchedClusterCount}",
            $"temporal_stable_clusters={temporalSummary.StableClusterCount}",
            $"temporal_isolated_clusters={temporalSummary.IsolatedClusterCount}",
        };

        string variantKey;
        string confidence;

        if (hasSpecial472Family && secondary.Count == 0 && family.ClusterCount <= 5)
        {
            variantKey = "placeholder_special_runtime";
            confidence = "high";
            reasons.Add("detected_small_472_family_with_empty_secondary_day");
        }
        else if (secondary.Count == 0 && family.ClusterCount == 1 && only528LikeClusters)
        {
            variantKey = "no_games_runtime";
            confidence = "medium";
            reasons.Add("single_528_like_family_with_empty_secondary_day");
        }
        else if (secondary.Count > 0 && secondary.Count <= 8 && family.ClusterCount == 1 && only528LikeClusters)
        {
            variantKey = "placeholder_organized_runtime";
            confidence = "medium";
            reasons.Add("small_secondary_payload_with_single_organized_528_family");
        }
        else if (secondary.Count > 8 && family.ClusterCount <= 4 && only528LikeClusters)
        {
            variantKey = "agenda_defined_organized_runtime";
            confidence = "medium";
            reasons.Add("non_empty_secondary_payload_with_small_organized_528_family");
        }
        else if (secondary.Count > 0 && family.ClusterCount >= 20)
        {
            variantKey = "agenda_defined_noisy_runtime";
            confidence = "medium";
            reasons.Add("non_empty_secondary_payload_with_heavy_multi_cluster_runtime_noise");
        }
        else
        {
            variantKey = "unknown_runtime";
            confidence = "low";
            reasons.Add("runtime_shape_did_not_match_current_heuristics");
        }

        if (headerTypes.Contains((ushort)0x0009))
        {
            reasons.Add("secondary_header_contains_0x0009_variant_marker");
        }

        var (semanticEventKey, semanticEventConfidence, semanticReasons) = ClassifySemanticEvent(
            year,
            month,
            day,
            headerTypes,
            secondary.Count,
            family.ClusterCount,
            variantKey);

        return new Pes2021RuntimeCalendarVariantReport(
            attachmentId.Value,
            family.DayIndex,
            family.Year,
            family.Month,
            family.Day,
            variantKey,
            confidence,
            semanticEventKey,
            semanticEventConfidence,
            secondary.Count,
            secondary.Items.Count,
            headerTypes,
            family.TotalHits,
            family.ClusterCount,
            dominantStrides,
            hasSpecial472Family,
                temporalSummary,
            secondary.Count > 0 || secondary.Items.Count > 0 ? "secondary-backed" : "runtime-projected",
            family.ClusterCount > 0 || secondary.Count > 0 || secondary.Items.Count > 0 || headerTypes.Length > 0 ? "visible" : "projected",
            semanticEventKey != "unknown_event" ? "stop" : variantKey != "unknown_runtime" ? "candidate" : "no-stop",
            semanticReasons,
            reasons);
    }

    public async Task<Pes2021AnnualCalendarEventInventoryReport> InventoryAnnualEventsAsync(
        AttachmentId attachmentId,
        int year,
        ulong? calendarBaseAddress = null,
        ulong? secondaryBaseAddress = null,
        CancellationToken cancellationToken = default)
    {
        var resolvedMainBase = await ResolveBaseAddressAsync(attachmentId, calendarBaseAddress, cancellationToken);
        var resolvedSecondaryBase = await ResolveSecondaryBaseAddressAsync(attachmentId, year, 9, 22, secondaryBaseAddress, cancellationToken);
        var summary = await CalendarSummaryAsync(attachmentId, resolvedMainBase, cancellationToken: cancellationToken);
        var matchCounts = summary.Dates.ToDictionary(
            static item => item.Date,
            static item => item.MatchCount,
            StringComparer.Ordinal);

        var days = new List<Pes2021AnnualCalendarEventEntry>();
        var startDate = new DateOnly(year, 1, 1);
        for (var offset = 0; offset < Pes2021AgendaProfile.SecondaryDayCount; offset++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var date = startDate.AddDays(offset);
            var previousDate = date.AddDays(-1);
            var nextDate = date.AddDays(1);
            var dateKey = $"{date:yyyy-MM-dd}";
            var previousDateKey = $"{previousDate:yyyy-MM-dd}";
            var nextDateKey = $"{nextDate:yyyy-MM-dd}";
            var mainMatchCount = matchCounts.TryGetValue(dateKey, out var currentMatches) ? currentMatches : 0;
            var previousDayMainMatchCount = matchCounts.TryGetValue(previousDateKey, out var previousMatches) ? previousMatches : 0;
            var nextDayMainMatchCount = matchCounts.TryGetValue(nextDateKey, out var nextMatches) ? nextMatches : 0;

            var secondary = await DumpSecondaryDayAsync(
                attachmentId,
                date.Year,
                date.Month,
                date.Day,
                resolvedSecondaryBase,
                cancellationToken);

            var headerTypes = secondary.HeaderEvents
                .Select(item => item.Type)
                .Distinct()
                .OrderBy(static value => value)
                .ToArray();

            var (semanticEventKey, semanticEventConfidence, semanticReasons) = ClassifySemanticEvent(
                date.Year,
                date.Month,
                date.Day,
                headerTypes,
                secondary.Count,
                clusterCount: 0,
                variantKey: "inventory_only");

            if (semanticEventKey == "unknown_event"
                && mainMatchCount > 0
                && secondary.Count == 0
                && secondary.Items.Count == 0
                && headerTypes.Any(static value => value == 0x000D)
                && headerTypes.Any(static value => value == 0x003F))
            {
                semanticEventKey = "libertadores_round_of_16_first_leg_placeholder_candidate";
                semanticEventConfidence = "medium";
                semanticReasons =
                [
                    "matched_inventory_only_libertadores_round_of_16_first_leg_placeholder_signature",
                    $"main_match_count={mainMatchCount}",
                    $"header_types={string.Join(",", headerTypes.Select(static value => $"0x{value:X4}"))}"
                ];
            }

            if (semanticEventKey == "unknown_event"
                && mainMatchCount == 0
                && secondary.Count == 0
                && secondary.Items.Count == 0
                && (headerTypes.Length == 0 || headerTypes.All(static value => value == 0x003F)))
            {
                continue;
            }
            var (inventoryPatternKey, inventoryPatternConfidence, inventoryPatternReasons) = ClassifyInventoryPattern(
                headerTypes,
                secondary.Count,
                secondary.Items.Count,
                mainMatchCount,
                previousDayMainMatchCount,
                nextDayMainMatchCount,
                semanticEventKey);

            var (sourceRole, visibility, stopState) = BuildInventoryDisposition(
                mainMatchCount,
                secondary.Count,
                secondary.Items.Count,
                headerTypes.Length,
                semanticEventKey,
                inventoryPatternKey);
            var dayRole = BuildInventoryDayRole(
                mainMatchCount,
                semanticEventKey,
                inventoryPatternKey);

            days.Add(new Pes2021AnnualCalendarEventEntry(
                $"{date:yyyy-MM-dd}",
                secondary.DayIndex,
                mainMatchCount,
                previousDayMainMatchCount,
                nextDayMainMatchCount,
                secondary.Count,
                secondary.Items.Count,
                headerTypes,
                semanticEventKey,
                semanticEventConfidence,
                semanticReasons,
                inventoryPatternKey,
                inventoryPatternConfidence,
                dayRole,
                sourceRole,
                visibility,
                stopState,
                inventoryPatternReasons));
        }

        return new Pes2021AnnualCalendarEventInventoryReport(
            attachmentId.Value,
            year,
            resolvedMainBase,
            resolvedSecondaryBase,
            days.Count,
            days
                .OrderBy(static item => item.Date, StringComparer.Ordinal)
                .ToArray());
    }

    private async Task<Pes2021CalendarBaseResult?> FindBaseForDateAsync(
        AttachmentId attachmentId,
        int year,
        int month,
        int day,
        int roundValue,
        int competitionCode,
        string? moduleName,
        int maxResults,
        CancellationToken cancellationToken)
    {
        var patterns = new[]
        {
            new SearchPatternSpec(
                "specific",
                BuildDatePattern(year, month, day, roundValue, competitionCode),
                new long[] { 0 }),
            new SearchPatternSpec(
                "generic",
                BuildDateOnlyPattern(year, month, day),
                new long[] { -4, 0 }),
        };

        foreach (var pattern in patterns)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var scanResult = await memoryService.ScanPatternAsync(
                new PatternScanRequest(attachmentId, pattern.Pattern, moduleName, maxResults),
                cancellationToken);

            if (scanResult.Addresses.Count == 0)
            {
                continue;
            }

            var candidates = new List<Pes2021CalendarBaseCandidate>();
            foreach (var matchAddress in scanResult.Addresses)
            {
                cancellationToken.ThrowIfCancellationRequested();

                foreach (var adjustment in pattern.Adjustments)
                {
                    if (adjustment < 0 && matchAddress < unchecked((ulong)(-adjustment)))
                    {
                        continue;
                    }

                    var candidateBaseAddress = adjustment >= 0
                        ? matchAddress + unchecked((ulong)adjustment)
                        : matchAddress - unchecked((ulong)(-adjustment));

                    var validationScore = await ValidateBaseAsync(attachmentId, candidateBaseAddress, 10, cancellationToken);
                    var strongScore = await CountStrongRecordsAsync(attachmentId, candidateBaseAddress, 10, cancellationToken);
                    var clusterSpan = await CountClusterSpanAsync(attachmentId, candidateBaseAddress, 256, cancellationToken);
                    candidates.Add(new Pes2021CalendarBaseCandidate(
                        matchAddress,
                        candidateBaseAddress,
                        validationScore,
                        strongScore,
                        clusterSpan));
                }
            }

            var bestCandidate = candidates
                .OrderByDescending(candidate => candidate.ValidationScore)
                .ThenByDescending(candidate => candidate.StrongScore)
                .ThenByDescending(candidate => candidate.ClusterSpan)
                .ThenBy(candidate => candidate.CandidateBaseAddress)
                .FirstOrDefault();

            if (bestCandidate is null || bestCandidate.ValidationScore < 5)
            {
                continue;
            }

            var normalizedBase = await NormalizeClusterStartAsync(attachmentId, bestCandidate.CandidateBaseAddress, 512, cancellationToken);
            var anchorIndex = normalizedBase <= bestCandidate.CandidateBaseAddress
                ? checked((int)((bestCandidate.CandidateBaseAddress - normalizedBase) / (ulong)Pes2021AgendaProfile.RecordStride))
                : 0;

            return new Pes2021CalendarBaseResult(
                attachmentId.Value,
                bestCandidate.MatchAddress,
                bestCandidate.CandidateBaseAddress,
                normalizedBase,
                anchorIndex,
                bestCandidate.ValidationScore,
                bestCandidate.StrongScore,
                bestCandidate.ClusterSpan,
                pattern.Pattern,
                pattern.Name,
                year,
                competitionCode,
                roundValue,
                candidates
                    .OrderByDescending(candidate => candidate.ValidationScore)
                    .ThenByDescending(candidate => candidate.StrongScore)
                    .ThenByDescending(candidate => candidate.ClusterSpan)
                    .ThenBy(candidate => candidate.CandidateBaseAddress)
                    .Take(10)
                    .ToArray());
        }

        return null;
    }

    private async Task<int> ValidateBaseAsync(AttachmentId attachmentId, ulong baseAddress, int sampleCount, CancellationToken cancellationToken)
    {
        var score = 0;
        for (var index = 0; index < sampleCount; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var record = await TryReadRecordAsync(attachmentId, baseAddress + (ulong)(index * Pes2021AgendaProfile.RecordStride), index, cancellationToken);
            if (record is null || !IsPlausibleRecord(record))
            {
                break;
            }

            score++;
        }

        return score;
    }

    private async Task<int> CountStrongRecordsAsync(AttachmentId attachmentId, ulong baseAddress, int sampleCount, CancellationToken cancellationToken)
    {
        var score = 0;
        for (var index = 0; index < sampleCount; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var record = await TryReadRecordAsync(attachmentId, baseAddress + (ulong)(index * Pes2021AgendaProfile.RecordStride), index, cancellationToken);
            if (record is null || !IsStrongRecord(record))
            {
                break;
            }

            score++;
        }

        return score;
    }

    private async Task<int> CountClusterSpanAsync(AttachmentId attachmentId, ulong baseAddress, int maxSteps, CancellationToken cancellationToken)
    {
        var backward = 0;
        var forward = 0;

        var cursor = baseAddress >= (ulong)Pes2021AgendaProfile.RecordStride
            ? baseAddress - (ulong)Pes2021AgendaProfile.RecordStride
            : 0;

        while (backward < maxSteps && cursor > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var record = await TryReadRecordAsync(attachmentId, cursor, -1, cancellationToken);
            if (record is null || !IsPlausibleRecord(record) || !await HasForwardPlausibleRunAsync(attachmentId, cursor, 4, 3, cancellationToken))
            {
                break;
            }

            backward++;
            if (cursor < (ulong)Pes2021AgendaProfile.RecordStride)
            {
                break;
            }

            cursor -= (ulong)Pes2021AgendaProfile.RecordStride;
        }

        cursor = baseAddress + (ulong)Pes2021AgendaProfile.RecordStride;
        while (forward < maxSteps)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var record = await TryReadRecordAsync(attachmentId, cursor, -1, cancellationToken);
            if (record is null || !IsPlausibleRecord(record) || !await HasForwardPlausibleRunAsync(attachmentId, cursor, 4, 3, cancellationToken))
            {
                break;
            }

            forward++;
            cursor += (ulong)Pes2021AgendaProfile.RecordStride;
        }

        return backward + 1 + forward;
    }

    private async Task<ulong> NormalizeClusterStartAsync(AttachmentId attachmentId, ulong baseAddress, int maxSteps, CancellationToken cancellationToken)
    {
        var startBase = baseAddress;
        var steps = 0;
        var cursor = baseAddress >= (ulong)Pes2021AgendaProfile.RecordStride
            ? baseAddress - (ulong)Pes2021AgendaProfile.RecordStride
            : 0;

        while (steps < maxSteps && cursor > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var record = await TryReadRecordAsync(attachmentId, cursor, -1, cancellationToken);
            if (record is null || !IsPlausibleRecord(record) || !await HasForwardPlausibleRunAsync(attachmentId, cursor, 4, 3, cancellationToken))
            {
                break;
            }

            startBase = cursor;
            steps++;
            if (cursor < (ulong)Pes2021AgendaProfile.RecordStride)
            {
                break;
            }

            cursor -= (ulong)Pes2021AgendaProfile.RecordStride;
        }

        return startBase;
    }

    private async Task<bool> HasForwardPlausibleRunAsync(AttachmentId attachmentId, ulong baseAddress, int runLength, int minScore, CancellationToken cancellationToken)
    {
        var score = 0;
        for (var index = 0; index < runLength; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var record = await TryReadRecordAsync(attachmentId, baseAddress + (ulong)(index * Pes2021AgendaProfile.RecordStride), index, cancellationToken);
            if (record is null || !IsPlausibleRecord(record))
            {
                break;
            }

            score++;
        }

        return score >= minScore;
    }

    private async Task<Pes2021SecondaryScoreResult> ScoreSecondaryCalendarBaseAsync(
        AttachmentId attachmentId,
        ulong baseAddress,
        IReadOnlyList<int>? sampleDays,
        CancellationToken cancellationToken)
    {
        var days = sampleDays is { Count: > 0 }
            ? sampleDays
            : Pes2021AgendaProfile.SecondarySampleDays;

        var details = new Pes2021SecondaryScoreBuilder(baseAddress, days);
        var score = 0;

        foreach (var dayIndex in days)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var dayBase = baseAddress + ((ulong)dayIndex * (ulong)Pes2021AgendaProfile.SecondaryDayStride);
            var block = await ReadBytesAsync(attachmentId, dayBase, Pes2021AgendaProfile.SecondaryDayStride, cancellationToken);
            var summary = new Pes2021SecondaryDaySummary(dayIndex, null, null, false, "n/a");

            if (block is null || block.Length < Pes2021AgendaProfile.SecondaryDayStride)
            {
                details.Issues.Add($"day[{dayIndex}] unreadable");
                details.DaySummaries.Add(summary);
                continue;
            }

            var count = BinaryPrimitives.ReadUInt32LittleEndian(block.AsSpan(Pes2021AgendaProfile.SecondaryCountOffset, sizeof(uint)));
            summary = summary with { Count = count };

            if (dayIndex == Pes2021AgendaProfile.SecondaryDayCount - 1)
            {
                if (count == 0xFFFFFFFF)
                {
                    details.SpecialDayOk = true;
                    score += 4;
                }
                else
                {
                    details.Issues.Add($"day[{dayIndex}] expected 0xFFFFFFFF, got 0x{count:X}");
                }

                details.DaySummaries.Add(summary);
                continue;
            }

            if (count <= (uint)SecondaryMaxItems)
            {
                details.PlausibleCounts++;
                score++;
            }
            else
            {
                details.Issues.Add($"day[{dayIndex}] count=0x{count:X} above max={SecondaryMaxItems}");
            }

            var (items, terminatorFound) = ReadSecondaryCalendarItemsFromBlock(block);
            summary = summary with
            {
                ItemCount = items?.Count,
                TerminatorFound = terminatorFound,
            };

            if (items is not null)
            {
                if (terminatorFound)
                {
                    details.TerminatorDays++;
                    score++;
                }
                else
                {
                    details.Issues.Add($"day[{dayIndex}] missing 0xFFFF terminator");
                }

                if (count == (uint)items.Count)
                {
                    details.MatchedCounts++;
                    score += 2;
                }
                else
                {
                    details.Issues.Add($"day[{dayIndex}] count=0x{count:X} differs from items={items.Count}");
                }
            }
            else
            {
                details.Issues.Add($"day[{dayIndex}] unreadable items");
            }

            var headerState = EvaluateSecondaryCalendarHeader(block);
            summary = summary with { HeaderState = headerState.State };
            if (headerState.Ok)
            {
                details.HeaderDays++;
                score++;
            }
            else
            {
                details.Issues.Add($"day[{dayIndex}] header={headerState.State}");
            }

            details.DaySummaries.Add(summary);
        }

        var report = new Pes2021SecondaryCalendarCandidateReport(
            baseAddress,
            score,
            details.PlausibleCounts,
            details.MatchedCounts,
            details.TerminatorDays,
            details.HeaderDays,
            details.SpecialDayOk,
            details.DaySummaries.ToArray(),
            details.Issues.ToArray());

        return new Pes2021SecondaryScoreResult(score, report);
    }

    private static (IReadOnlyList<ushort>? Items, bool TerminatorFound) ReadSecondaryCalendarItemsFromBlock(byte[] block)
    {
        var items = new List<ushort>();
        var itemsBase = Pes2021AgendaProfile.SecondaryItemsStart;
        var terminatorFound = false;
        var maxItems = SecondaryMaxItems;

        for (var itemIndex = 0; itemIndex < maxItems; itemIndex++)
        {
            var offset = itemsBase + (itemIndex * sizeof(ushort));
            if (offset + sizeof(ushort) > block.Length)
            {
                return (null, false);
            }

            var value = BinaryPrimitives.ReadUInt16LittleEndian(block.AsSpan(offset, sizeof(ushort)));
            if (value == 0xFFFF)
            {
                terminatorFound = true;
                break;
            }

            items.Add(value);
        }

        return (items, terminatorFound);
    }

    private static SecondaryHeaderState EvaluateSecondaryCalendarHeader(byte[] block)
    {
        var validSlots = 0;
        var sawTerminator = false;

        for (var slotIndex = 0; slotIndex < Pes2021AgendaProfile.SecondaryHeaderMaxEvents; slotIndex++)
        {
            var slotOffset = slotIndex * Pes2021AgendaProfile.SecondaryHeaderEventSize;
            if (slotOffset + Pes2021AgendaProfile.SecondaryHeaderEventSize > block.Length)
            {
                return new SecondaryHeaderState(false, "unreadable");
            }

            var year = BinaryPrimitives.ReadUInt16LittleEndian(block.AsSpan(slotOffset, sizeof(ushort)));
            if (year == 0xFFFF)
            {
                sawTerminator = true;
                break;
            }

            var month = block[slotOffset + 0x02];
            var day = block[slotOffset + 0x03];
            if (year < 2020 || year > 2040 || month is < 1 or > 12 || day is < 1 or > 31)
            {
                return new SecondaryHeaderState(false, $"slot_{slotIndex}_invalid");
            }

            validSlots++;
        }

        if (validSlots == 0 && !sawTerminator)
        {
            return new SecondaryHeaderState(false, "no_terminator");
        }

        return new SecondaryHeaderState(true, sawTerminator ? "terminator" : "valid_slots");
    }

    private static int CompareSecondaryReports(Pes2021SecondaryCalendarCandidateReport left, Pes2021SecondaryCalendarCandidateReport right)
    {
        if (left.Score != right.Score)
        {
            return right.Score.CompareTo(left.Score);
        }

        return left.BaseAddress.CompareTo(right.BaseAddress);
    }

    private async Task<Pes2021CalendarRecordSnapshot?> TryReadRecordAsync(
        AttachmentId attachmentId,
        ulong address,
        int index,
        CancellationToken cancellationToken)
    {
        var bytes = await ReadBytesAsync(attachmentId, address, Pes2021AgendaProfile.RecordStride, cancellationToken);
        if (bytes is null || bytes.Length < Pes2021AgendaProfile.RecordStride)
        {
            return null;
        }

        var competitionCode = BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(0x00, sizeof(ushort)));
        var roundValue = bytes[0x02];
        var year = BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(0x04, sizeof(ushort)));
        var month = bytes[0x06];
        var day = bytes[0x07];
        var homeId = BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(0x10, sizeof(ushort)));
        var homeLiga = BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(0x12, sizeof(ushort)));
        var awayId = BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(0x14, sizeof(ushort)));
        var awayLiga = BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(0x16, sizeof(ushort)));
        var homeScore = bytes[0x18];
        var awayScore = bytes[0x1B];
        var competitionName = GetCompetitionName(competitionCode);

        return new Pes2021CalendarRecordSnapshot(
            index,
            address,
            competitionCode,
            competitionName,
            roundValue,
            year,
            month,
            day,
            homeId,
            homeLiga,
            awayId,
            awayLiga,
            homeScore,
            awayScore,
            homeId == 65535 || awayId == 65535,
            BuildEventId(index, competitionCode, year, month, day, roundValue, homeId, awayId, homeLiga));
    }

    private async Task<byte[]?> ReadBytesAsync(AttachmentId attachmentId, ulong address, int size, CancellationToken cancellationToken)
    {
        try
        {
            var result = await memoryService.ReadAsync(
                new ReadMemoryRequest(attachmentId, address, MemoryValueKind.Bytes, size),
                cancellationToken);

            return Convert.FromHexString(result.Value);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            return null;
        }
    }

    private async Task<byte[]> ReadClusterPreviewBytesAsync(
        AttachmentId attachmentId,
        ulong clusterStartAddress,
        ulong regionBaseAddress,
        ulong regionSize,
        int requestedSize,
        CancellationToken cancellationToken)
    {
        var clippedSize = ClipReadSizeToRegion(regionBaseAddress, regionSize, clusterStartAddress, requestedSize);
        if (clippedSize <= 0)
        {
            return [];
        }

        return await ReadBytesAsync(attachmentId, clusterStartAddress, clippedSize, cancellationToken) ?? [];
    }

    private async Task<ulong> ResolveBaseAddressAsync(AttachmentId attachmentId, ulong? baseAddress, CancellationToken cancellationToken)
    {
        if (baseAddress is > 0)
        {
            return baseAddress.Value;
        }

        if (_baseCache.TryGetValue(attachmentId, out var cached))
        {
            return cached.NormalizedBaseAddress;
        }

        var result = await FindBaseAsync(attachmentId, cancellationToken: cancellationToken);
        return result.NormalizedBaseAddress;
    }

    private async Task<ulong> ResolveSecondaryBaseAddressAsync(
        AttachmentId attachmentId,
        int year,
        int month,
        int day,
        ulong? baseAddress,
        CancellationToken cancellationToken)
    {
        if (baseAddress is > 0)
        {
            return baseAddress.Value;
        }

        if (_secondaryBaseCache.TryGetValue(attachmentId, out var cached)
            && cached.Year == year
            && cached.Month == month
            && cached.Day == day)
        {
            return cached.BaseAddress;
        }

        var result = await FindSecondaryBaseByDateAsync(attachmentId, year, month, day, cancellationToken: cancellationToken);
        return result.BaseAddress;
    }

    private static bool IsPlausibleRecord(Pes2021CalendarRecordSnapshot? record)
    {
        if (record is null)
        {
            return false;
        }

        return record.CompetitionCode is > 0 and <= MaxPlausibleCompetitionCode
            && record.Year is >= 2020 and <= 2040
            && record.Month is >= 1 and <= 12
            && record.Day is >= 1 and <= 31
            && record.Round is >= 0 and <= 80;
    }

    private static bool IsStrongRecord(Pes2021CalendarRecordSnapshot? record)
    {
        if (!IsPlausibleRecord(record))
        {
            return false;
        }

        return record!.HomeId is >= 0 and <= 5000
            && record.AwayId is >= 0 and <= 5000;
    }

    private string GetCompetitionName(int competitionCode)
    {
        var map = _competitionMap.Value;
        return map.TryGetValue(competitionCode, out var name) ? name : "DESCONHECIDA";
    }

    private static string ResolveComparisonCompetitionName(
        Pes2021CompetitionDateSummary? first,
        Pes2021CompetitionDateSummary? second)
    {
        if (first is not null && !string.Equals(first.CompetitionName, "DESCONHECIDA", StringComparison.OrdinalIgnoreCase))
        {
            return first.CompetitionName;
        }

        if (second is not null && !string.Equals(second.CompetitionName, "DESCONHECIDA", StringComparison.OrdinalIgnoreCase))
        {
            return second.CompetitionName;
        }

        return first?.CompetitionName
            ?? second?.CompetitionName
            ?? "DESCONHECIDA";
    }

    private static string BuildDatePattern(int year, int month, int day, int roundValue, int competitionCode)
        => string.Format(
            CultureInfo.InvariantCulture,
            "{0:X2} {1:X2} {2:X2} ?? {3:X2} {4:X2} {5:X2} {6:X2}",
            competitionCode & 0xFF,
            (competitionCode >> 8) & 0xFF,
            roundValue & 0xFF,
            year & 0xFF,
            (year >> 8) & 0xFF,
            month & 0xFF,
            day & 0xFF);

    private static string BuildDateOnlyPattern(int year, int month, int day)
        => string.Format(
            CultureInfo.InvariantCulture,
            "?? ?? ?? ?? {0:X2} {1:X2} {2:X2} {3:X2}",
            year & 0xFF,
            (year >> 8) & 0xFF,
            month & 0xFF,
            day & 0xFF);

    private static string BuildSecondaryDatePattern(int year, int month, int day)
        => string.Format(
            CultureInfo.InvariantCulture,
            "{0:X2} {1:X2} {2:X2} {3:X2}",
            year & 0xFF,
            (year >> 8) & 0xFF,
            month & 0xFF,
            day & 0xFF);

    private static string FormatDateKey(int year, int month, int day)
        => string.Format(CultureInfo.InvariantCulture, "{0:D4}{1:D2}{2:D2}", year, month, day);

    private static int ComputeSecondaryDayIndex(int year, int month, int day)
    {
        var matchDate = new DateOnly(year, month, day);
        var seasonStart = new DateOnly(year, 1, 1);
        var dayIndex = matchDate.DayNumber - seasonStart.DayNumber;
        if (dayIndex < 0 || dayIndex >= Pes2021AgendaProfile.SecondaryDayCount)
        {
            throw new ArgumentOutOfRangeException(nameof(day), $"The date {year:D4}-{month:D2}-{day:D2} falls outside the supported secondary-calendar range.");
        }

        return dayIndex;
    }

    private static string FormatRounds(HashSet<int> rounds)
    {
        if (rounds.Count == 0)
        {
            return "-";
        }

        var ordered = rounds.OrderBy(value => value).ToArray();
        if (ordered.Length == 1)
        {
            return ordered[0].ToString(CultureInfo.InvariantCulture);
        }

        return string.Format(CultureInfo.InvariantCulture, "{0}-{1}", ordered[0], ordered[^1]);
    }

    private static CompetitionBuilder GetOrCreateBuilder(
        Dictionary<int, CompetitionBuilder> competitionCounts,
        int competitionCode,
        string competitionName)
    {
        if (competitionCounts.TryGetValue(competitionCode, out var builder))
        {
            return builder;
        }

        builder = new CompetitionBuilder(competitionCode, competitionName);
        competitionCounts[competitionCode] = builder;
        return builder;
    }

    private static SummaryBuilder GetOrCreateSummaryBuilder(
        Dictionary<string, SummaryBuilder> summaries,
        string dateKey,
        int year,
        int month,
        int day)
    {
        if (summaries.TryGetValue(dateKey, out var builder))
        {
            return builder;
        }

        builder = new SummaryBuilder(string.Format(CultureInfo.InvariantCulture, "{0:D4}-{1:D2}-{2:D2}", year, month, day));
        summaries[dateKey] = builder;
        return builder;
    }

    private static string BuildEventId(
        int index,
        int competitionCode,
        int year,
        int month,
        int day,
        int roundValue,
        int homeId,
        int awayId,
        int homeLiga)
    {
        return string.Format(
            CultureInfo.InvariantCulture,
            "{0}|{1}|{2:D4}{3:D2}{4:D2}|{5}|{6}|{7}|{8}",
            index,
            competitionCode,
            year,
            month,
            day,
            roundValue,
            homeId,
            awayId,
            homeLiga);
    }

    private static readonly int SecondaryMaxItems = (Pes2021AgendaProfile.SecondaryItemsEnd - Pes2021AgendaProfile.SecondaryItemsStart) / sizeof(ushort);

    private static IReadOnlyList<Pes2021SecondaryCalendarHeaderEvent> ReadSecondaryHeaderEvents(byte[] block)
    {
        var events = new List<Pes2021SecondaryCalendarHeaderEvent>();
        for (var slotIndex = 0; slotIndex < Pes2021AgendaProfile.SecondaryHeaderMaxEvents; slotIndex++)
        {
            var slotOffset = slotIndex * Pes2021AgendaProfile.SecondaryHeaderEventSize;
            if (slotOffset + Pes2021AgendaProfile.SecondaryHeaderEventSize > block.Length)
            {
                break;
            }

            var year = BinaryPrimitives.ReadUInt16LittleEndian(block.AsSpan(slotOffset, sizeof(ushort)));
            if (year == 0xFFFF)
            {
                break;
            }

            var month = block[slotOffset + 0x02];
            var day = block[slotOffset + 0x03];
            var type = BinaryPrimitives.ReadUInt16LittleEndian(block.AsSpan(slotOffset + 0x04, sizeof(ushort)));
            var value = BinaryPrimitives.ReadInt16LittleEndian(block.AsSpan(slotOffset + 0x06, sizeof(short)));
            events.Add(new Pes2021SecondaryCalendarHeaderEvent(slotIndex, year, month, day, type, value));
        }

        return events;
    }

    private static string BuildInt32Pattern(int value)
    {
        Span<byte> bytes = stackalloc byte[sizeof(int)];
        BinaryPrimitives.WriteInt32LittleEndian(bytes, value);
        return string.Join(" ", bytes.ToArray().Select(static item => item.ToString("X2", CultureInfo.InvariantCulture)));
    }

    private static IReadOnlyList<MemoryRegionInfo> FilterRuntimeScanRegions(
        IReadOnlyList<MemoryRegionInfo> regions,
        ulong? startAddress,
        ulong? stopAddress)
    {
        var filtered = regions
            .Where(static region =>
                region.IsReadable
                && region.IsWritable
                && !region.IsExecutable
                && string.Equals(region.Type, "Private", StringComparison.OrdinalIgnoreCase))
            .Where(region =>
            {
                var regionStart = region.BaseAddress;
                var regionStop = checked(region.BaseAddress + region.RegionSize);
                if (startAddress.HasValue && regionStop <= startAddress.Value)
                {
                    return false;
                }

                if (stopAddress.HasValue && regionStart >= stopAddress.Value)
                {
                    return false;
                }

                return true;
            })
            .OrderBy(static region => region.BaseAddress)
            .ToArray();

        return filtered;
    }

    private static List<RuntimeClusterBuilder> BuildRuntimeClusters(
        IReadOnlyList<ulong> hits,
        IReadOnlyList<MemoryRegionInfo> regions,
        int clusterGap)
    {
        var clusters = new List<RuntimeClusterBuilder>();
        RuntimeClusterBuilder? current = null;

        foreach (var hit in hits)
        {
            var region = FindRegion(regions, hit);
            if (region is null)
            {
                continue;
            }

            if (current is null
                || current.Region.BaseAddress != region.BaseAddress
                || hit - current.EndAddress > (ulong)clusterGap)
            {
                current = new RuntimeClusterBuilder(region);
                clusters.Add(current);
            }

            current.Add(hit);
        }

        return clusters;
    }

    private static MemoryRegionInfo? FindRegion(IReadOnlyList<MemoryRegionInfo> regions, ulong address)
        => regions.FirstOrDefault(region =>
            region.BaseAddress <= address
            && checked(region.BaseAddress + region.RegionSize) > address);

    private static IReadOnlyList<int> DecodeInt32Preview(byte[] buffer)
    {
        if (buffer.Length < sizeof(int))
        {
            return [];
        }

        var count = Math.Min(buffer.Length / sizeof(int), 64);
        var values = new int[count];
        for (var index = 0; index < count; index++)
        {
            values[index] = BinaryPrimitives.ReadInt32LittleEndian(buffer.AsSpan(index * sizeof(int), sizeof(int)));
        }

        return values;
    }

    private static Dictionary<int, ClusterMatch> MatchClustersByPreviewSimilarity(
        IReadOnlyList<Pes2021RuntimeCalendarFamilyClusterReport> currentClusters,
        IReadOnlyList<Pes2021RuntimeCalendarFamilyClusterReport> candidateClusters)
    {
        var matches = new Dictionary<int, ClusterMatch>();
        var currentGroups = currentClusters
            .Select((cluster, index) => new IndexedCluster(index, cluster))
            .GroupBy(static item => item.Cluster.TypicalStride)
            .ToDictionary(
                static group => group.Key,
                static group => group.OrderBy(item => item.Cluster.ClusterStartAddress).ToArray());

        var candidateGroups = candidateClusters
            .Select((cluster, index) => new IndexedCluster(index, cluster))
            .GroupBy(static item => item.Cluster.TypicalStride)
            .ToDictionary(
                static group => group.Key,
                static group => group.OrderBy(item => item.Cluster.ClusterStartAddress).ToArray());

        foreach (var (stride, strideCurrentClusters) in currentGroups)
        {
            if (!candidateGroups.TryGetValue(stride, out var strideCandidateClusters))
            {
                continue;
            }

            var usedCurrent = new HashSet<int>();
            var usedCandidate = new HashSet<int>();
            var candidates = new List<ClusterMatchCandidate>(strideCurrentClusters.Length * strideCandidateClusters.Length);
            foreach (var currentCluster in strideCurrentClusters)
            {
                foreach (var candidateCluster in strideCandidateClusters)
                {
                    var sharedPreviewValueCount = CountSharedPreviewValues(currentCluster.Cluster.PreviewInt32, candidateCluster.Cluster.PreviewInt32);
                    if (sharedPreviewValueCount == 0)
                    {
                        continue;
                    }

                    candidates.Add(new ClusterMatchCandidate(
                        currentCluster.Index,
                        candidateCluster.Index,
                        sharedPreviewValueCount,
                        Math.Abs(currentCluster.Cluster.HitCount - candidateCluster.Cluster.HitCount)));
                }
            }

            foreach (var candidate in candidates
                .OrderByDescending(item => item.SharedPreviewValueCount)
                .ThenBy(item => item.HitCountDelta)
                .ThenBy(item => currentClusters[item.CurrentClusterIndex].ClusterStartAddress)
                .ThenBy(item => candidateClusters[item.CandidateClusterIndex].ClusterStartAddress))
            {
                if (!usedCurrent.Add(candidate.CurrentClusterIndex) || !usedCandidate.Add(candidate.CandidateClusterIndex))
                {
                    continue;
                }

                matches[candidate.CurrentClusterIndex] = new ClusterMatch(
                    candidateClusters[candidate.CandidateClusterIndex],
                    "preview-overlap",
                    candidate.SharedPreviewValueCount);
            }

            var remainingCurrent = strideCurrentClusters
                .Where(item => !usedCurrent.Contains(item.Index))
                .OrderBy(item => item.Cluster.ClusterStartAddress)
                .ToArray();
            var remainingCandidate = strideCandidateClusters
                .Where(item => !usedCandidate.Contains(item.Index))
                .OrderBy(item => item.Cluster.ClusterStartAddress)
                .ToArray();

            var fallbackCount = Math.Min(remainingCurrent.Length, remainingCandidate.Length);
            for (var index = 0; index < fallbackCount; index++)
            {
                var currentCluster = remainingCurrent[index];
                var candidateCluster = remainingCandidate[index];
                matches[currentCluster.Index] = new ClusterMatch(candidateCluster.Cluster, "stride-ordinal-fallback", 0);
            }
        }

        return matches;
    }

    private static int CountSharedPreviewValues(
        IReadOnlyList<int> left,
        IReadOnlyList<int> right)
    {
        if (left.Count == 0 || right.Count == 0)
        {
            return 0;
        }

        var shared = left.Distinct().ToHashSet();
        shared.IntersectWith(right);
        return shared.Count;
    }

    private static int ClipReadSizeToRegion(
        ulong regionBaseAddress,
        ulong regionSize,
        ulong readAddress,
        int requestedSize)
    {
        if (requestedSize <= 0)
        {
            return 0;
        }

        var regionStop = checked(regionBaseAddress + regionSize);
        if (readAddress >= regionStop)
        {
            return 0;
        }

        var remainingBytes = regionStop - readAddress;
        return (int)Math.Min((ulong)requestedSize, remainingBytes);
    }

    private static Pes2021RuntimeCalendarTemporalComparisonSummary BuildTemporalComparisonSummary(
        Pes2021RuntimeCalendarFamilyComparisonReport comparison)
    {
        var previousMatchedClusterCount = comparison.Clusters.Count(static cluster => cluster.PreviousClusterStartAddress.HasValue);
        var nextMatchedClusterCount = comparison.Clusters.Count(static cluster => cluster.NextClusterStartAddress.HasValue);
        var stableClusterCount = comparison.Clusters.Count(static cluster => cluster.PreviousClusterStartAddress.HasValue && cluster.NextClusterStartAddress.HasValue);
        var isolatedClusterCount = comparison.Clusters.Count(static cluster => !cluster.PreviousClusterStartAddress.HasValue && !cluster.NextClusterStartAddress.HasValue);
        var previousPreviewOverlapMatchCount = comparison.Clusters.Count(static cluster => string.Equals(cluster.PreviousMatchStrategy, "preview-overlap", StringComparison.Ordinal));
        var nextPreviewOverlapMatchCount = comparison.Clusters.Count(static cluster => string.Equals(cluster.NextMatchStrategy, "preview-overlap", StringComparison.Ordinal));
        var previousFallbackMatchCount = comparison.Clusters.Count(static cluster => string.Equals(cluster.PreviousMatchStrategy, "stride-ordinal-fallback", StringComparison.Ordinal));
        var nextFallbackMatchCount = comparison.Clusters.Count(static cluster => string.Equals(cluster.NextMatchStrategy, "stride-ordinal-fallback", StringComparison.Ordinal));

        return new Pes2021RuntimeCalendarTemporalComparisonSummary(
            comparison.Clusters.Count,
            previousMatchedClusterCount,
            nextMatchedClusterCount,
            stableClusterCount,
            isolatedClusterCount,
            previousPreviewOverlapMatchCount,
            nextPreviewOverlapMatchCount,
            previousFallbackMatchCount,
            nextFallbackMatchCount);
    }

    private static IReadOnlyList<Pes2021RuntimeCalendarValueDiff> BuildValueDiff(
        IReadOnlyList<Pes2021RuntimeCalendarPreviewRecord> source,
        IReadOnlyList<int>? excludeValues)
    {
        var exclude = excludeValues is null ? [] : excludeValues.ToHashSet();
        return source
            .Where(item => !exclude.Contains(item.Value))
            .GroupBy(item => item.Value)
            .Select(group =>
            {
                var first = group.First();
                return new Pes2021RuntimeCalendarValueDiff(first.Value, first.Resolved, first.Record);
            })
            .OrderBy(item => item.Value)
            .ToArray();
    }

    private static IReadOnlyList<int> IntersectOrderedPrefix(IReadOnlyList<int> left, IReadOnlyList<int> right)
    {
        var count = Math.Min(left.Count, right.Count);
        var values = new List<int>(count);
        for (var index = 0; index < count; index++)
        {
            if (left[index] != right[index])
            {
                break;
            }

            values.Add(left[index]);
        }

        return values;
    }

    private static IReadOnlyList<Pes2021RuntimeCalendarSequenceRun> BuildSequenceRuns(IReadOnlyList<int> values)
    {
        if (values.Count == 0)
        {
            return [];
        }

        var runs = new List<Pes2021RuntimeCalendarSequenceRun>();
        var current = new List<int> { values[0] };

        for (var index = 1; index < values.Count; index++)
        {
            var value = values[index];
            if (value == current[^1] + 1)
            {
                current.Add(value);
                continue;
            }

            runs.Add(new Pes2021RuntimeCalendarSequenceRun(current[0], current[^1], current.Count, current.ToArray()));
            current = [value];
        }

        runs.Add(new Pes2021RuntimeCalendarSequenceRun(current[0], current[^1], current.Count, current.ToArray()));
        return runs;
    }

    private static (string SemanticEventKey, string SemanticEventConfidence, IReadOnlyList<string> Reasons) ClassifySemanticEvent(
        int year,
        int month,
        int day,
        IReadOnlyList<ushort> headerTypes,
        uint secondaryCount,
        int clusterCount,
        string variantKey)
    {
        var reasons = new List<string>();
        var headerKey = string.Join(",", headerTypes.Select(static value => $"0x{value:X4}"));
        var headerSet = headerTypes.ToHashSet();

        if (IsKnownCallupDate(year, month, day))
        {
            reasons.Add("matched_confirmed_national_team_callup_date");
            reasons.Add($"variant={variantKey}");
            return ("national_team_callup", "high", reasons);
        }

        if (year == 2026 && month == 1 && day == 1)
        {
            reasons.Add("matched_confirmed_transfer_window_start_date");
            reasons.Add($"header_types={headerKey}");
            return ("transfer_window_start", "high", reasons);
        }

        if (year == 2026 && month == 7 && day == 1)
        {
            reasons.Add("matched_confirmed_transfer_window_start_date");
            reasons.Add($"header_types={headerKey}");
            return ("transfer_window_start", "high", reasons);
        }

        if (year == 2026 && month == 1 && day == 31)
        {
            reasons.Add("matched_confirmed_transfer_window_end_date");
            reasons.Add($"header_types={headerKey}");
            return ("transfer_window_end", "high", reasons);
        }

        if (year == 2026 && month == 8 && day == 31)
        {
            reasons.Add("matched_confirmed_transfer_window_end_date");
            reasons.Add($"header_types={headerKey}");
            return ("transfer_window_end", "high", reasons);
        }

        if (year == 2026 && month == 6 && day == 30)
        {
            reasons.Add("matched_confirmed_coach_offer_response_deadline");
            reasons.Add($"header_types={headerKey}");
            return ("coach_offer_response_deadline", "high", reasons);
        }

        if (year == 2026 && month == 12 && day == 29)
        {
            reasons.Add("matched_confirmed_asian_best_player_award_announcement");
            reasons.Add($"header_types={headerKey}");
            return ("asian_best_player_award_announcement", "high", reasons);
        }

        if (year == 2026 && month == 12 && day == 30)
        {
            reasons.Add("matched_confirmed_south_america_best_player_award_announcement");
            reasons.Add($"header_types={headerKey}");
            return ("south_america_best_player_award_announcement", "high", reasons);
        }

        if (year == 2026 && month == 12 && day == 31)
        {
            reasons.Add("matched_confirmed_last_day_of_season");
            reasons.Add($"header_types={headerKey}");
            return ("last_day_of_season", "high", reasons);
        }

        if (year == 2026 && month == 6 && day == 13)
        {
            reasons.Add("matched_confirmed_libertadores_round_of_16_first_leg_placeholder");
            reasons.Add($"header_types={headerKey}");
            return ("libertadores_round_of_16_first_leg_placeholder", "high", reasons);
        }

        if (year == 2026 && month == 3 && day == 19)
        {
            reasons.Add("matched_confirmed_libertadores_group_stage_matchday_2_followup");
            reasons.Add($"header_types={headerKey}");
            return ("libertadores_group_stage_matchday_2_followup", "high", reasons);
        }

        if (year == 2026 && month == 4 && day == 16)
        {
            reasons.Add("matched_confirmed_libertadores_group_stage_matchday_4_followup");
            reasons.Add($"header_types={headerKey}");
            return ("libertadores_group_stage_matchday_4_followup", "high", reasons);
        }

        if (year == 2026 && month == 6 && day == 26)
        {
            reasons.Add("matched_confirmed_club_ranking_update");
            reasons.Add($"header_types={headerKey}");
            return ("club_ranking_update", "high", reasons);
        }

        if (year == 2026 && month == 8 && day == 26)
        {
            reasons.Add("matched_confirmed_europe_best_player_award_prelude");
            reasons.Add($"header_types={headerKey}");
            return ("europe_best_player_award_prelude", "high", reasons);
        }

        if (year == 2026 && month == 8 && day == 27)
        {
            reasons.Add("matched_confirmed_europe_best_player_award_announcement");
            reasons.Add($"header_types={headerKey}");
            return ("europe_best_player_award_announcement", "high", reasons);
        }

        if (year == 2026 && month == 12 && day == 10)
        {
            reasons.Add("matched_confirmed_hidden_competition_projection_source_marker");
            reasons.Add($"header_types={headerKey}");
            return ("hidden_competition_projection_source_marker", "high", reasons);
        }

        if (year == 2026 && month == 12 && day == 12)
        {
            reasons.Add("matched_confirmed_hidden_competition_projection_source_marker");
            reasons.Add($"header_types={headerKey}");
            return ("hidden_competition_projection_source_marker", "high", reasons);
        }

        if (year == 2026 && month == 12 && day == 2)
        {
            reasons.Add("matched_confirmed_world_best_player_award_announcement");
            reasons.Add($"header_types={headerKey}");
            return ("world_best_player_award_announcement", "high", reasons);
        }

        if (HasHeaderSignature(headerSet, 0x0025, 0x003F))
        {
            reasons.Add("matched_libertadores_group_stage_matchday_2_followup_header_signature");
            reasons.Add($"variant={variantKey}");
            return ("libertadores_group_stage_matchday_2_followup_candidate", "medium", reasons);
        }

        if (HasHeaderSignature(headerSet, 0x0027, 0x003F))
        {
            reasons.Add("matched_libertadores_group_stage_matchday_4_followup_header_signature");
            reasons.Add($"variant={variantKey}");
            return ("libertadores_group_stage_matchday_4_followup_candidate", "medium", reasons);
        }

        if (HasHeaderSignature(headerSet, 0x0001, 0x0009, 0x003C, 0x003E) || HasHeaderSignature(headerSet, 0x0009, 0x003B))
        {
            reasons.Add("matched_transfer_window_start_header_signature");
            reasons.Add($"cluster_count={clusterCount}");
            return ("transfer_window_start_candidate", "medium", reasons);
        }

        if (HasHeaderSignature(headerSet, 0x0009, 0x000B) || HasHeaderSignature(headerSet, 0x0009, 0x000A))
        {
            reasons.Add("matched_transfer_window_end_header_signature");
            reasons.Add($"secondary_count={secondaryCount}");
            return ("transfer_window_end_candidate", "medium", reasons);
        }

        if (HasHeaderSignature(headerSet, 0x0004) || HasHeaderSignature(headerSet, 0x0007))
        {
            reasons.Add("matched_transfer_window_boundary_prelude_signature");
            return ("transfer_window_boundary_prelude_candidate", "medium", reasons);
        }

        if (HasHeaderSignature(headerSet, 0x000F, 0x0010, 0x0018))
        {
            reasons.Add("matched_midyear_admin_boundary_signature");
            return ("midyear_admin_boundary_candidate", "medium", reasons);
        }

        if (HasHeaderSignature(headerSet, 0x0020, 0x002C)
            || (HasHeaderSignature(headerSet, 0x000D) && !HasHeaderSignature(headerSet, 0x003F))
            || HasHeaderSignature(headerSet, 0x000E, 0x0011, 0x001A))
        {
            reasons.Add("matched_season_rollover_boundary_signature");
            return ("season_rollover_boundary_candidate", "medium", reasons);
        }

        return ("unknown_event", "low", []);
    }

    private static bool IsKnownCallupDate(int year, int month, int day)
        => year == 2026
            && ((month, day) == (3, 16)
                || (month, day) == (5, 26)
                || (month, day) == (6, 23)
                || (month, day) == (8, 21)
                || (month, day) == (9, 22)
                || (month, day) == (10, 30));

    private static bool HasHeaderSignature(HashSet<ushort> headerSet, params ushort[] requiredTypes)
        => requiredTypes.All(headerSet.Contains);

    private static (string InventoryPatternKey, string InventoryPatternConfidence, IReadOnlyList<string> Reasons) ClassifyInventoryPattern(
        IReadOnlyList<ushort> headerTypes,
        uint secondaryCount,
        int secondaryItemCount,
        int mainMatchCount,
        int previousDayMainMatchCount,
        int nextDayMainMatchCount,
        string semanticEventKey)
    {
        if (semanticEventKey != "unknown_event")
        {
            return ("known_semantic_event", "high", [$"semantic={semanticEventKey}"]);
        }

        var reasons = new List<string>
        {
            $"main={mainMatchCount}",
            $"prev={previousDayMainMatchCount}",
            $"next={nextDayMainMatchCount}",
            $"secondary_count={secondaryCount}",
            $"secondary_items={secondaryItemCount}",
            $"headers={string.Join(",", headerTypes.Select(static value => $"0x{value:X4}"))}",
        };

        if (headerTypes.Count == 0 && secondaryCount > 0 && secondaryItemCount > 0 && nextDayMainMatchCount > 0)
        {
            reasons.Add("secondary_payload_points_to_next_day_schedule_without_header_markers");
            return ("next_day_schedule_bridge_candidate", "medium", reasons);
        }

        if (headerTypes.Count == 0 && secondaryCount > 0 && secondaryItemCount > 0 && nextDayMainMatchCount == 0 && previousDayMainMatchCount == 0)
        {
            reasons.Add("standalone_secondary_payload_without_neighbor_matches");
            return ("standalone_secondary_payload_candidate", "medium", reasons);
        }

        if (headerTypes.Count == 0 && secondaryCount > 0 && secondaryItemCount > 0 && previousDayMainMatchCount > 0 && nextDayMainMatchCount == 0)
        {
            reasons.Add("secondary_payload_after_previous_day_match_block");
            return ("post_match_followup_payload_candidate", "medium", reasons);
        }

        if (headerTypes.Count > 0 && headerTypes.All(static value => value == 0x003F) && secondaryCount > 0 && nextDayMainMatchCount > 0)
        {
            reasons.Add("0x003F_only_header_with_next_day_schedule_bridge");
            return ("placeholder_bridge_candidate", "medium", reasons);
        }

        if (headerTypes.Any(static value => value == 0x000D)
            && headerTypes.Any(static value => value == 0x003F)
            && secondaryCount == 0
            && secondaryItemCount == 0)
        {
            reasons.Add("0x000D_plus_0x003F_signature_is_shared_between_midyear_and_year_end_markers");
            return ("ambiguous_0x000D_0x003F_marker_candidate", "medium", reasons);
        }

        if (headerTypes.Count > 0 && headerTypes.Any(static value => value != 0x003F))
        {
            reasons.Add("rare_non_003F_header_marker_present");
            return ("rare_header_marker_candidate", "medium", reasons);
        }

        if (headerTypes.Count > 0 && headerTypes.All(static value => value == 0x003F) && secondaryCount == 0 && secondaryItemCount == 0)
        {
            reasons.Add("pure_0x003F_marker_without_local_payload");
            return ("pure_placeholder_marker_candidate", "medium", reasons);
        }

        return ("unknown_inventory_pattern", "low", reasons);
    }

    private static (string SourceRole, string Visibility, string StopState) BuildInventoryDisposition(
        int mainMatchCount,
        uint secondaryCount,
        int secondaryItemCount,
        int headerCount,
        string semanticEventKey,
        string inventoryPatternKey)
    {
        var sourceRole = secondaryCount > 0 || secondaryItemCount > 0 || headerCount > 0
            ? "secondary-backed"
            : mainMatchCount > 0
                ? "main-backed"
                : "runtime-projected";

        var visibility = mainMatchCount > 0
            || secondaryCount > 0
            || secondaryItemCount > 0
            || headerCount > 0
            || semanticEventKey != "unknown_event"
            ? "visible"
            : "projected";

        var stopState = semanticEventKey != "unknown_event"
            ? "stop"
            : mainMatchCount > 0
                ? "no-stop"
                : inventoryPatternKey != "unknown_inventory_pattern"
                    ? "candidate"
                    : "no-stop";

        return (sourceRole, visibility, stopState);
    }

    private static string BuildInventoryDayRole(
        int mainMatchCount,
        string semanticEventKey,
        string inventoryPatternKey)
    {
        if (mainMatchCount > 0 && semanticEventKey != "unknown_event")
        {
            return "mixed-match-and-semantic-day";
        }

        if (mainMatchCount > 0)
        {
            return "calendar-match-day";
        }

        if (semanticEventKey != "unknown_event")
        {
            return "semantic-event-day";
        }

        if (inventoryPatternKey != "unknown_inventory_pattern")
        {
            return "calendar-marker-day";
        }

        return "unclassified-day";
    }

    private async Task<IReadOnlyList<Pes2021RuntimeCalendarPreviewRecord>> DecodeRuntimePreviewRecordsAsync(
        AttachmentId attachmentId,
        ulong? baseAddress,
        IReadOnlyList<int> previewValues,
        CancellationToken cancellationToken)
    {
        if (previewValues.Count == 0)
        {
            return [];
        }

        var results = new List<Pes2021RuntimeCalendarPreviewRecord>(previewValues.Count);
        foreach (var value in previewValues.Take(32))
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (baseAddress is null || value < 0 || value >= 13014)
            {
                results.Add(new Pes2021RuntimeCalendarPreviewRecord(value, false, null));
                continue;
            }

            var record = await TryReadRecordAsync(
                attachmentId,
                baseAddress.Value + (ulong)(value * Pes2021AgendaProfile.RecordStride),
                value,
                cancellationToken);

            if (IsPlausibleRecord(record))
            {
                results.Add(new Pes2021RuntimeCalendarPreviewRecord(value, true, record));
                continue;
            }

            results.Add(new Pes2021RuntimeCalendarPreviewRecord(value, false, null));
        }

        return results;
    }

    private async Task<IReadOnlyList<ulong>> ScanInt32InRegionAsync(
        AttachmentId attachmentId,
        MemoryRegionInfo region,
        int expectedValue,
        ulong? scanStartAddress,
        ulong? scanStopAddress,
        CancellationToken cancellationToken)
    {
        const int chunkSize = 64 * 1024;
        const int scanAlignment = sizeof(int);
        var expectedBytes = BitConverter.GetBytes(expectedValue);
        var matches = new List<ulong>();
        var overlap = expectedBytes.Length - 1;

        var regionStart = region.BaseAddress;
        var regionStop = checked(region.BaseAddress + region.RegionSize);
        var startOffset = scanStartAddress.HasValue ? Math.Max(regionStart, scanStartAddress.Value) - regionStart : 0UL;
        var stopOffset = scanStopAddress.HasValue ? Math.Min(regionStop, scanStopAddress.Value) - regionStart : region.RegionSize;
        if (startOffset >= stopOffset)
        {
            return matches;
        }

        var alignmentRemainder = startOffset % (ulong)scanAlignment;
        if (alignmentRemainder != 0)
        {
            startOffset += (ulong)scanAlignment - alignmentRemainder;
        }

        if (startOffset >= stopOffset)
        {
            return matches;
        }

        for (var cursor = startOffset; cursor < stopOffset; cursor += (ulong)chunkSize)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var remaining = stopOffset - cursor;
            var primaryLength = (int)Math.Min((ulong)chunkSize, remaining);
            var bytesToRead = (int)Math.Min((ulong)(chunkSize + overlap), remaining);
            var absoluteAddress = checked(region.BaseAddress + cursor);
            var buffer = await ReadBytesAsync(attachmentId, absoluteAddress, bytesToRead, cancellationToken);
            if (buffer is null || buffer.Length < expectedBytes.Length)
            {
                continue;
            }

            var scanLimit = Math.Min(primaryLength, buffer.Length - expectedBytes.Length + 1);
            for (var position = 0; position < scanLimit; position += scanAlignment)
            {
                if (buffer[position] != expectedBytes[0]
                    || buffer[position + 1] != expectedBytes[1]
                    || buffer[position + 2] != expectedBytes[2]
                    || buffer[position + 3] != expectedBytes[3])
                {
                    continue;
                }

                matches.Add(absoluteAddress + (ulong)position);
            }
        }

        return matches;
    }

    private sealed class RuntimeClusterBuilder(MemoryRegionInfo region)
    {
        public MemoryRegionInfo Region { get; } = region;

        public List<ulong> Addresses { get; } = [];

        public ulong StartAddress => Addresses[0];

        public ulong EndAddress => Addresses[^1];

        public int TypicalStride
        {
            get
            {
                if (Addresses.Count < 2)
                {
                    return 0;
                }

                return Addresses
                    .Zip(Addresses.Skip(1), static (left, right) => checked((int)(right - left)))
                    .GroupBy(static delta => delta)
                    .OrderByDescending(group => group.Count())
                    .ThenBy(group => group.Key)
                    .Select(group => group.Key)
                    .FirstOrDefault();
            }
        }

        public void Add(ulong address) => Addresses.Add(address);
    }

    private sealed record IndexedCluster(int Index, Pes2021RuntimeCalendarFamilyClusterReport Cluster);

    private sealed record ClusterMatch(
        Pes2021RuntimeCalendarFamilyClusterReport Cluster,
        string Strategy,
        int SharedPreviewValueCount);

    private sealed record ClusterMatchCandidate(
        int CurrentClusterIndex,
        int CandidateClusterIndex,
        int SharedPreviewValueCount,
        int HitCountDelta);

    private sealed record SearchPatternSpec(string Name, string Pattern, long[] Adjustments);

    private sealed record CompetitionBuilder(int CompetitionCode, string CompetitionName)
    {
        public int MatchCount { get; set; }
        public HashSet<int> Rounds { get; } = new();
    }

    private sealed record SummaryBuilder(string Date)
    {
        public int MatchCount { get; set; }
        public HashSet<int> Competitions { get; } = new();
    }

    private sealed record SecondaryHeaderState(bool Ok, string State);

    private sealed record Pes2021SecondaryScoreResult(int Score, Pes2021SecondaryCalendarCandidateReport Report);

    private sealed class Pes2021SecondaryScoreBuilder
    {
        public Pes2021SecondaryScoreBuilder(ulong baseAddress, IReadOnlyList<int> sampleDays)
        {
            BaseAddress = baseAddress;
            SampleDays = sampleDays;
        }

        public ulong BaseAddress { get; }

        public IReadOnlyList<int> SampleDays { get; }

        public int PlausibleCounts { get; set; }

        public int MatchedCounts { get; set; }

        public int TerminatorDays { get; set; }

        public int HeaderDays { get; set; }

        public bool SpecialDayOk { get; set; }

        public List<Pes2021SecondaryDaySummary> DaySummaries { get; } = [];

        public List<string> Issues { get; } = [];
    }
}
