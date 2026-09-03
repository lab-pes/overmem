using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Overmem.Extensions.Pes2021.Tests;

public sealed class Pes2021PlayerContractsTests
{
    [Fact]
    public void AnchorContract_RoundTripsThroughSystemTextJson()
    {
        var json = LoadWireExample("anchor");
        using var document = JsonDocument.Parse(json);

        Assert.Equal("pes2021.player-memory.v1", document.RootElement.GetProperty("schemaVersion").GetString());
        Assert.Equal("player_anchor", document.RootElement.GetProperty("kind").GetString());

        var session = document.RootElement.GetProperty("session");
        Assert.True(session.TryGetProperty("attachmentId", out _));
        Assert.True(session.TryGetProperty("processId", out _));
        Assert.True(session.TryGetProperty("processStartedAtUtc", out _));
        Assert.True(session.TryGetProperty("profileId", out _));
        Assert.True(session.TryGetProperty("profileVersion", out _));
        Assert.True(session.TryGetProperty("profileSha256", out _));

        var anchor = document.RootElement.GetProperty("anchor");
        Assert.True(anchor.TryGetProperty("recordAddress", out _));
        Assert.True(anchor.TryGetProperty("playerId", out _));
        Assert.True(anchor.TryGetProperty("fingerprint", out _));
        Assert.True(anchor.TryGetProperty("context", out _));
        Assert.True(anchor.TryGetProperty("evidenceStatus", out _));

        var roundTrip = JsonSerializer.Deserialize<JsonElement>(json);
        Assert.Equal("pes2021.player-memory.v1", roundTrip.GetProperty("schemaVersion").GetString());
    }

    [Fact]
    public void SnapshotContract_RawAndDisplayAreBothReturned_WhenTransformExists()
    {
        var json = LoadWireExample("snapshot");
        using var document = JsonDocument.Parse(json);

        var fields = document.RootElement.GetProperty("player").GetProperty("fields");
        var marketValue = fields.EnumerateArray()
            .Single(element => element.GetProperty("name").GetString() == "marketValue");

        Assert.Equal(JsonValueKind.Number, marketValue.GetProperty("raw").ValueKind);
        Assert.Equal(JsonValueKind.Number, marketValue.GetProperty("display").ValueKind);
        Assert.Equal("CANDIDATE", marketValue.GetProperty("evidenceStatus").GetString());
    }

    [Fact]
    public void ScanContract_DistinguishesRawAndDisplayAndPreservesEvidenceStatus()
    {
        var json = LoadWireExample("scan");
        using var document = JsonDocument.Parse(json);

        var summary = document.RootElement.GetProperty("summary");
        Assert.Equal(30001, summary.GetProperty("theoreticalSlots").GetInt32());
        Assert.Equal(25005, summary.GetProperty("populatedSlots").GetInt32());
        Assert.Equal(4996, summary.GetProperty("emptyReservedSlots").GetInt32());
        Assert.Equal(0, summary.GetProperty("unaccountedSlots").GetInt32());
        Assert.Equal(25005, summary.GetProperty("uniqueRawPlayerIds").GetInt32());
        Assert.Equal(0, summary.GetProperty("duplicatePlayerIds").GetInt32());

        var historical = summary.GetProperty("historicalComparison");
        Assert.Equal(23253, historical.GetProperty("historicalExportRows").GetInt32());
        Assert.Equal(23250, historical.GetProperty("historicalUniqueIds").GetInt32());
        Assert.Equal(23250, historical.GetProperty("historicalIdsPresentLive").GetInt32());
        Assert.Equal(0, historical.GetProperty("historicalIdsAbsentLive").GetInt32());
        Assert.Equal(1755, historical.GetProperty("liveRawIdsAbsentHistorically").GetInt32());

        var firstPlayer = document.RootElement.GetProperty("players").EnumerateArray().First();
        var fields = firstPlayer.GetProperty("fields").EnumerateArray().ToList();

        Assert.Contains(fields, field =>
            field.GetProperty("name").GetString() == "unknown_12c"
            && field.GetProperty("evidenceStatus").GetString() == "UNKNOWN"
            && field.TryGetProperty("display", out var display)
            && display.ValueKind == JsonValueKind.Null);
    }

    [Fact]
    public void HighBitIdFixture_RoundTripsWithoutTruncation_AndKeepsIdFlagsUnknown()
    {
        var json = LoadWireExample("player-high-bit-id");
        using var document = JsonDocument.Parse(json);

        Assert.Equal("pes2021.player-memory.v1", document.RootElement.GetProperty("schemaVersion").GetString());
        Assert.Equal("player_snapshot", document.RootElement.GetProperty("kind").GetString());

        var player = document.RootElement.GetProperty("player");
        Assert.True(player.GetProperty("playerId").TryGetUInt32(out var playerId));
        Assert.Equal(0x8000003Eu, playerId);
        Assert.Equal(2147483710u, playerId);

        Assert.Equal("Franz Gonzales", player.GetProperty("fingerprint").GetString());
        Assert.Equal("UNKNOWN", player.GetProperty("idFlags").GetString());
        Assert.Equal("EDIT_BASE_CONFIRMED", player.GetProperty("context").GetString());

        var fields = player.GetProperty("fields").EnumerateArray().ToList();
        var nameField = fields.Single(f => f.GetProperty("name").GetString() == "playerName");
        Assert.Equal("Franz Gonzales", nameField.GetProperty("raw").GetString());

        // Round-trip test: ensure serialization and deserialization maintain full u32 without signed overflow
        var roundTrip = JsonSerializer.Deserialize<JsonElement>(json);
        var roundTripPlayer = roundTrip.GetProperty("player");
        Assert.True(roundTripPlayer.GetProperty("playerId").TryGetUInt32(out var rtPlayerId));
        Assert.Equal(0x8000003Eu, rtPlayerId);
        Assert.Equal("UNKNOWN", roundTripPlayer.GetProperty("idFlags").GetString());
    }

    [Fact]
    public void QueryContract_AmbiguousResultsAreReturnedWithAmbiguousFlag()
    {
        var json = LoadWireExample("query");
        using var document = JsonDocument.Parse(json);

        Assert.True(document.RootElement.GetProperty("ambiguous").GetBoolean());
        Assert.True(document.RootElement.GetProperty("results").GetArrayLength() > 1);
    }

    [Fact]
    public void PatchPlanContract_CarriesOldBytesNewBytesAndRollbackId()
    {
        var json = LoadWireExample("patch-plan");
        using var document = JsonDocument.Parse(json);

        Assert.Equal("patch_plan", document.RootElement.GetProperty("kind").GetString());
        Assert.True(document.RootElement.TryGetProperty("planId", out var planId));
        Assert.False(string.IsNullOrEmpty(planId.GetString()));

        var patches = document.RootElement.GetProperty("patches").EnumerateArray().ToList();
        Assert.NotEmpty(patches);
        var firstPatch = patches.First();
        Assert.Equal("marketValue", firstPatch.GetProperty("field").GetString());
        Assert.True(firstPatch.TryGetProperty("oldBytes", out _));
        Assert.True(firstPatch.TryGetProperty("newBytes", out _));

        var rollback = document.RootElement.GetProperty("rollback");
        Assert.True(rollback.TryGetProperty("rollbackId", out _));
        Assert.True(rollback.TryGetProperty("artifactPath", out _));
    }

    [Fact]
    public void ApplyResultContract_OutcomesUseScreamingSnakeCase()
    {
        var json = LoadWireExample("apply-result");
        using var document = JsonDocument.Parse(json);

        Assert.Equal("apply_result", document.RootElement.GetProperty("kind").GetString());

        var validOutcomes = new HashSet<string>
        {
            "applied", "dry_run", "expected_bytes_mismatch", "verify_failed", "rollback_invoked", "rejected",
        };

        var dryRun = LoadWireExample("apply-result-dry-run");
        using var dryRunDoc = JsonDocument.Parse(dryRun);
        Assert.Contains(dryRunDoc.RootElement.GetProperty("outcome").GetString()!, validOutcomes);

        var rejected = LoadWireExample("apply-result-rejected");
        using var rejectedDoc = JsonDocument.Parse(rejected);
        Assert.Contains(rejectedDoc.RootElement.GetProperty("outcome").GetString()!, validOutcomes);
        Assert.True(rejectedDoc.RootElement.TryGetProperty("code", out _));
    }

    [Fact]
    public void RollbackResultContract_CarriesBytesRestoredAndRawRestored()
    {
        var json = LoadWireExample("rollback-result");
        using var document = JsonDocument.Parse(json);

        Assert.Equal("rollback_result", document.RootElement.GetProperty("kind").GetString());
        var verification = document.RootElement.GetProperty("verification");
        Assert.True(verification.TryGetProperty("bytesRestored", out _));
        Assert.True(verification.TryGetProperty("rawRestored", out _));
        Assert.Equal("rolled_back", document.RootElement.GetProperty("outcome").GetString());
    }

    [Fact]
    public void WireContracts_DoNotEmbedAbsoluteLiveAddresses()
    {
        var examples = new[]
        {
            "anchor", "scan", "snapshot", "query", "patch-plan", "apply-result", "rollback-result", "player-high-bit-id",
        };

        foreach (var name in examples)
        {
            var json = LoadWireExample(name);
            Assert.DoesNotContain("0x7FF4D908", json, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("58120", json);
            Assert.DoesNotContain("Piero Hincapie", json, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Theory]
    [InlineData("anchor", "player_anchor")]
    [InlineData("scan", "player_scan")]
    [InlineData("snapshot", "player_snapshot")]
    [InlineData("query", "player_query")]
    [InlineData("patch-plan", "patch_plan")]
    [InlineData("apply-result", "apply_result")]
    [InlineData("rollback-result", "rollback_result")]
    [InlineData("player-high-bit-id", "player_snapshot")]
    public void WireContracts_KindFieldMatchesExampleName(string fileName, string expectedKind)
    {
        var json = LoadWireExample(fileName);
        using var document = JsonDocument.Parse(json);
        Assert.Equal(expectedKind, document.RootElement.GetProperty("kind").GetString());
    }

    private static string LoadWireExample(string name)
    {
        var assemblyDirectory = Path.GetDirectoryName(typeof(Pes2021PlayerContractsTests).Assembly.Location)
            ?? AppContext.BaseDirectory;
        var docsDirectory = Path.GetFullPath(Path.Combine(assemblyDirectory, "..", "..", "..", "..", "..", "docs", "pes2021", "player-memory"));
        var examplesDir = Path.Combine(docsDirectory, "wire-examples");
        var filePath = Path.Combine(examplesDir, $"{name}.json");
        Assert.True(File.Exists(filePath), $"missing wire example at '{filePath}'");
        return File.ReadAllText(filePath);
    }
}
