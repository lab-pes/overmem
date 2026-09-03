using Microsoft.Extensions.DependencyInjection;
using Overmem.Abstractions.Cli;
using Overmem.Abstractions.Memory;
using Overmem.Abstractions.Processes;
using Overmem.Application;
using Overmem.Extensions.Pes2021.ClubRelations;
using Overmem.Extensions.Pes2021.Fixtures;
using Overmem.Extensions.Pes2021.Players;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System;

namespace Overmem.Extensions.Pes2021.Cli;

public sealed class Pes2021CliExtension : ICliCommandExtension
{
    public CliCommand? TryParse(string commandName, IReadOnlyDictionary<string, string?> options)
    {
        return commandName switch
        {
            "pes2021-find-calendar-base" => new Pes2021FindCalendarBaseCliCommand(
                CliOptionParser.ParseSelector(options),
                CliOptionParser.ParseOptionalInt32(CliOptionParser.GetOptionalOption(options, "year")),
                CliOptionParser.ParseOptionalInt32(CliOptionParser.GetOptionalOption(options, "month")),
                CliOptionParser.ParseOptionalInt32(CliOptionParser.GetOptionalOption(options, "day")),
                CliOptionParser.ParseOptionalInt32(CliOptionParser.GetOptionalOption(options, "round")),
                CliOptionParser.ParseOptionalInt32(CliOptionParser.GetOptionalOption(options, "competition-code")),
                CliOptionParser.GetOptionalOption(options, "module-name"),
                CliOptionParser.ParseInt32(CliOptionParser.GetOptionalOption(options, "max-results") ?? "100")),
            "pes2021-dump-calendar-date" => new Pes2021DumpCalendarDateCliCommand(
                CliOptionParser.ParseSelector(options),
                CliOptionParser.ParseInt32(CliOptionParser.GetRequiredOption(options, "year")),
                CliOptionParser.ParseInt32(CliOptionParser.GetRequiredOption(options, "month")),
                CliOptionParser.ParseInt32(CliOptionParser.GetRequiredOption(options, "day")),
                CliOptionParser.ParseOptionalUnsignedLong(CliOptionParser.GetOptionalOption(options, "base-address")),
                CliOptionParser.ParseInt32(CliOptionParser.GetOptionalOption(options, "max-records") ?? "13014")),
            "pes2021-compare-calendar-dates" => new Pes2021CompareCalendarDatesCliCommand(
                CliOptionParser.ParseSelector(options),
                CliOptionParser.ParseInt32(CliOptionParser.GetRequiredOption(options, "first-year")),
                CliOptionParser.ParseInt32(CliOptionParser.GetRequiredOption(options, "first-month")),
                CliOptionParser.ParseInt32(CliOptionParser.GetRequiredOption(options, "first-day")),
                CliOptionParser.ParseInt32(CliOptionParser.GetRequiredOption(options, "second-year")),
                CliOptionParser.ParseInt32(CliOptionParser.GetRequiredOption(options, "second-month")),
                CliOptionParser.ParseInt32(CliOptionParser.GetRequiredOption(options, "second-day")),
                CliOptionParser.ParseOptionalUnsignedLong(CliOptionParser.GetOptionalOption(options, "base-address")),
                CliOptionParser.ParseInt32(CliOptionParser.GetOptionalOption(options, "max-records") ?? "13014")),
            "pes2021-calendar-summary" => new Pes2021CalendarSummaryCliCommand(
                CliOptionParser.ParseSelector(options),
                CliOptionParser.ParseOptionalUnsignedLong(CliOptionParser.GetOptionalOption(options, "base-address")),
                CliOptionParser.ParseInt32(CliOptionParser.GetOptionalOption(options, "max-records") ?? "13014")),
            "pes2021-inventory-annual-events" => new Pes2021InventoryAnnualEventsCliCommand(
                CliOptionParser.ParseSelector(options),
                CliOptionParser.ParseInt32(CliOptionParser.GetRequiredOption(options, "year")),
                CliOptionParser.ParseOptionalUnsignedLong(CliOptionParser.GetOptionalOption(options, "calendar-base-address")),
                CliOptionParser.ParseOptionalUnsignedLong(CliOptionParser.GetOptionalOption(options, "secondary-base-address"))),
            "pes2021-find-secondary-calendar-base-by-date" => new Pes2021FindSecondaryCalendarBaseByDateCliCommand(
                CliOptionParser.ParseSelector(options),
                CliOptionParser.ParseInt32(CliOptionParser.GetRequiredOption(options, "year")),
                CliOptionParser.ParseInt32(CliOptionParser.GetRequiredOption(options, "month")),
                CliOptionParser.ParseInt32(CliOptionParser.GetRequiredOption(options, "day")),
                CliOptionParser.GetOptionalOption(options, "module-name"),
                CliOptionParser.ParseInt32(CliOptionParser.GetOptionalOption(options, "max-results") ?? "256")),
            "pes2021-dump-secondary-calendar-day" => new Pes2021DumpSecondaryCalendarDayCliCommand(
                CliOptionParser.ParseSelector(options),
                CliOptionParser.ParseInt32(CliOptionParser.GetRequiredOption(options, "year")),
                CliOptionParser.ParseInt32(CliOptionParser.GetRequiredOption(options, "month")),
                CliOptionParser.ParseInt32(CliOptionParser.GetRequiredOption(options, "day")),
                CliOptionParser.ParseOptionalUnsignedLong(CliOptionParser.GetOptionalOption(options, "base-address"))),
            "pes2021-scan-runtime-day-index-clusters" => new Pes2021ScanRuntimeDayIndexClustersCliCommand(
                CliOptionParser.ParseSelector(options),
                CliOptionParser.ParseInt32(CliOptionParser.GetRequiredOption(options, "year")),
                CliOptionParser.ParseInt32(CliOptionParser.GetRequiredOption(options, "month")),
                CliOptionParser.ParseInt32(CliOptionParser.GetRequiredOption(options, "day")),
                CliOptionParser.ParseInt32(CliOptionParser.GetOptionalOption(options, "max-results") ?? "5000"),
                CliOptionParser.ParseInt32(CliOptionParser.GetOptionalOption(options, "cluster-gap") ?? "4096"),
                CliOptionParser.ParseInt32(CliOptionParser.GetOptionalOption(options, "preview-bytes") ?? "256")),
            "pes2021-dump-runtime-day-payload-family" => new Pes2021DumpRuntimeDayPayloadFamilyCliCommand(
                CliOptionParser.ParseSelector(options),
                CliOptionParser.ParseInt32(CliOptionParser.GetRequiredOption(options, "year")),
                CliOptionParser.ParseInt32(CliOptionParser.GetRequiredOption(options, "month")),
                CliOptionParser.ParseInt32(CliOptionParser.GetRequiredOption(options, "day")),
                CliOptionParser.ParseOptionalUnsignedLong(CliOptionParser.GetOptionalOption(options, "start-address")),
                CliOptionParser.ParseOptionalUnsignedLong(CliOptionParser.GetOptionalOption(options, "stop-address")),
                CliOptionParser.ParseOptionalUnsignedLong(CliOptionParser.GetOptionalOption(options, "calendar-base-address")),
                CliOptionParser.ParseInt32List(CliOptionParser.GetOptionalOption(options, "preferred-strides")),
                CliOptionParser.ParseInt32(CliOptionParser.GetOptionalOption(options, "min-hit-count") ?? "3"),
                CliOptionParser.ParseInt32(CliOptionParser.GetOptionalOption(options, "cluster-gap") ?? "4096"),
                CliOptionParser.ParseInt32(CliOptionParser.GetOptionalOption(options, "preview-bytes") ?? "256")),
            "pes2021-compare-runtime-day-payload-family" => new Pes2021CompareRuntimeDayPayloadFamilyCliCommand(
                CliOptionParser.ParseSelector(options),
                CliOptionParser.ParseInt32(CliOptionParser.GetRequiredOption(options, "year")),
                CliOptionParser.ParseInt32(CliOptionParser.GetRequiredOption(options, "month")),
                CliOptionParser.ParseInt32(CliOptionParser.GetRequiredOption(options, "day")),
                CliOptionParser.ParseOptionalUnsignedLong(CliOptionParser.GetOptionalOption(options, "start-address")),
                CliOptionParser.ParseOptionalUnsignedLong(CliOptionParser.GetOptionalOption(options, "stop-address")),
                CliOptionParser.ParseOptionalUnsignedLong(CliOptionParser.GetOptionalOption(options, "calendar-base-address")),
                CliOptionParser.ParseInt32List(CliOptionParser.GetOptionalOption(options, "preferred-strides")),
                CliOptionParser.ParseInt32(CliOptionParser.GetOptionalOption(options, "min-hit-count") ?? "3"),
                CliOptionParser.ParseInt32(CliOptionParser.GetOptionalOption(options, "cluster-gap") ?? "4096"),
                CliOptionParser.ParseInt32(CliOptionParser.GetOptionalOption(options, "preview-bytes") ?? "256")),
            "pes2021-dump-runtime-day-payload-cluster-detail" => new Pes2021DumpRuntimeDayPayloadClusterDetailCliCommand(
                CliOptionParser.ParseSelector(options),
                CliOptionParser.ParseInt32(CliOptionParser.GetRequiredOption(options, "year")),
                CliOptionParser.ParseInt32(CliOptionParser.GetRequiredOption(options, "month")),
                CliOptionParser.ParseInt32(CliOptionParser.GetRequiredOption(options, "day")),
                CliOptionParser.ParseInt32(CliOptionParser.GetRequiredOption(options, "cluster-ordinal")),
                CliOptionParser.ParseOptionalUnsignedLong(CliOptionParser.GetOptionalOption(options, "start-address")),
                CliOptionParser.ParseOptionalUnsignedLong(CliOptionParser.GetOptionalOption(options, "stop-address")),
                CliOptionParser.ParseOptionalUnsignedLong(CliOptionParser.GetOptionalOption(options, "calendar-base-address")),
                CliOptionParser.ParseInt32List(CliOptionParser.GetOptionalOption(options, "preferred-strides")),
                CliOptionParser.ParseInt32(CliOptionParser.GetOptionalOption(options, "min-hit-count") ?? "3"),
                CliOptionParser.ParseInt32(CliOptionParser.GetOptionalOption(options, "cluster-gap") ?? "4096"),
                CliOptionParser.ParseInt32(CliOptionParser.GetOptionalOption(options, "preview-bytes") ?? "256"),
                CliOptionParser.ParseInt32(CliOptionParser.GetOptionalOption(options, "ints-before-hit") ?? "8"),
                CliOptionParser.ParseInt32(CliOptionParser.GetOptionalOption(options, "ints-after-hit") ?? "24")),
            "pes2021-analyze-runtime-day-payload-cluster" => new Pes2021AnalyzeRuntimeDayPayloadClusterCliCommand(
                CliOptionParser.ParseSelector(options),
                CliOptionParser.ParseInt32(CliOptionParser.GetRequiredOption(options, "year")),
                CliOptionParser.ParseInt32(CliOptionParser.GetRequiredOption(options, "month")),
                CliOptionParser.ParseInt32(CliOptionParser.GetRequiredOption(options, "day")),
                CliOptionParser.ParseInt32(CliOptionParser.GetRequiredOption(options, "cluster-ordinal")),
                CliOptionParser.ParseOptionalUnsignedLong(CliOptionParser.GetOptionalOption(options, "start-address")),
                CliOptionParser.ParseOptionalUnsignedLong(CliOptionParser.GetOptionalOption(options, "stop-address")),
                CliOptionParser.ParseOptionalUnsignedLong(CliOptionParser.GetOptionalOption(options, "calendar-base-address")),
                CliOptionParser.ParseInt32List(CliOptionParser.GetOptionalOption(options, "preferred-strides")),
                CliOptionParser.ParseInt32(CliOptionParser.GetOptionalOption(options, "min-hit-count") ?? "3"),
                CliOptionParser.ParseInt32(CliOptionParser.GetOptionalOption(options, "cluster-gap") ?? "4096"),
                CliOptionParser.ParseInt32(CliOptionParser.GetOptionalOption(options, "preview-bytes") ?? "256"),
                CliOptionParser.ParseInt32(CliOptionParser.GetOptionalOption(options, "ints-before-hit") ?? "8"),
                CliOptionParser.ParseInt32(CliOptionParser.GetOptionalOption(options, "ints-after-hit") ?? "24")),
            "pes2021-classify-runtime-day-variant" => new Pes2021ClassifyRuntimeDayVariantCliCommand(
                CliOptionParser.ParseSelector(options),
                CliOptionParser.ParseInt32(CliOptionParser.GetRequiredOption(options, "year")),
                CliOptionParser.ParseInt32(CliOptionParser.GetRequiredOption(options, "month")),
                CliOptionParser.ParseInt32(CliOptionParser.GetRequiredOption(options, "day")),
                CliOptionParser.ParseOptionalUnsignedLong(CliOptionParser.GetOptionalOption(options, "start-address")),
                CliOptionParser.ParseOptionalUnsignedLong(CliOptionParser.GetOptionalOption(options, "stop-address")),
                CliOptionParser.ParseOptionalUnsignedLong(CliOptionParser.GetOptionalOption(options, "secondary-base-address")),
                CliOptionParser.ParseOptionalUnsignedLong(CliOptionParser.GetOptionalOption(options, "calendar-base-address")),
                CliOptionParser.ParseInt32List(CliOptionParser.GetOptionalOption(options, "preferred-strides")),
                CliOptionParser.ParseInt32(CliOptionParser.GetOptionalOption(options, "min-hit-count") ?? "3"),
                CliOptionParser.ParseInt32(CliOptionParser.GetOptionalOption(options, "cluster-gap") ?? "4096"),
                CliOptionParser.ParseInt32(CliOptionParser.GetOptionalOption(options, "preview-bytes") ?? "256")),
            "pes2021-find-fixture-anchor" => new Pes2021FindFixtureAnchorCliCommand(
                CliOptionParser.ParseSelector(options),
                CliOptionParser.ParseInt32(CliOptionParser.GetRequiredOption(options, "competition-id")),
                CliOptionParser.ParseInt32(CliOptionParser.GetRequiredOption(options, "team-id")),
                CliOptionParser.ParseOptionalInt32(CliOptionParser.GetOptionalOption(options, "team-liga")),
                CliOptionParser.GetOptionalOption(options, "profile-file"),
                CliOptionParser.ParseOptionalUnsignedLong(CliOptionParser.GetOptionalOption(options, "scan-start-address")),
                CliOptionParser.ParseOptionalUnsignedLong(CliOptionParser.GetOptionalOption(options, "scan-stop-address")),
                CliOptionParser.ParseOptionalInt32(CliOptionParser.GetOptionalOption(options, "block-records")),
                CliOptionParser.ParseOptionalUnsignedLong(CliOptionParser.GetOptionalOption(options, "max-scan-bytes")),
                CliOptionParser.GetOptionalOption(options, "output-file")),
            "pes2021-extract-competition-fixtures" => new Pes2021ExtractCompetitionFixturesCliCommand(
                CliOptionParser.ParseSelector(options),
                CliOptionParser.ParseInt32(CliOptionParser.GetRequiredOption(options, "competition-id")),
                CliOptionParser.ParseOptionalInt32(CliOptionParser.GetOptionalOption(options, "team-id")),
                CliOptionParser.ParseOptionalInt32(CliOptionParser.GetOptionalOption(options, "team-liga")),
                CliOptionParser.ParseOptionalUnsignedLong(CliOptionParser.GetOptionalOption(options, "calendar-base-address")),
                CliOptionParser.ParseOptionalUnsignedLong(CliOptionParser.GetOptionalOption(options, "competition-block-base-address")),
                CliOptionParser.ParseOptionalUnsignedLong(CliOptionParser.GetOptionalOption(options, "anchor-address")),
                CliOptionParser.GetOptionalOption(options, "profile-file"),
                CliOptionParser.GetOptionalOption(options, "competition-map-file"),
                CliOptionParser.GetOptionalOption(options, "team-map-file"),
                CliOptionParser.ParseOptionalInt32(CliOptionParser.GetOptionalOption(options, "block-records")),
                CliOptionParser.ParseOptionalInt32(CliOptionParser.GetOptionalOption(options, "record-limit")),
                CliOptionParser.GetOptionalOption(options, "output-file")),
            "pes2021-scan-club-relations" => new Pes2021ScanClubRelationsCliCommand(
                CliOptionParser.ParseSelector(options),
                CliOptionParser.GetRequiredOption(options, "team-catalog"),
                CliOptionParser.GetRequiredOption(options, "competition-map"),
                CliOptionParser.GetRequiredOption(options, "output"),
                CliOptionParser.ParseInt32(CliOptionParser.GetOptionalOption(options, "block-bytes") ?? "4194304"),
                CliOptionParser.ParseInt32(CliOptionParser.GetOptionalOption(options, "restart-timeout-seconds") ?? "180"),
                CliOptionParser.GetOptionalOption(options, "mode") ?? "baseline",
                CliOptionParser.GetOptionalOption(options, "input"),
                CliOptionParser.ParseInt32List(CliOptionParser.GetOptionalOption(options, "window-sizes") ?? "256,1024,4096")),
            "pes2021-find-player-anchor" => new Pes2021FindPlayerAnchorCliCommand(
                CliOptionParser.ParseSelector(options),
                CliOptionParser.ParseUInt32(CliOptionParser.GetRequiredOption(options, "control-player-id")),
                CliOptionParser.GetOptionalOption(options, "profile-file"),
                CliOptionParser.GetOptionalOption(options, "output-file")),
            "pes2021-scan-players" => new Pes2021ScanPlayersCliCommand(
                CliOptionParser.ParseSelector(options),
                CliOptionParser.ParseUInt32(CliOptionParser.GetRequiredOption(options, "control-player-id")),
                CliOptionParser.ParseOptionalUnsignedLong(CliOptionParser.GetOptionalOption(options, "anchor-address")),
                CliOptionParser.GetOptionalOption(options, "profile-file"),
                CliOptionParser.GetOptionalOption(options, "output-file")),
            "pes2021-query-player" => new Pes2021QueryPlayerCliCommand(
                CliOptionParser.ParseSelector(options),
                CliOptionParser.ParseUInt32(CliOptionParser.GetRequiredOption(options, "player-id")),
                CliOptionParser.GetOptionalOption(options, "profile-file")),
            "pes2021-export-player-catalog" => new Pes2021ExportPlayerCatalogCliCommand(
                CliOptionParser.ParseSelector(options),
                CliOptionParser.ParseUInt32(CliOptionParser.GetRequiredOption(options, "control-player-id")),
                CliOptionParser.GetOptionalOption(options, "profile-file"),
                CliOptionParser.GetRequiredOption(options, "output")),
            _ => null
        };
    }

    public Task<int>? TryExecute(CliCommand command, IServiceProvider services, TextWriter stdout, CancellationToken cancellationToken)
    {
        return command switch
        {
            Pes2021FindCalendarBaseCliCommand findCalendarBase => ExecuteWithAttachmentAsync(findCalendarBase.Selector, services.GetRequiredService<ProcessMemoryApplicationService>(), attachment =>
                services.GetRequiredService<Pes2021AgendaService>().FindBaseAsync(
                    attachment.AttachmentId,
                    findCalendarBase.Year,
                    findCalendarBase.Month,
                    findCalendarBase.Day,
                    findCalendarBase.RoundValue,
                    findCalendarBase.CompetitionCode,
                    findCalendarBase.ModuleName,
                    findCalendarBase.MaxResults,
                    cancellationToken), stdout, cancellationToken),
            Pes2021DumpCalendarDateCliCommand dumpCalendarDate => ExecuteWithAttachmentAsync(dumpCalendarDate.Selector, services.GetRequiredService<ProcessMemoryApplicationService>(), attachment =>
                services.GetRequiredService<Pes2021AgendaService>().DumpDateAsync(
                    attachment.AttachmentId,
                    dumpCalendarDate.Year,
                    dumpCalendarDate.Month,
                    dumpCalendarDate.Day,
                    dumpCalendarDate.BaseAddress,
                    dumpCalendarDate.MaxRecords,
                    cancellationToken), stdout, cancellationToken),
            Pes2021CompareCalendarDatesCliCommand compareCalendarDates => ExecuteWithAttachmentAsync(compareCalendarDates.Selector, services.GetRequiredService<ProcessMemoryApplicationService>(), attachment =>
                services.GetRequiredService<Pes2021AgendaService>().CompareDatesAsync(
                    attachment.AttachmentId,
                    compareCalendarDates.FirstYear,
                    compareCalendarDates.FirstMonth,
                    compareCalendarDates.FirstDay,
                    compareCalendarDates.SecondYear,
                    compareCalendarDates.SecondMonth,
                    compareCalendarDates.SecondDay,
                    compareCalendarDates.BaseAddress,
                    compareCalendarDates.MaxRecords,
                    cancellationToken), stdout, cancellationToken),
            Pes2021CalendarSummaryCliCommand calendarSummary => ExecuteWithAttachmentAsync(calendarSummary.Selector, services.GetRequiredService<ProcessMemoryApplicationService>(), attachment =>
                services.GetRequiredService<Pes2021AgendaService>().CalendarSummaryAsync(
                    attachment.AttachmentId,
                    calendarSummary.BaseAddress,
                    calendarSummary.MaxRecords,
                    cancellationToken), stdout, cancellationToken),
            Pes2021InventoryAnnualEventsCliCommand inventoryAnnualEvents => ExecuteWithAttachmentAsync(inventoryAnnualEvents.Selector, services.GetRequiredService<ProcessMemoryApplicationService>(), attachment =>
                services.GetRequiredService<Pes2021AgendaService>().InventoryAnnualEventsAsync(
                    attachment.AttachmentId,
                    inventoryAnnualEvents.Year,
                    inventoryAnnualEvents.CalendarBaseAddress,
                    inventoryAnnualEvents.SecondaryBaseAddress,
                    cancellationToken), stdout, cancellationToken),
            Pes2021FindSecondaryCalendarBaseByDateCliCommand findSecondaryBaseByDate => ExecuteWithAttachmentAsync(findSecondaryBaseByDate.Selector, services.GetRequiredService<ProcessMemoryApplicationService>(), attachment =>
                services.GetRequiredService<Pes2021AgendaService>().FindSecondaryBaseByDateAsync(
                    attachment.AttachmentId,
                    findSecondaryBaseByDate.Year,
                    findSecondaryBaseByDate.Month,
                    findSecondaryBaseByDate.Day,
                    findSecondaryBaseByDate.ModuleName,
                    findSecondaryBaseByDate.MaxResults,
                    cancellationToken), stdout, cancellationToken),
            Pes2021DumpSecondaryCalendarDayCliCommand dumpSecondaryCalendarDay => ExecuteWithAttachmentAsync(dumpSecondaryCalendarDay.Selector, services.GetRequiredService<ProcessMemoryApplicationService>(), attachment =>
                services.GetRequiredService<Pes2021AgendaService>().DumpSecondaryDayAsync(
                    attachment.AttachmentId,
                    dumpSecondaryCalendarDay.Year,
                    dumpSecondaryCalendarDay.Month,
                    dumpSecondaryCalendarDay.Day,
                    dumpSecondaryCalendarDay.BaseAddress,
                    cancellationToken), stdout, cancellationToken),
            Pes2021ScanRuntimeDayIndexClustersCliCommand scanRuntimeDayIndexClusters => ExecuteWithAttachmentAsync(scanRuntimeDayIndexClusters.Selector, services.GetRequiredService<ProcessMemoryApplicationService>(), attachment =>
                services.GetRequiredService<Pes2021AgendaService>().ScanRuntimeDayIndexClustersAsync(
                    attachment.AttachmentId,
                    scanRuntimeDayIndexClusters.Year,
                    scanRuntimeDayIndexClusters.Month,
                    scanRuntimeDayIndexClusters.Day,
                    scanRuntimeDayIndexClusters.MaxResults,
                    scanRuntimeDayIndexClusters.ClusterGap,
                    scanRuntimeDayIndexClusters.PreviewBytes,
                    cancellationToken), stdout, cancellationToken),
            Pes2021DumpRuntimeDayPayloadFamilyCliCommand dumpRuntimeDayPayloadFamily => ExecuteWithAttachmentAsync(dumpRuntimeDayPayloadFamily.Selector, services.GetRequiredService<ProcessMemoryApplicationService>(), attachment =>
                services.GetRequiredService<Pes2021AgendaService>().DumpRuntimeDayPayloadFamilyAsync(
                    attachment.AttachmentId,
                    dumpRuntimeDayPayloadFamily.Year,
                    dumpRuntimeDayPayloadFamily.Month,
                    dumpRuntimeDayPayloadFamily.Day,
                    dumpRuntimeDayPayloadFamily.StartAddress,
                    dumpRuntimeDayPayloadFamily.StopAddress,
                    dumpRuntimeDayPayloadFamily.CalendarBaseAddress,
                    dumpRuntimeDayPayloadFamily.PreferredStrides,
                    dumpRuntimeDayPayloadFamily.MinHitCount,
                    dumpRuntimeDayPayloadFamily.ClusterGap,
                    dumpRuntimeDayPayloadFamily.PreviewBytes,
                    cancellationToken), stdout, cancellationToken),
            Pes2021CompareRuntimeDayPayloadFamilyCliCommand compareRuntimeDayPayloadFamily => ExecuteWithAttachmentAsync(compareRuntimeDayPayloadFamily.Selector, services.GetRequiredService<ProcessMemoryApplicationService>(), attachment =>
                services.GetRequiredService<Pes2021AgendaService>().CompareRuntimeDayPayloadFamilyAsync(
                    attachment.AttachmentId,
                    compareRuntimeDayPayloadFamily.Year,
                    compareRuntimeDayPayloadFamily.Month,
                    compareRuntimeDayPayloadFamily.Day,
                    compareRuntimeDayPayloadFamily.StartAddress,
                    compareRuntimeDayPayloadFamily.StopAddress,
                    compareRuntimeDayPayloadFamily.CalendarBaseAddress,
                    compareRuntimeDayPayloadFamily.PreferredStrides,
                    compareRuntimeDayPayloadFamily.MinHitCount,
                    compareRuntimeDayPayloadFamily.ClusterGap,
                    compareRuntimeDayPayloadFamily.PreviewBytes,
                    cancellationToken), stdout, cancellationToken),
            Pes2021DumpRuntimeDayPayloadClusterDetailCliCommand dumpRuntimeDayPayloadClusterDetail => ExecuteWithAttachmentAsync(dumpRuntimeDayPayloadClusterDetail.Selector, services.GetRequiredService<ProcessMemoryApplicationService>(), attachment =>
                services.GetRequiredService<Pes2021AgendaService>().DumpRuntimeDayPayloadClusterDetailAsync(
                    attachment.AttachmentId,
                    dumpRuntimeDayPayloadClusterDetail.Year,
                    dumpRuntimeDayPayloadClusterDetail.Month,
                    dumpRuntimeDayPayloadClusterDetail.Day,
                    dumpRuntimeDayPayloadClusterDetail.ClusterOrdinal,
                    dumpRuntimeDayPayloadClusterDetail.StartAddress,
                    dumpRuntimeDayPayloadClusterDetail.StopAddress,
                    dumpRuntimeDayPayloadClusterDetail.CalendarBaseAddress,
                    dumpRuntimeDayPayloadClusterDetail.PreferredStrides,
                    dumpRuntimeDayPayloadClusterDetail.MinHitCount,
                    dumpRuntimeDayPayloadClusterDetail.ClusterGap,
                    dumpRuntimeDayPayloadClusterDetail.PreviewBytes,
                    dumpRuntimeDayPayloadClusterDetail.IntsBeforeHit,
                    dumpRuntimeDayPayloadClusterDetail.IntsAfterHit,
                    cancellationToken), stdout, cancellationToken),
            Pes2021AnalyzeRuntimeDayPayloadClusterCliCommand analyzeRuntimeDayPayloadCluster => ExecuteWithAttachmentAsync(analyzeRuntimeDayPayloadCluster.Selector, services.GetRequiredService<ProcessMemoryApplicationService>(), attachment =>
                services.GetRequiredService<Pes2021AgendaService>().AnalyzeRuntimeDayPayloadClusterAsync(
                    attachment.AttachmentId,
                    analyzeRuntimeDayPayloadCluster.Year,
                    analyzeRuntimeDayPayloadCluster.Month,
                    analyzeRuntimeDayPayloadCluster.Day,
                    analyzeRuntimeDayPayloadCluster.ClusterOrdinal,
                    analyzeRuntimeDayPayloadCluster.StartAddress,
                    analyzeRuntimeDayPayloadCluster.StopAddress,
                    analyzeRuntimeDayPayloadCluster.CalendarBaseAddress,
                    analyzeRuntimeDayPayloadCluster.PreferredStrides,
                    analyzeRuntimeDayPayloadCluster.MinHitCount,
                    analyzeRuntimeDayPayloadCluster.ClusterGap,
                    analyzeRuntimeDayPayloadCluster.PreviewBytes,
                    analyzeRuntimeDayPayloadCluster.IntsBeforeHit,
                    analyzeRuntimeDayPayloadCluster.IntsAfterHit,
                    cancellationToken), stdout, cancellationToken),
            Pes2021ClassifyRuntimeDayVariantCliCommand classifyRuntimeDayVariant => ExecuteWithAttachmentAsync(classifyRuntimeDayVariant.Selector, services.GetRequiredService<ProcessMemoryApplicationService>(), attachment =>
                services.GetRequiredService<Pes2021AgendaService>().ClassifyRuntimeDayVariantAsync(
                    attachment.AttachmentId,
                    classifyRuntimeDayVariant.Year,
                    classifyRuntimeDayVariant.Month,
                    classifyRuntimeDayVariant.Day,
                    classifyRuntimeDayVariant.StartAddress,
                    classifyRuntimeDayVariant.StopAddress,
                    classifyRuntimeDayVariant.SecondaryBaseAddress,
                    classifyRuntimeDayVariant.CalendarBaseAddress,
                    classifyRuntimeDayVariant.PreferredStrides,
                    classifyRuntimeDayVariant.MinHitCount,
                    classifyRuntimeDayVariant.ClusterGap,
                    classifyRuntimeDayVariant.PreviewBytes,
                    cancellationToken), stdout, cancellationToken),
            Pes2021FindFixtureAnchorCliCommand findFixtureAnchor => ExecuteFixtureAttachmentAsync(findFixtureAnchor.Selector, services.GetRequiredService<ProcessMemoryApplicationService>(), services.GetRequiredService<Pes2021CompetitionFixtureService>(), attachment =>
                services.GetRequiredService<Pes2021CompetitionFixtureService>().FindFixtureAnchorAsync(
                    attachment.AttachmentId,
                    BuildProcessIdentity(attachment),
                    LoadProfile(findFixtureAnchor.ProfilePath),
                    new CompetitionId((ushort)findFixtureAnchor.CompetitionId),
                    (ushort)findFixtureAnchor.TeamId,
                    findFixtureAnchor.TeamLiga.HasValue ? (ushort)findFixtureAnchor.TeamLiga.Value : (ushort?)null,
                    cancellationToken), stdout, findFixtureAnchor.OutputFile, cancellationToken),
            Pes2021ExtractCompetitionFixturesCliCommand extractCompetitionFixtures => ExecuteFixtureAttachmentAsync(extractCompetitionFixtures.Selector, services.GetRequiredService<ProcessMemoryApplicationService>(), services.GetRequiredService<Pes2021CompetitionFixtureService>(), attachment =>
                services.GetRequiredService<Pes2021CompetitionFixtureService>().ExtractCompetitionFixturesAsync(
                    attachment.AttachmentId,
                    BuildProcessIdentity(attachment),
                    new CompetitionFixtureExtractionRequest(
                        CompetitionId: new CompetitionId((ushort)extractCompetitionFixtures.CompetitionId),
                        TeamId: extractCompetitionFixtures.TeamId.HasValue ? (ushort)extractCompetitionFixtures.TeamId.Value : (ushort?)null,
                        TeamLiga: extractCompetitionFixtures.TeamLiga.HasValue ? (ushort)extractCompetitionFixtures.TeamLiga.Value : (ushort?)null,
                        CalendarArrayBaseAddress: extractCompetitionFixtures.CalendarBaseAddress,
                        CompetitionBlockBaseAddress: extractCompetitionFixtures.CompetitionBlockBaseAddress,
                        AnchorAddress: extractCompetitionFixtures.AnchorAddress,
                        ProfilePath: extractCompetitionFixtures.ProfileFile,
                        CompetitionMapPath: extractCompetitionFixtures.CompetitionMapFile,
                        TeamMapPath: extractCompetitionFixtures.TeamMapFile,
                        BlockRecords: extractCompetitionFixtures.BlockRecords,
                        RecordLimit: extractCompetitionFixtures.RecordLimit),
                    cancellationToken), stdout, extractCompetitionFixtures.OutputFile, cancellationToken),
            Pes2021ScanClubRelationsCliCommand scanClubRelations => ExecuteWithAttachmentAsync(scanClubRelations.Selector, services.GetRequiredService<ProcessMemoryApplicationService>(), async attachment =>
                await DispatchClubRelationsAsync(services, attachment, scanClubRelations, cancellationToken), stdout, cancellationToken),
            Pes2021FindPlayerAnchorCliCommand findPlayerAnchor => ExecutePlayerAttachmentAsync(findPlayerAnchor.Selector, services.GetRequiredService<ProcessMemoryApplicationService>(), services.GetRequiredService<Pes2021PlayerCatalogService>(), attachment =>
                services.GetRequiredService<Pes2021PlayerAnchorFinder>().FindAsync(
                    attachment.AttachmentId,
                    BuildProcessIdentity(attachment),
                    LoadPlayerProfile(findPlayerAnchor.ProfileFile),
                    findPlayerAnchor.ControlPlayerId,
                    regions: null,
                    cancellationToken), stdout, findPlayerAnchor.OutputFile, cancellationToken),
            Pes2021ScanPlayersCliCommand scanPlayers => ExecutePlayerAttachmentAsync(scanPlayers.Selector, services.GetRequiredService<ProcessMemoryApplicationService>(), services.GetRequiredService<Pes2021PlayerCatalogService>(), attachment =>
                services.GetRequiredService<Pes2021PlayerCatalogService>().RefreshAsync(
                    attachment.AttachmentId,
                    BuildProcessIdentity(attachment),
                    LoadPlayerProfile(scanPlayers.ProfileFile),
                    scanPlayers.ControlPlayerId,
                    scanPlayers.AnchorAddress,
                    regions: null,
                    cancellationToken), stdout, scanPlayers.OutputFile, cancellationToken),
            Pes2021QueryPlayerCliCommand queryPlayer => ExecutePlayerAttachmentAsync(queryPlayer.Selector, services.GetRequiredService<ProcessMemoryApplicationService>(), services.GetRequiredService<Pes2021PlayerCatalogService>(), async attachment =>
            {
                await services.GetRequiredService<Pes2021PlayerCatalogService>().RefreshAsync(
                    attachment.AttachmentId,
                    BuildProcessIdentity(attachment),
                    LoadPlayerProfile(queryPlayer.ProfileFile),
                    queryPlayer.PlayerId,
                    regions: null,
                    cancellationToken);
                return services.GetRequiredService<Pes2021PlayerQueryService>().QueryByPlayerId(queryPlayer.PlayerId);
            }, stdout, outputFile: null, cancellationToken),
            Pes2021ExportPlayerCatalogCliCommand exportCatalog => ExecutePlayerAttachmentAsync(exportCatalog.Selector, services.GetRequiredService<ProcessMemoryApplicationService>(), services.GetRequiredService<Pes2021PlayerCatalogService>(), async attachment =>
            {
                var discovery = await services.GetRequiredService<Pes2021PlayerCatalogService>().RefreshAsync(
                    attachment.AttachmentId,
                    BuildProcessIdentity(attachment),
                    LoadPlayerProfile(exportCatalog.ProfileFile),
                    exportCatalog.ControlPlayerId,
                    regions: null,
                    cancellationToken);
                var export = Pes2021PlayerCatalogExporter.Build(discovery);
                Pes2021AtomicFileWriter.WriteJson(exportCatalog.OutputFile, export, JsonOptions);
                return export;
            }, stdout, exportCatalog.OutputFile, cancellationToken),
            _ => null
        };
    }

    private static async Task<Pes2021ClubScanResult> DispatchClubRelationsAsync(
        IServiceProvider services,
        AttachmentInfo attachment,
        Pes2021ScanClubRelationsCliCommand command,
        CancellationToken cancellationToken)
    {
        var service = services.GetRequiredService<Pes2021ClubRelationsService>();
        var mode = (command.Mode ?? "baseline").Trim().ToLowerInvariant();
        if (mode == "layout")
        {
            return await service.ExecuteLayoutAsync(
                attachment.AttachmentId,
                attachment,
                command.TeamCatalogPath,
                command.CompetitionMapPath,
                command.OutputDirectory,
                command.InputObservationsPath,
                command.WindowSizes,
                command.RestartTimeoutSeconds,
                cancellationToken);
        }

        if (mode != "baseline")
        {
            throw new ArgumentException($"Unknown mode '{command.Mode}'. Use 'baseline' or 'layout'.");
        }

        return await service.ExecuteAsync(
            attachment.AttachmentId,
            attachment,
            command.TeamCatalogPath,
            command.CompetitionMapPath,
            command.OutputDirectory,
            command.BlockBytes,
            command.RestartTimeoutSeconds,
            cancellationToken);
    }

    public IReadOnlyList<string> GetHelpLines()
    {
        return [
            "  pes2021-find-calendar-base --pid <id>|--name <process> --competition-code <code> [--year <yyyy>] [--month <mm>] [--day <dd>] [--round <value>] [--module-name <module>] [--max-results <count>]",
            "  pes2021-dump-calendar-date --pid <id>|--name <process> --year <yyyy> --month <mm> --day <dd> [--base-address <value>] [--max-records <count>]",
            "  pes2021-compare-calendar-dates --pid <id>|--name <process> --first-year <yyyy> --first-month <mm> --first-day <dd> --second-year <yyyy> --second-month <mm> --second-day <dd> [--base-address <value>] [--max-records <count>]",
            "  pes2021-calendar-summary --pid <id>|--name <process> [--base-address <value>] [--max-records <count>]",
            "  pes2021-inventory-annual-events --pid <id>|--name <process> --year <yyyy> [--calendar-base-address <value>] [--secondary-base-address <value>]",
            "  pes2021-find-secondary-calendar-base-by-date --pid <id>|--name <process> --year <yyyy> --month <mm> --day <dd> [--module-name <module>] [--max-results <count>]",
            "  pes2021-dump-secondary-calendar-day --pid <id>|--name <process> --year <yyyy> --month <mm> --day <dd> [--base-address <value>]",
            "  pes2021-scan-runtime-day-index-clusters --pid <id>|--name <process> --year <yyyy> --month <mm> --day <dd> [--max-results <count>] [--cluster-gap <bytes>] [--preview-bytes <bytes>]",
            "  pes2021-dump-runtime-day-payload-family --pid <id>|--name <process> --year <yyyy> --month <mm> --day <dd> [--start-address <value>] [--stop-address <value>] [--calendar-base-address <value>] [--preferred-strides <s1,s2,...>] [--min-hit-count <count>] [--cluster-gap <bytes>] [--preview-bytes <bytes>]",
            "  pes2021-compare-runtime-day-payload-family --pid <id>|--name <process> --year <yyyy> --month <mm> --day <dd> [--start-address <value>] [--stop-address <value>] [--calendar-base-address <value>] [--preferred-strides <s1,s2,...>] [--min-hit-count <count>] [--cluster-gap <bytes>] [--preview-bytes <bytes>]",
            "  pes2021-dump-runtime-day-payload-cluster-detail --pid <id>|--name <process> --year <yyyy> --month <mm> --day <dd> --cluster-ordinal <index> [--start-address <value>] [--stop-address <value>] [--calendar-base-address <value>] [--preferred-strides <s1,s2,...>] [--min-hit-count <count>] [--cluster-gap <bytes>] [--preview-bytes <bytes>] [--ints-before-hit <count>] [--ints-after-hit <count>]",
            "  pes2021-analyze-runtime-day-payload-cluster --pid <id>|--name <process> --year <yyyy> --month <mm> --day <dd> --cluster-ordinal <index> [--start-address <value>] [--stop-address <value>] [--calendar-base-address <value>] [--preferred-strides <s1,s2,...>] [--min-hit-count <count>] [--cluster-gap <bytes>] [--preview-bytes <bytes>] [--ints-before-hit <count>] [--ints-after-hit <count>]",
            "  pes2021-classify-runtime-day-variant --pid <id>|--name <process> --year <yyyy> --month <mm> --day <dd> [--start-address <value>] [--stop-address <value>] [--secondary-base-address <value>] [--calendar-base-address <value>] [--preferred-strides <s1,s2,...>] [--min-hit-count <count>] [--cluster-gap <bytes>] [--preview-bytes <bytes>]",
            "  pes2021-find-fixture-anchor --pid <id>|--name <process> --competition-id <id> --team-id <id> [--team-liga <id>] [--profile-file <path>] [--scan-start-address <value>] [--scan-stop-address <value>] [--block-records <count>] [--max-scan-bytes <bytes>] [--output-file <path>]",
            "  pes2021-extract-competition-fixtures --pid <id>|--name <process> --competition-id <id> [--team-id <id>] [--team-liga <id>] [--calendar-base-address <value>] [--competition-block-base-address <value>] [--anchor-address <value>] [--profile-file <path>] [--competition-map-file <path>] [--team-map-file <path>] [--block-records <count>] [--record-limit <count>] [--output-file <path>]",
            "  pes2021-scan-club-relations --pid <id>|--name <process> --team-catalog <path> --competition-map <path> --output <dir> [--mode baseline|layout] [--block-bytes <bytes>] [--restart-timeout-seconds <seconds>] [--input <observations.csv>] [--window-sizes <s1,s2,...>]",
            "  pes2021-find-player-anchor --pid <id>|--name <process> --control-player-id <id> [--profile-file <path>] [--output-file <path>]",
            "  pes2021-scan-players --pid <id>|--name <process> --control-player-id <id> [--anchor-address <validated-candidate>] [--profile-file <path>] [--output-file <path>]",
            "  pes2021-query-player --pid <id>|--name <process> --player-id <id> [--profile-file <path>]",
            "  pes2021-export-player-catalog --pid <id>|--name <process> --control-player-id <id> --output <path> [--profile-file <path>]"
        ];
    }

    private static readonly JsonSerializerOptions JsonOptions = Pes2021FixtureJson.Options;

    private static async Task<int> ExecuteWithAttachmentAsync<T>(
        ProcessSelector selector,
        ProcessMemoryApplicationService applicationService,
        Func<AttachmentInfo, Task<T>> action,
        TextWriter stdout,
        CancellationToken cancellationToken)
    {
        var attachment = await applicationService.AttachAsync(selector, cancellationToken);
        try
        {
            var result = await action(attachment);
            await stdout.WriteLineAsync(JsonSerializer.Serialize(result, JsonOptions));
            return 0;
        }
        finally
        {
            await applicationService.DetachAsync(attachment.AttachmentId, cancellationToken);
        }
    }

    private static async Task<int> ExecuteFixtureAttachmentAsync<T>(
        ProcessSelector selector,
        ProcessMemoryApplicationService applicationService,
        Pes2021CompetitionFixtureService fixtureService,
        Func<AttachmentInfo, Task<T>> action,
        TextWriter stdout,
        string? outputFile,
        CancellationToken cancellationToken)
    {
        var attachment = await applicationService.AttachAsync(selector, cancellationToken);
        try
        {
            var result = await action(attachment);
            if (string.IsNullOrWhiteSpace(outputFile))
            {
                await stdout.WriteLineAsync(JsonSerializer.Serialize(result, JsonOptions));
            }
            else
            {
                Pes2021AtomicFileWriter.WriteJson(outputFile, result, JsonOptions);
            }

            return 0;
        }
        finally
        {
            await applicationService.DetachAsync(attachment.AttachmentId, cancellationToken);
            fixtureService.InvalidateAttachment(attachment.AttachmentId);
        }
    }

    private static ProcessInstanceIdentity BuildProcessIdentity(AttachmentInfo attachment)
        => new(
            AttachmentId: attachment.AttachmentId,
            ProcessId: attachment.ProcessId,
            ProcessStartedAtUtc: attachment.ProcessStartedAtUtc,
            ProcessName: attachment.ProcessName);

    private static Pes2021FixtureProfile LoadProfile(string? profilePath)
    {
        if (!string.IsNullOrWhiteSpace(profilePath))
        {
            return Pes2021FixtureProfileLoader.LoadFromFile(profilePath);
        }

        return Pes2021FixtureProfileDefaults.GetOrLoad();
    }

    private static Pes2021PlayerProfile LoadPlayerProfile(string? profilePath)
    {
        if (!string.IsNullOrWhiteSpace(profilePath))
        {
            return Pes2021PlayerProfileLoader.LoadFromFile(profilePath);
        }

        return Pes2021PlayerProfileDefaults.GetOrLoad();
    }

    private static async Task<int> ExecutePlayerAttachmentAsync<T>(
        ProcessSelector selector,
        ProcessMemoryApplicationService applicationService,
        Pes2021PlayerCatalogService catalogService,
        Func<AttachmentInfo, Task<T>> action,
        TextWriter stdout,
        string? outputFile,
        CancellationToken cancellationToken)
    {
        var attachment = await applicationService.AttachAsync(selector, cancellationToken);
        try
        {
            var result = await action(attachment);
            if (string.IsNullOrWhiteSpace(outputFile))
            {
                await stdout.WriteLineAsync(JsonSerializer.Serialize(result, JsonOptions));
            }
            else
            {
                Pes2021AtomicFileWriter.WriteJson(outputFile, result, JsonOptions);
            }

            return 0;
        }
        finally
        {
            await applicationService.DetachAsync(attachment.AttachmentId, cancellationToken);
            catalogService.Catalog.Clear();
        }
    }

    private static async Task<int> ExecuteStrideScanAsync(
        ProcessSelector selector,
        ProcessMemoryApplicationService applicationService,
        Pes2021PlayerProfile profile,
        ulong startAddress,
        ulong stopAddress,
        int stride,
        int maxRecords,
        TextWriter stdout,
        CancellationToken cancellationToken)
    {
        var attachment = await applicationService.AttachAsync(selector, cancellationToken);
        try
        {
            var records = new List<object>();
            var slot = 0;
            for (var addr = startAddress; addr < stopAddress && records.Count < maxRecords; addr += (ulong)stride, slot++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var req = new ReadMemoryRequest(attachment.AttachmentId, addr, MemoryValueKind.Bytes, profile.Stride);
                ReadMemoryResult resp;
                try
                {
                    resp = await applicationService.ReadAsync(req, cancellationToken);
                }
                catch (Exception)
                {
                    continue;
                }

                if (resp.BytesRead != profile.Stride) continue;

                byte[] slice;
                try { slice = Convert.FromHexString(resp.Value); }
                catch { continue; }

                var height = slice[0];
                var weight = slice[1];
                if (height < 140 || height > 220 || weight < 40 || weight > 130) continue;

                var playerId = System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(slice.AsSpan(0x30, 4));
                if (playerId < 1 || playerId > 2_000_000) continue;

                var nameStart = 0x38;
                var nameBytes = slice.AsSpan(nameStart, 61);
                var nullAt = nameBytes.IndexOf((byte)0);
                string? name = null;
                if (nullAt > 0)
                {
                    var nameSlice = nameBytes.Slice(0, nullAt);
                    var isUtf8 = true;
                    for (var ni = 0; ni < nameSlice.Length; ni++)
                    {
                        var b = nameSlice[ni];
                        if (b < 0x20 || b == 0x7F) { isUtf8 = false; break; }
                        if (b >= 0xC2 && b <= 0xDF)
                        {
                            if (ni + 1 >= nameSlice.Length) { isUtf8 = false; break; }
                            if ((nameSlice[ni + 1] & 0xC0) != 0x80) { isUtf8 = false; break; }
                            ni++;
                        }
                        else if (b >= 0xE0 && b <= 0xEF)
                        {
                            if (ni + 2 >= nameSlice.Length) { isUtf8 = false; break; }
                            if ((nameSlice[ni + 1] & 0xC0) != 0x80) { isUtf8 = false; break; }
                            if ((nameSlice[ni + 2] & 0xC0) != 0x80) { isUtf8 = false; break; }
                            ni += 2;
                        }
                        else if (b >= 0x80)
                        {
                            isUtf8 = false;
                            break;
                        }
                    }
                    if (isUtf8)
                    {
                        var bytes = nameSlice.ToArray();
                        name = System.Text.Encoding.UTF8.GetString(bytes);
                    }
                    else
                    {
                        name = System.Text.Encoding.ASCII.GetString(nameSlice);
                    }
                }
                if (name is null || name.Trim().Length == 0) continue;

                records.Add(new
                {
                    slot,
                    address = $"0x{addr:X}",
                    playerId,
                    height,
                    weight,
                    name,
                });
            }

            var summary = new
            {
                schemaVersion = "pes2021.player-memory.live.v1",
                processId = attachment.ProcessId,
                processName = attachment.ProcessName,
                startAddress = $"0x{startAddress:X}",
                stopAddress = $"0x{stopAddress:X}",
                stride,
                recordsDecoded = records.Count,
                records,
            };

            await stdout.WriteLineAsync(JsonSerializer.Serialize(summary, JsonOptions));
            return 0;
        }
        finally
        {
            await applicationService.DetachAsync(attachment.AttachmentId, cancellationToken);
        }
    }

    private static async Task<int> ExecuteScanAllArenasAsync(
        ProcessSelector selector,
        ProcessMemoryApplicationService applicationService,
        Pes2021PlayerProfile profile,
        int stride,
        int maxRecordsPerArena,
        ulong minRegionSize,
        TextWriter stdout,
        CancellationToken cancellationToken)
    {
        var attachment = await applicationService.AttachAsync(selector, cancellationToken);
        try
        {
            var regions = await applicationService.ListRegionsAsync(attachment.AttachmentId, cancellationToken);
            var candidates = regions
                .Where(r => string.Equals(r.State, "Commit", StringComparison.OrdinalIgnoreCase)
                    && string.Equals(r.Type, "Private", StringComparison.OrdinalIgnoreCase)
                    && r.IsReadable && r.IsWritable
                    && r.RegionSize >= minRegionSize)
                .OrderByDescending(r => r.RegionSize)
                .ToList();

            var arenaSummaries = new List<object>();
            var allRecords = new List<object>();
            var globalSlot = 0;

            foreach (var region in candidates)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var regionRecords = new List<object>();
                var regionSlot = 0;
                var start = region.BaseAddress;
                var stop = checked(start + region.RegionSize);
                for (var addr = start; addr < stop && regionRecords.Count < maxRecordsPerArena; addr += (ulong)stride, regionSlot++)
                {
                    ReadMemoryResult resp;
                    try
                    {
                        resp = await applicationService.ReadAsync(
                            new ReadMemoryRequest(attachment.AttachmentId, addr, MemoryValueKind.Bytes, profile.Stride),
                            cancellationToken);
                    }
                    catch
                    {
                        continue;
                    }

                    if (resp.BytesRead != profile.Stride) continue;
                    byte[] slice;
                    try { slice = Convert.FromHexString(resp.Value); }
                    catch { continue; }

                    var height = slice[0];
                    var weight = slice[1];
                    if (height < 140 || height > 220 || weight < 40 || weight > 130) continue;

                    var playerId = System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(slice.AsSpan(0x30, 4));
                    if (playerId < 1 || playerId > 2_000_000) continue;

                    var nameBytes = slice.AsSpan(0x38, 61);
                    var nullAt = nameBytes.IndexOf((byte)0);
                    string? name = null;
                    if (nullAt > 0)
                    {
                        var nameSlice = nameBytes.Slice(0, nullAt);
                        var isUtf8 = true;
                        for (var ni = 0; ni < nameSlice.Length; ni++)
                        {
                            var b = nameSlice[ni];
                            if (b < 0x20 || b == 0x7F) { isUtf8 = false; break; }
                            if (b >= 0xC2 && b <= 0xDF)
                            {
                                if (ni + 1 >= nameSlice.Length) { isUtf8 = false; break; }
                                if ((nameSlice[ni + 1] & 0xC0) != 0x80) { isUtf8 = false; break; }
                                ni++;
                            }
                            else if (b >= 0xE0 && b <= 0xEF)
                            {
                                if (ni + 2 >= nameSlice.Length) { isUtf8 = false; break; }
                                if ((nameSlice[ni + 1] & 0xC0) != 0x80) { isUtf8 = false; break; }
                                if ((nameSlice[ni + 2] & 0xC0) != 0x80) { isUtf8 = false; break; }
                                ni += 2;
                            }
                            else if (b >= 0x80)
                            {
                                isUtf8 = false;
                                break;
                            }
                        }
                        var nameByteArr = nameSlice.ToArray();
                        name = isUtf8 ? System.Text.Encoding.UTF8.GetString(nameByteArr) : System.Text.Encoding.ASCII.GetString(nameByteArr);
                    }
                    if (name is null || name.Trim().Length == 0) continue;

                    var record = new
                    {
                        globalSlot,
                        regionSlot,
                        regionIndex = arenaSummaries.Count,
                        address = $"0x{addr:X}",
                        playerId,
                        height,
                        weight,
                        name,
                    };
                    regionRecords.Add(record);
                    allRecords.Add(record);
                    globalSlot++;
                }

                if (regionRecords.Count > 0)
                {
                    arenaSummaries.Add(new
                    {
                        regionIndex = arenaSummaries.Count,
                        baseAddress = $"0x{region.BaseAddress:X}",
                        stopAddress = $"0x{stop:X}",
                        sizeBytes = region.RegionSize,
                        protection = region.Protection,
                        recordsFound = regionRecords.Count,
                        firstRecord = regionRecords[0],
                        lastRecord = regionRecords[^1],
                    });
                }
            }

            var summary = new
            {
                schemaVersion = "pes2021.player-memory.live.v2",
                processId = attachment.ProcessId,
                processName = attachment.ProcessName,
                stride,
                maxRecordsPerArena,
                minRegionSize,
                arenasScanned = candidates.Count,
                arenasWithPlayers = arenaSummaries.Count,
                totalRecords = allRecords.Count,
                arenas = arenaSummaries,
                records = allRecords,
            };

            await stdout.WriteLineAsync(JsonSerializer.Serialize(summary, JsonOptions));
            return 0;
        }
        finally
        {
            await applicationService.DetachAsync(attachment.AttachmentId, cancellationToken);
        }
    }
}
