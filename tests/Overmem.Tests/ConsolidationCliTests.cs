using Overmem.Abstractions.Cli;
using Overmem.Cli;
using Overmem.Extensions.Pes2021.Cli;

namespace Overmem.Tests;

public sealed class ConsolidationCliTests
{
    private static readonly ICliCommandExtension[] Extensions = [new Pes2021CliExtension()];

    [Fact]
    public void FamilyDiscoveryIsReachableWithItsScanLimits()
    {
        var command = Assert.IsType<Pes2021DiscoverPlayerFamiliesCliCommand>(CliArgumentParser.Parse(
            ["pes2021-discover-player-families", "--pid", "123", "--control-player-id", "0x1234",
             "--max-bytes", "8192", "--timeout-ms", "2000", "--output-mode", "Full"], Extensions));
        Assert.Equal(0x1234u, command.ControlPlayerId);
        Assert.Equal(8192, command.MaxBytes);
        Assert.Equal(2000, command.TimeoutMs);
        Assert.Equal("Full", command.OutputMode);
    }

    [Theory]
    [InlineData("pes2021-inventory-player-hits", typeof(Pes2021InventoryPlayerHitsCliCommand))]
    [InlineData("pes2021-export-family-catalog", typeof(Pes2021ExportFamilyCatalogCliCommand))]
    public void RemainingFamilyScanCommandsAreReachable(string name, Type expected)
    {
        var command = CliArgumentParser.Parse(
            [name, "--pid", "123", "--control-player-id", "1234", "--output-file", "catalog.json"], Extensions);
        Assert.IsType(expected, command);
    }

    [Fact]
    public void OfflineFamilyComparisonDoesNotRequireAProcess()
    {
        var command = Assert.IsType<Pes2021ComparePlayerSessionsCliCommand>(CliArgumentParser.Parse(
            ["pes2021-compare-player-sessions", "--before-catalog", "before.json", "--after-catalog", "after.json"], Extensions));
        Assert.Equal("before.json", command.BeforeCatalogPath);
        Assert.Equal("after.json", command.AfterCatalogPath);
    }
}
