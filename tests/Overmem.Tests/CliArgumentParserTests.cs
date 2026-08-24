using Overmem.Abstractions.Memory;
using Overmem.Cli;

namespace Overmem.Tests;

public sealed class CliArgumentParserTests
{
    [Fact]
    public void ParseReadCommand_AcceptsPidAndHexAddress()
    {
        var command = CliArgumentParser.Parse([
            "read",
            "--pid", "1234",
            "--address", "0x1000",
            "--value-kind", "Int32"
        ]);

        var read = Assert.IsType<ReadCliCommand>(command);
        Assert.Equal(1234, read.Selector.ProcessId);
        Assert.Equal(0x1000UL, read.Address);
        Assert.Equal(MemoryValueKind.Int32, read.ValueKind);
        Assert.Equal(0, read.Size);
    }

    [Fact]
    public void ParseResolveModulePointerCommand_ParsesOffsets()
    {
        var command = CliArgumentParser.Parse([
            "resolve-module-pointer",
            "--name", "demo",
            "--module-name", "demo.exe",
            "--base-offset", "0x20",
            "--offsets", "0x10,32"
        ]);

        var resolve = Assert.IsType<ResolveModulePointerCliCommand>(command);
        Assert.Equal("demo", resolve.Selector.ProcessName);
        Assert.Equal("demo.exe", resolve.ModuleName);
        Assert.Equal(0x20, resolve.BaseOffset);
        Assert.Equal([0x10, 32], resolve.Offsets);
    }

    [Fact]
    public void ParseTableRefreshCommand_RequiresSelectorAndFile()
    {
        var command = CliArgumentParser.Parse([
            "table-refresh",
            "--pid", "55",
            "--file", "table.json"
        ]);

        var refresh = Assert.IsType<RefreshTableCliCommand>(command);
        Assert.Equal(55, refresh.Selector.ProcessId);
        Assert.Equal("table.json", refresh.FilePath);
    }

    [Fact]
    public void ParseTableSaveCommand_ParsesSourceAndDestination()
    {
        var command = CliArgumentParser.Parse([
            "table-save",
            "--source-file", "input.json",
            "--destination-file", "output.json"
        ]);

        var save = Assert.IsType<SaveTableCliCommand>(command);
        Assert.Equal("input.json", save.SourceFilePath);
        Assert.Equal("output.json", save.DestinationFilePath);
    }

    [Fact]
    public void ParseScanValueCommand_ParsesSearchOptions()
    {
        var command = CliArgumentParser.Parse([
            "scan-value",
            "--pid", "99",
            "--value-kind", "Int32",
            "--value", "1337",
            "--alignment", "4",
            "--max-results", "25"
        ]);

        var scan = Assert.IsType<ScanValueCliCommand>(command);
        Assert.Equal(99, scan.Selector.ProcessId);
        Assert.Equal(MemoryValueKind.Int32, scan.ValueKind);
        Assert.Equal("1337", scan.Value);
        Assert.Equal(4, scan.Alignment);
        Assert.Equal(25, scan.MaxResults);
    }

    [Fact]
    public void ParseDiscoverPointersCommand_ParsesDiscoveryOptions()
    {
        var command = CliArgumentParser.Parse([
            "discover-pointers",
            "--pid", "77",
            "--target-address", "0x1234",
            "--max-depth", "3",
            "--max-offset", "0x20",
            "--alignment", "8",
            "--max-results", "40",
            "--base-module-name", "demo.exe",
            "--skip-revalidation"
        ]);

        var discover = Assert.IsType<DiscoverPointersCliCommand>(command);
        Assert.Equal(77, discover.Selector.ProcessId);
        Assert.Equal(0x1234UL, discover.TargetAddress);
        Assert.Equal(3, discover.MaxDepth);
        Assert.Equal(0x20, discover.MaxOffset);
        Assert.Equal(8, discover.Alignment);
        Assert.Equal(40, discover.MaxResults);
        Assert.Equal("demo.exe", discover.BaseModuleName);
        Assert.False(discover.RevalidateCandidates);
    }

}
