using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Overmem.Extensions.Pes2021.Cli;
using Overmem.Extensions.Pes2021.Players;

namespace Overmem.Extensions.Pes2021.Tests;

public sealed class Pes2021PlayerCatalogTests
{
    [Fact]
    public void Catalog_ReplaceAndSnapshot_RoundTripsRecords()
    {
        var profile = Pes2021PlayerProfileDefaults.BuildBuiltIn();
        var record1 = BuildRecord(profile, 58120, "Piero Hincapie", 184, 74, 500_000);
        var record2 = BuildRecord(profile, 58121, "Jhon Sanchez", 175, 74, 0);
        var decoded1 = Pes2021PlayerRecordParser.TryParse(record1, 0, 0x1000, profile).Record!;
        var decoded2 = Pes2021PlayerRecordParser.TryParse(record2, 1, 0x117C, profile).Record!;

        var session = MakeSession(profile);
        var diagnostics = new PlayerDiscoveryDiagnostics(
            CacheDisposition: Overmem.Extensions.Pes2021.Fixtures.CacheDisposition.Discovered,
            RegionsEnumerated: 1, RegionsAccepted: 1, RegionsRejected: 0,
            BytesRequested: 1900, BytesRead: 1900, ReadCalls: 1, BlocksRead: 1,
            RecordsDecoded: 2, RecordsAccepted: 2, RecordsRejected: 0,
            DuplicatePlayerIds: 0, AmbiguousResolutions: 0,
            RejectionReasons: new Dictionary<string, int>(),
            StageDurationMs: new Dictionary<string, double>(),
            Regions: System.Array.Empty<PlayerRegionDiagnostic>(),
            Warnings: System.Array.Empty<string>());

        var result = new PlayerDiscoveryResult(session, new[] { decoded1, decoded2 }, diagnostics);
        var catalog = new Pes2021PlayerCatalog();
        catalog.Replace(result);

        Assert.Equal(2, catalog.Count);
        Assert.Equal(decoded1.PlayerId, catalog.Snapshot()[0].PlayerId);
    }

    [Fact]
    public void QueryService_QueryByPlayerId_ReturnsSingleMatch()
    {
        var profile = Pes2021PlayerProfileDefaults.BuildBuiltIn();
        var record1 = BuildRecord(profile, 58120, "Piero Hincapie", 184, 74, 500_000);
        var record2 = BuildRecord(profile, 58121, "Jhon Sanchez", 175, 74, 0);
        var decoded1 = Pes2021PlayerRecordParser.TryParse(record1, 0, 0x1000, profile).Record!;
        var decoded2 = Pes2021PlayerRecordParser.TryParse(record2, 1, 0x117C, profile).Record!;

        var session = MakeSession(profile);
        var diagnostics = MakeDiagnostics();
        var catalog = new Pes2021PlayerCatalog();
        catalog.Replace(new PlayerDiscoveryResult(session, new[] { decoded1, decoded2 }, diagnostics));

        var query = new Pes2021PlayerQueryService(catalog);
        var result = query.QueryByPlayerId(58120);
        Assert.False(result.Ambiguous);
        Assert.Single(result.Results);
        Assert.Equal("Piero Hincapie", result.Results[0].PlayerName);
    }

    [Fact]
    public void QueryService_QueryByPlayerId_FlagsAmbiguous_WhenDuplicates()
    {
        var profile = Pes2021PlayerProfileDefaults.BuildBuiltIn();
        var first = Pes2021PlayerRecordParser.TryParse(BuildRecord(profile, 58120, "Piero Hincapie", 184, 74, 500_000), 0, 0x1000, profile).Record!;
        var second = Pes2021PlayerRecordParser.TryParse(BuildRecord(profile, 58120, "Piero Hincapie", 184, 74, 500_000), 1, 0x2000, profile).Record!;

        var session = MakeSession(profile);
        var diagnostics = MakeDiagnostics();
        var catalog = new Pes2021PlayerCatalog();
        catalog.Replace(new PlayerDiscoveryResult(session, new[] { first, second }, diagnostics));

        var query = new Pes2021PlayerQueryService(catalog);
        var result = query.QueryByPlayerId(58120);
        Assert.True(result.Ambiguous);
        Assert.Equal(2, result.Results.Count);
    }

    [Fact]
    public void QueryService_QueryByName_ExactAndPartial()
    {
        var profile = Pes2021PlayerProfileDefaults.BuildBuiltIn();
        var first = Pes2021PlayerRecordParser.TryParse(BuildRecord(profile, 58120, "Piero Hincapie", 184, 74, 0), 0, 0x1000, profile).Record!;
        var second = Pes2021PlayerRecordParser.TryParse(BuildRecord(profile, 58121, "Piero Sanchez", 175, 74, 0), 1, 0x117C, profile).Record!;

        var session = MakeSession(profile);
        var diagnostics = MakeDiagnostics();
        var catalog = new Pes2021PlayerCatalog();
        catalog.Replace(new PlayerDiscoveryResult(session, new[] { first, second }, diagnostics));

        var query = new Pes2021PlayerQueryService(catalog);
        var exact = query.QueryByName("Piero Hincapie", exactMatch: true);
        Assert.Single(exact.Results);

        var partial = query.QueryByName("Piero", exactMatch: false);
        Assert.Equal(2, partial.Results.Count);
    }

    [Fact]
    public void Exporter_BuildsPes2021PlayersV1Payload()
    {
        var profile = Pes2021PlayerProfileDefaults.BuildBuiltIn();
        var record = Pes2021PlayerRecordParser.TryParse(BuildRecord(profile, 58120, "Piero Hincapie", 184, 74, 500_000), 0, 0x1000, profile).Record!;

        var session = MakeSession(profile);
        var diagnostics = MakeDiagnostics();
        var result = new PlayerDiscoveryResult(session, new[] { record }, diagnostics);

        var export = Pes2021PlayerCatalogExporter.Build(result);
        Assert.Equal("pes2021.players.v1", export.SchemaVersion);
        Assert.Equal("player_catalog", export.Kind);
        Assert.Single(export.Players);
        Assert.Contains(export.Players[0].Fields, f => f.Name == "marketValue");
    }

    [Fact]
    public void Exporter_WritesAtomicFile_WithStablePayload()
    {
        var profile = Pes2021PlayerProfileDefaults.BuildBuiltIn();
        var record = Pes2021PlayerRecordParser.TryParse(BuildRecord(profile, 58120, "Piero Hincapie", 184, 74, 500_000), 0, 0x1000, profile).Record!;

        var session = MakeSession(profile);
        var diagnostics = MakeDiagnostics();
        var result = new PlayerDiscoveryResult(session, new[] { record }, diagnostics);

        var export = Pes2021PlayerCatalogExporter.Build(result);
        var tempPath = Path.Combine(Path.GetTempPath(), $"players-export-{System.Guid.NewGuid():N}.json");
        try
        {
            Pes2021AtomicFileWriter.WriteJson(tempPath, export, new JsonSerializerOptions
            {
                WriteIndented = true,
                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
            });

            Assert.True(File.Exists(tempPath));
            using var stream = File.OpenRead(tempPath);
            using var document = JsonDocument.Parse(stream);
            Assert.Equal("pes2021.players.v1", document.RootElement.GetProperty("schemaVersion").GetString());
            Assert.Equal("player_catalog", document.RootElement.GetProperty("kind").GetString());
        }
        finally
        {
            if (File.Exists(tempPath)) File.Delete(tempPath);
        }
    }

    private static PlayerSession MakeSession(Pes2021PlayerProfile profile)
        => new(
            new PlayerProcessInstanceIdentity(new Overmem.Abstractions.Processes.AttachmentId(System.Guid.NewGuid()), 1234, System.DateTimeOffset.UtcNow, "PES2021"),
            profile.ProfileId, profile.ProfileVersion, profile.Sha256, profile.Stride,
            "0x1000", "0x2000", "0x0", 0u, string.Empty, string.Empty, System.DateTimeOffset.UtcNow,
            Overmem.Extensions.Pes2021.Fixtures.CacheDisposition.Discovered);

    private static PlayerDiscoveryDiagnostics MakeDiagnostics()
        => new(
            CacheDisposition: Overmem.Extensions.Pes2021.Fixtures.CacheDisposition.Discovered,
            RegionsEnumerated: 1, RegionsAccepted: 1, RegionsRejected: 0,
            BytesRequested: 1900, BytesRead: 1900, ReadCalls: 1, BlocksRead: 1,
            RecordsDecoded: 1, RecordsAccepted: 1, RecordsRejected: 0,
            DuplicatePlayerIds: 0, AmbiguousResolutions: 0,
            RejectionReasons: new Dictionary<string, int>(),
            StageDurationMs: new Dictionary<string, double>(),
            Regions: System.Array.Empty<PlayerRegionDiagnostic>(),
            Warnings: System.Array.Empty<string>());

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