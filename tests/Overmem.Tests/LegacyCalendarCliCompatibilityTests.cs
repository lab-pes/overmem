using Overmem.Abstractions.Cli;
using Overmem.Cli;
using Overmem.Extensions.Pes2021.Cli;

namespace Overmem.Tests;

// Recovered from legacy-dgit; assertions retained through the extension migration.
public sealed class LegacyCalendarCliCompatibilityTests
{
    private static CliCommand ParseWithExtension(string[] args)
        => CliArgumentParser.Parse(args, [new Pes2021CliExtension()]);

    [Fact]
    public void ParsePes2021FindCalendarBaseCommand_ParsesOptionalFilters()
    {
        var command = ParseWithExtension([
            "pes2021-find-calendar-base",
            "--pid", "77",
            "--year", "2026",
            "--month", "2",
            "--day", "1",
            "--round", "1",
            "--competition-code", "29",
            "--module-name", "demo.exe",
            "--max-results", "25"
        ]);

        var find = Assert.IsType<Pes2021FindCalendarBaseCliCommand>(command);
        Assert.Equal(77, find.Selector.ProcessId);
        Assert.Equal(2026, find.Year);
        Assert.Equal(2, find.Month);
        Assert.Equal(1, find.Day);
        Assert.Equal(1, find.RoundValue);
        Assert.Equal(29, find.CompetitionCode);
        Assert.Equal("demo.exe", find.ModuleName);
        Assert.Equal(25, find.MaxResults);
    }

    [Fact]
    public void ParsePes2021DumpCalendarDateCommand_ParsesBaseAddress()
    {
        var command = ParseWithExtension([
            "pes2021-dump-calendar-date",
            "--name", "PES2021",
            "--year", "2026",
            "--month", "2",
            "--day", "1",
            "--base-address", "0x1234",
            "--max-records", "500"
        ]);

        var dump = Assert.IsType<Pes2021DumpCalendarDateCliCommand>(command);
        Assert.Equal("PES2021", dump.Selector.ProcessName);
        Assert.Equal(2026, dump.Year);
        Assert.Equal(2, dump.Month);
        Assert.Equal(1, dump.Day);
        Assert.Equal(0x1234UL, dump.BaseAddress);
        Assert.Equal(500, dump.MaxRecords);
    }

    [Fact]
    public void ParsePes2021CompareCalendarDatesCommand_ParsesTwoDates()
    {
        var command = ParseWithExtension([
            "pes2021-compare-calendar-dates",
            "--pid", "26368",
            "--first-year", "2026",
            "--first-month", "1",
            "--first-day", "24",
            "--second-year", "2026",
            "--second-month", "2",
            "--second-day", "1",
            "--base-address", "0x1234",
            "--max-records", "901"
        ]);

        var compare = Assert.IsType<Pes2021CompareCalendarDatesCliCommand>(command);
        Assert.Equal(26368, compare.Selector.ProcessId);
        Assert.Equal(2026, compare.FirstYear);
        Assert.Equal(1, compare.FirstMonth);
        Assert.Equal(24, compare.FirstDay);
        Assert.Equal(2026, compare.SecondYear);
        Assert.Equal(2, compare.SecondMonth);
        Assert.Equal(1, compare.SecondDay);
        Assert.Equal(0x1234UL, compare.BaseAddress);
        Assert.Equal(901, compare.MaxRecords);
    }

    [Fact]
    public void ParsePes2021CalendarSummaryCommand_ParsesBaseAddress()
    {
        var command = ParseWithExtension([
            "pes2021-calendar-summary",
            "--pid", "26368",
            "--base-address", "0x1234",
            "--max-records", "901"
        ]);

        var summary = Assert.IsType<Pes2021CalendarSummaryCliCommand>(command);
        Assert.Equal(26368, summary.Selector.ProcessId);
        Assert.Equal(0x1234UL, summary.BaseAddress);
        Assert.Equal(901, summary.MaxRecords);
    }

    [Fact]
    public void ParsePes2021InventoryAnnualEventsCommand_ParsesBaseAddresses()
    {
        var command = ParseWithExtension([
            "pes2021-inventory-annual-events",
            "--pid", "26368",
            "--year", "2026",
            "--calendar-base-address", "0x1234",
            "--secondary-base-address", "0x5678"
        ]);

        var inventory = Assert.IsType<Pes2021InventoryAnnualEventsCliCommand>(command);
        Assert.Equal(26368, inventory.Selector.ProcessId);
        Assert.Equal(2026, inventory.Year);
        Assert.Equal(0x1234UL, inventory.CalendarBaseAddress);
        Assert.Equal(0x5678UL, inventory.SecondaryBaseAddress);
    }

    [Fact]
    public void ParsePes2021FindSecondaryCalendarBaseByDateCommand_ParsesDateAndModule()
    {
        var command = ParseWithExtension([
            "pes2021-find-secondary-calendar-base-by-date",
            "--name", "PES2021",
            "--year", "2026",
            "--month", "3",
            "--day", "28",
            "--module-name", "PES2021.exe",
            "--max-results", "512"
        ]);

        var find = Assert.IsType<Pes2021FindSecondaryCalendarBaseByDateCliCommand>(command);
        Assert.Equal("PES2021", find.Selector.ProcessName);
        Assert.Equal(2026, find.Year);
        Assert.Equal(3, find.Month);
        Assert.Equal(28, find.Day);
        Assert.Equal("PES2021.exe", find.ModuleName);
        Assert.Equal(512, find.MaxResults);
    }

    [Fact]
    public void ParsePes2021DumpSecondaryCalendarDayCommand_ParsesOptionalBaseAddress()
    {
        var command = ParseWithExtension([
            "pes2021-dump-secondary-calendar-day",
            "--pid", "26368",
            "--year", "2026",
            "--month", "3",
            "--day", "29",
            "--base-address", "0x9876"
        ]);

        var dump = Assert.IsType<Pes2021DumpSecondaryCalendarDayCliCommand>(command);
        Assert.Equal(26368, dump.Selector.ProcessId);
        Assert.Equal(2026, dump.Year);
        Assert.Equal(3, dump.Month);
        Assert.Equal(29, dump.Day);
        Assert.Equal(0x9876UL, dump.BaseAddress);
    }

    [Fact]
    public void ParsePes2021ScanRuntimeDayIndexClustersCommand_ParsesHexOptions()
    {
        var command = ParseWithExtension([
            "pes2021-scan-runtime-day-index-clusters",
            "--name", "PES2021",
            "--year", "2026",
            "--month", "3",
            "--day", "28",
            "--max-results", "0x200",
            "--cluster-gap", "0x800",
            "--preview-bytes", "0x80"
        ]);

        var scan = Assert.IsType<Pes2021ScanRuntimeDayIndexClustersCliCommand>(command);
        Assert.Equal("PES2021", scan.Selector.ProcessName);
        Assert.Equal(2026, scan.Year);
        Assert.Equal(3, scan.Month);
        Assert.Equal(28, scan.Day);
        Assert.Equal(0x200, scan.MaxResults);
        Assert.Equal(0x800, scan.ClusterGap);
        Assert.Equal(0x80, scan.PreviewBytes);
    }

    [Fact]
    public void ParsePes2021DumpRuntimeDayPayloadFamilyCommand_ParsesOptionalArguments()
    {
        var command = ParseWithExtension([
            "pes2021-dump-runtime-day-payload-family",
            "--pid", "26368",
            "--year", "2026",
            "--month", "3",
            "--day", "29",
            "--start-address", "0x1000",
            "--stop-address", "0x2000",
            "--calendar-base-address", "0x3000",
            "--preferred-strides", "472,0x210",
            "--min-hit-count", "5",
            "--cluster-gap", "0x900",
            "--preview-bytes", "0x40"
        ]);

        var dump = Assert.IsType<Pes2021DumpRuntimeDayPayloadFamilyCliCommand>(command);
        Assert.Equal(26368, dump.Selector.ProcessId);
        Assert.Equal(2026, dump.Year);
        Assert.Equal(3, dump.Month);
        Assert.Equal(29, dump.Day);
        Assert.Equal(0x1000UL, dump.StartAddress);
        Assert.Equal(0x2000UL, dump.StopAddress);
        Assert.Equal(0x3000UL, dump.CalendarBaseAddress);
        Assert.Equal([472, 0x210], dump.PreferredStrides);
        Assert.Equal(5, dump.MinHitCount);
        Assert.Equal(0x900, dump.ClusterGap);
        Assert.Equal(0x40, dump.PreviewBytes);
    }

    [Fact]
    public void ParsePes2021CompareRuntimeDayPayloadFamilyCommand_ParsesOptionalArguments()
    {
        var command = ParseWithExtension([
            "pes2021-compare-runtime-day-payload-family",
            "--pid", "26368",
            "--year", "2026",
            "--month", "3",
            "--day", "29",
            "--start-address", "0x1000",
            "--stop-address", "0x2000",
            "--calendar-base-address", "0x3000",
            "--preferred-strides", "472,0x210",
            "--min-hit-count", "5",
            "--cluster-gap", "0x900",
            "--preview-bytes", "0x40"
        ]);

        var compare = Assert.IsType<Pes2021CompareRuntimeDayPayloadFamilyCliCommand>(command);
        Assert.Equal(26368, compare.Selector.ProcessId);
        Assert.Equal(2026, compare.Year);
        Assert.Equal(3, compare.Month);
        Assert.Equal(29, compare.Day);
        Assert.Equal(0x1000UL, compare.StartAddress);
        Assert.Equal(0x2000UL, compare.StopAddress);
        Assert.Equal(0x3000UL, compare.CalendarBaseAddress);
        Assert.Equal([472, 0x210], compare.PreferredStrides);
        Assert.Equal(5, compare.MinHitCount);
        Assert.Equal(0x900, compare.ClusterGap);
        Assert.Equal(0x40, compare.PreviewBytes);
    }

    [Fact]
    public void ParsePes2021DumpRuntimeDayPayloadClusterDetailCommand_ParsesClusterOptions()
    {
        var command = ParseWithExtension([
            "pes2021-dump-runtime-day-payload-cluster-detail",
            "--name", "PES2021",
            "--year", "2026",
            "--month", "3",
            "--day", "28",
            "--cluster-ordinal", "2",
            "--start-address", "0x1000",
            "--stop-address", "0x2000",
            "--calendar-base-address", "0x3000",
            "--preferred-strides", "472,0x210",
            "--min-hit-count", "5",
            "--cluster-gap", "0x900",
            "--preview-bytes", "0x40",
            "--ints-before-hit", "0x6",
            "--ints-after-hit", "0x18"
        ]);

        var detail = Assert.IsType<Pes2021DumpRuntimeDayPayloadClusterDetailCliCommand>(command);
        Assert.Equal("PES2021", detail.Selector.ProcessName);
        Assert.Equal(2026, detail.Year);
        Assert.Equal(3, detail.Month);
        Assert.Equal(28, detail.Day);
        Assert.Equal(2, detail.ClusterOrdinal);
        Assert.Equal(0x1000UL, detail.StartAddress);
        Assert.Equal(0x2000UL, detail.StopAddress);
        Assert.Equal(0x3000UL, detail.CalendarBaseAddress);
        Assert.Equal([472, 0x210], detail.PreferredStrides);
        Assert.Equal(5, detail.MinHitCount);
        Assert.Equal(0x900, detail.ClusterGap);
        Assert.Equal(0x40, detail.PreviewBytes);
        Assert.Equal(0x6, detail.IntsBeforeHit);
        Assert.Equal(0x18, detail.IntsAfterHit);
    }

    [Fact]
    public void ParsePes2021AnalyzeRuntimeDayPayloadClusterCommand_ParsesClusterOptions()
    {
        var command = ParseWithExtension([
            "pes2021-analyze-runtime-day-payload-cluster",
            "--pid", "26368",
            "--year", "2026",
            "--month", "3",
            "--day", "28",
            "--cluster-ordinal", "1",
            "--start-address", "0x4000",
            "--stop-address", "0x5000",
            "--calendar-base-address", "0x6000",
            "--preferred-strides", "0x1d8,0x210",
            "--min-hit-count", "4",
            "--cluster-gap", "0x700",
            "--preview-bytes", "0x80",
            "--ints-before-hit", "4",
            "--ints-after-hit", "20"
        ]);

        var analyze = Assert.IsType<Pes2021AnalyzeRuntimeDayPayloadClusterCliCommand>(command);
        Assert.Equal(26368, analyze.Selector.ProcessId);
        Assert.Equal(2026, analyze.Year);
        Assert.Equal(3, analyze.Month);
        Assert.Equal(28, analyze.Day);
        Assert.Equal(1, analyze.ClusterOrdinal);
        Assert.Equal(0x4000UL, analyze.StartAddress);
        Assert.Equal(0x5000UL, analyze.StopAddress);
        Assert.Equal(0x6000UL, analyze.CalendarBaseAddress);
        Assert.Equal([0x1d8, 0x210], analyze.PreferredStrides);
        Assert.Equal(4, analyze.MinHitCount);
        Assert.Equal(0x700, analyze.ClusterGap);
        Assert.Equal(0x80, analyze.PreviewBytes);
        Assert.Equal(4, analyze.IntsBeforeHit);
        Assert.Equal(20, analyze.IntsAfterHit);
    }

    [Fact]
    public void ParsePes2021ClassifyRuntimeDayVariantCommand_ParsesSecondaryBaseAddress()
    {
        var command = ParseWithExtension([
            "pes2021-classify-runtime-day-variant",
            "--name", "PES2021",
            "--year", "2026",
            "--month", "3",
            "--day", "29",
            "--start-address", "0x1000",
            "--stop-address", "0x2000",
            "--secondary-base-address", "0x2500",
            "--calendar-base-address", "0x3000",
            "--preferred-strides", "472,0x210",
            "--min-hit-count", "5",
            "--cluster-gap", "0x900",
            "--preview-bytes", "0x40"
        ]);

        var classify = Assert.IsType<Pes2021ClassifyRuntimeDayVariantCliCommand>(command);
        Assert.Equal("PES2021", classify.Selector.ProcessName);
        Assert.Equal(2026, classify.Year);
        Assert.Equal(3, classify.Month);
        Assert.Equal(29, classify.Day);
        Assert.Equal(0x1000UL, classify.StartAddress);
        Assert.Equal(0x2000UL, classify.StopAddress);
        Assert.Equal(0x2500UL, classify.SecondaryBaseAddress);
        Assert.Equal(0x3000UL, classify.CalendarBaseAddress);
        Assert.Equal([472, 0x210], classify.PreferredStrides);
        Assert.Equal(5, classify.MinHitCount);
        Assert.Equal(0x900, classify.ClusterGap);
        Assert.Equal(0x40, classify.PreviewBytes);
    }
}
