using Microsoft.Extensions.DependencyInjection;
using Overmem.Abstractions.Cli;
using Overmem.Abstractions.Processes;
using Overmem.Application;
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
            _ => null
        };
    }

    public IReadOnlyList<string> GetHelpLines()
    {
        return [
            "  pes2021-find-calendar-base --pid <id>|--name <process> [--year <yyyy>] [--month <mm>] [--day <dd>] [--round <value>] [--competition-code <code>] [--module-name <module>] [--max-results <count>]",
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
            "  pes2021-classify-runtime-day-variant --pid <id>|--name <process> --year <yyyy> --month <mm> --day <dd> [--start-address <value>] [--stop-address <value>] [--secondary-base-address <value>] [--calendar-base-address <value>] [--preferred-strides <s1,s2,...>] [--min-hit-count <count>] [--cluster-gap <bytes>] [--preview-bytes <bytes>]"
        ];
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

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
}
