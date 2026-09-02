using System.Collections.Generic;
using System.Linq;
using Overmem.Abstractions;
using Overmem.Abstractions.Memory;
using Overmem.Abstractions.Processes;
using Overmem.Extensions.Pes2021.Cli;
using Overmem.Extensions.Pes2021.Fixtures;
using Overmem.Extensions.Pes2021.Players;
using Overmem.Runtime;

namespace Overmem.Extensions.Pes2021.Tests;

public sealed class Pes2021PlayerCliSurfaceTests
{
    [Fact]
    public async Task FindPlayerAnchor_RefreshScanQuery_RoundTripsThroughServices()
    {
        var gateway = new FakeProcessMemoryGateway();
        var profile = Pes2021PlayerProfileDefaults.BuildBuiltIn();

        var region = BuildRegionWithFiveRecords();
        gateway.MapRegion(0x1000, region);

        var clock = new FakeSystemClock();
        var attachment = new AttachmentInfo(AttachmentId.New(), 1234, "PES2021",
            ProcessArchitecture.X64, clock.UtcNow);

        var catalog = new Pes2021PlayerCatalog();
        var service = new Pes2021PlayerCatalogService(
            catalog,
            new Pes2021PlayerAnchorFinder(gateway, clock),
            new Pes2021PlayerRegionScanner(gateway, clock),
            new Pes2021PlayerSessionCache(gateway),
            gateway,
            clock);

        var discovery = await service.RefreshAsync(
            attachment.AttachmentId,
            new ProcessInstanceIdentity(attachment.AttachmentId, attachment.ProcessId, attachment.ProcessStartedAtUtc, attachment.ProcessName),
            profile,
            58120,
            regions: null,
            default);

        Assert.Equal(5, discovery.Players.Count);
        var query = new Pes2021PlayerQueryService(catalog);
        var result = query.QueryByPlayerId(58120);
        Assert.False(result.Ambiguous);
        Assert.Single(result.Results);
        Assert.Equal("Piero Hincapie", result.Results[0].PlayerName);
    }

    [Fact]
    public void CliExtension_Parses_AllPlayerCommands()
    {
        var extension = new Pes2021CliExtension();
        var findAnchor = extension.TryParse("pes2021-find-player-anchor", new Dictionary<string, string?>
        {
            ["pid"] = "1234",
            ["control-player-id"] = "58120",
            ["profile-file"] = "files/pes2021/player-memory/pes2021-player-record-v1.json",
            ["output-file"] = "tmp.json",
        });
        Assert.NotNull(findAnchor);

        var scanPlayers = extension.TryParse("pes2021-scan-players", new Dictionary<string, string?>
        {
            ["name"] = "PES2021",
            ["control-player-id"] = "58120",
            ["max-records"] = "1000",
        });
        Assert.NotNull(scanPlayers);

        var query = extension.TryParse("pes2021-query-player", new Dictionary<string, string?>
        {
            ["pid"] = "1234",
            ["player-id"] = "58120",
        });
        Assert.NotNull(query);

        var export = extension.TryParse("pes2021-export-player-catalog", new Dictionary<string, string?>
        {
            ["pid"] = "1234",
            ["control-player-id"] = "58120",
            ["output"] = "tmp-catalog.json",
        });
        Assert.NotNull(export);

        Assert.Contains(extension.GetHelpLines(), line => line.Contains("pes2021-find-player-anchor"));
        Assert.Contains(extension.GetHelpLines(), line => line.Contains("pes2021-scan-players"));
        Assert.Contains(extension.GetHelpLines(), line => line.Contains("pes2021-query-player"));
        Assert.Contains(extension.GetHelpLines(), line => line.Contains("pes2021-export-player-catalog"));
    }

    [Fact]
    public void CliExtension_RejectsUnknownCommand()
    {
        var extension = new Pes2021CliExtension();
        Assert.Null(extension.TryParse("pes2021-unknown", new Dictionary<string, string?> { ["pid"] = "1" }));
    }

    [Fact]
    public void ExportCatalog_WritesAtomicFile_EndToEnd()
    {
        var gateway = new FakeProcessMemoryGateway();
        var profile = Pes2021PlayerProfileDefaults.BuildBuiltIn();
        var region = BuildRegionWithFiveRecords();
        gateway.MapRegion(0x1000, region);

        var clock = new FakeSystemClock();
        var catalog = new Pes2021PlayerCatalog();
        var service = new Pes2021PlayerCatalogService(
            catalog,
            new Pes2021PlayerAnchorFinder(gateway, clock),
            new Pes2021PlayerRegionScanner(gateway, clock),
            new Pes2021PlayerSessionCache(gateway),
            gateway,
            clock);

        var attachment = new AttachmentInfo(AttachmentId.New(), 1234, "PES2021",
            ProcessArchitecture.X64, clock.UtcNow);

        var discovery = service.RefreshAsync(attachment.AttachmentId,
            new ProcessInstanceIdentity(attachment.AttachmentId, attachment.ProcessId, attachment.ProcessStartedAtUtc, attachment.ProcessName),
            profile, 58120, regions: null, default).GetAwaiter().GetResult();

        var export = Pes2021PlayerCatalogExporter.Build(discovery);
        Assert.Equal(5, export.Players.Count);

        var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"players-e2e-{System.Guid.NewGuid():N}.json");
        try
        {
            Pes2021AtomicFileWriter.WriteJson(path, export, new System.Text.Json.JsonSerializerOptions
            {
                WriteIndented = true,
                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
            });
            Assert.True(System.IO.File.Exists(path));
        }
        finally
        {
            if (System.IO.File.Exists(path)) System.IO.File.Delete(path);
        }
    }

    private static byte[] BuildRegionWithFiveRecords()
    {
        var profile = Pes2021PlayerProfileDefaults.BuildBuiltIn();
        var records = new[]
        {
            BuildRecord(profile, 58118, "Luis Segovia", 182, 74, 0),
            BuildRecord(profile, 58119, "Anthony Landazuri", 179, 73, 0),
            BuildRecord(profile, 58120, "Piero Hincapie", 184, 74, 500_000),
            BuildRecord(profile, 58121, "Jhon Sanchez", 175, 74, 0),
            BuildRecord(profile, 58122, "Jonathan Bauman", 178, 73, 0),
        };

        var buffer = new byte[records.Length * profile.Stride];
        for (var i = 0; i < records.Length; i++)
        {
            System.Buffer.BlockCopy(records[i], 0, buffer, i * profile.Stride, profile.Stride);
        }

        return buffer;
    }

    private static byte[] BuildRecord(Pes2021PlayerProfile profile, uint playerId, string name, byte height, byte weight, int marketValueRaw)
    {
        var bytes = new byte[profile.Stride];
        var heightField = profile.RecordLayout.Fields.Single(f => f.Name == "height");
        bytes[heightField.Offset] = height;
        var weightField = profile.RecordLayout.Fields.Single(f => f.Name == "weight");
        bytes[weightField.Offset] = weight;
        var playerIdField = profile.RecordLayout.Fields.Single(f => f.Name == "playerId");
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(playerIdField.Offset, 4), playerId);
        var nameField = profile.RecordLayout.Fields.Single(f => f.Name == "playerName");
        var max = System.Math.Min(name.Length, nameField.Width - 1);
        var ascii = System.Text.Encoding.ASCII.GetBytes(name.Substring(0, max));
        for (var i = 0; i < max; i++) bytes[nameField.Offset + i] = ascii[i];
        bytes[nameField.Offset + max] = 0;
        var marketField = profile.RecordLayout.Fields.Single(f => f.Name == "marketValue");
        System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(marketField.Offset, 4), marketValueRaw);
        return bytes;
    }
}