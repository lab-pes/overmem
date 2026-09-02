using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Overmem.Abstractions.Memory;
using Overmem.Abstractions.Processes;
using Overmem.Extensions.Pes2021.Fixtures;
using Overmem.Extensions.Pes2021.Players;

namespace Overmem.Extensions.Pes2021.Tests;

public sealed class Pes2021PlayerTransactionTests
{
    [Fact]
    public async Task PlanApplyVerify_RoundTripsThroughFakeGateway()
    {
        var (gateway, region, profile, _) = SetupRegionWithMarketRecord(out var recordAddress, out var marketValueField);
        var identity = new ProcessInstanceIdentity(AttachmentId.New(), 1234, DateTimeOffset.UtcNow, "Overmem.TestTarget");
        var core = new Pes2021PlayerTransactionCore(gateway);

        var artifactPath = Path.Combine(Path.GetTempPath(), $"player-rollback-{Guid.NewGuid():N}.json");
        try
        {
            var plan = await core.PlanAsync(identity.AttachmentId, identity, profile, recordAddress, marketValueField, rawNew: 0, artifactPath, default);
            Assert.Equal(500_000L, plan.RawOld);
            Assert.Equal(0L, plan.RawNew);

            var dryRun = await core.ApplyAsync(identity.AttachmentId, identity, profile, plan, dryRun: true, default);
            Assert.Equal("dry_run", dryRun.Outcome);

            var applied = await core.ApplyAsync(identity.AttachmentId, identity, profile, plan, dryRun: false, default);
            Assert.Equal("applied", applied.Outcome);

            Assert.Equal(0, ReadInt32LE(region, profile.Stride + marketValueField.Offset));

            var rollback = await core.RollbackAsync(identity.AttachmentId, identity, profile, plan, default);
            Assert.Equal("rolled_back", rollback.Outcome);
            Assert.Equal(marketValueField.Width, rollback.BytesRestored);

            Assert.Equal(500_000, ReadInt32LE(region, profile.Stride + marketValueField.Offset));
        }
        finally
        {
            if (File.Exists(artifactPath)) File.Delete(artifactPath);
        }
    }

    [Fact]
    public async Task ApplyAsync_RejectsExpectedBytesMismatch()
    {
        var (gateway, region, profile, _) = SetupRegionWithMarketRecord(out var recordAddress, out var marketValueField);
        var identity = new ProcessInstanceIdentity(AttachmentId.New(), 1234, DateTimeOffset.UtcNow, "Overmem.TestTarget");
        var core = new Pes2021PlayerTransactionCore(gateway);

        var artifactPath = Path.Combine(Path.GetTempPath(), $"player-rollback-{Guid.NewGuid():N}.json");
        try
        {
            var plan = await core.PlanAsync(identity.AttachmentId, identity, profile, recordAddress, marketValueField, rawNew: 100, artifactPath, default);

            region[profile.Stride + marketValueField.Offset] = 0x42;
            region[profile.Stride + marketValueField.Offset + 1] = 0x42;
            region[profile.Stride + marketValueField.Offset + 2] = 0x42;
            region[profile.Stride + marketValueField.Offset + 3] = 0x42;

            var result = await core.ApplyAsync(identity.AttachmentId, identity, profile, plan, dryRun: false, default);
            Assert.Equal("expected_bytes_mismatch", result.Outcome);
            Assert.Equal("PES2021_PLAYER_EXPECTED_BYTES_MISMATCH", result.Code);
        }
        finally
        {
            if (File.Exists(artifactPath)) File.Delete(artifactPath);
        }
    }

    [Fact]
    public async Task RollbackAsync_RestoresBytes_WhenPostApplyStateMatchesPlan()
    {
        var (gateway, region, profile, _) = SetupRegionWithMarketRecord(out var recordAddress, out var marketValueField);
        var identity = new ProcessInstanceIdentity(AttachmentId.New(), 1234, DateTimeOffset.UtcNow, "Overmem.TestTarget");
        var core = new Pes2021PlayerTransactionCore(gateway);

        var artifactPath = Path.Combine(Path.GetTempPath(), $"player-rollback-{Guid.NewGuid():N}.json");
        try
        {
            var plan = await core.PlanAsync(identity.AttachmentId, identity, profile, recordAddress, marketValueField, rawNew: 999, artifactPath, default);
            var applied = await core.ApplyAsync(identity.AttachmentId, identity, profile, plan, dryRun: false, default);
            Assert.Equal("applied", applied.Outcome);

            var rolled = await core.RollbackAsync(identity.AttachmentId, identity, profile, plan, default);
            Assert.Equal("rolled_back", rolled.Outcome);
            Assert.Equal(500_000, ReadInt32LE(region, profile.Stride + marketValueField.Offset));
        }
        finally
        {
            if (File.Exists(artifactPath)) File.Delete(artifactPath);
        }
    }

    [Fact]
    public async Task PlanAsync_RefusesUnknownProcess()
    {
        var (gateway, _, profile, _) = SetupRegionWithMarketRecord(out var recordAddress, out var marketValueField);
        var identity = new ProcessInstanceIdentity(AttachmentId.New(), 999, DateTimeOffset.UtcNow, "PES2021");
        var core = new Pes2021PlayerTransactionCore(gateway);

        var artifactPath = Path.Combine(Path.GetTempPath(), $"player-rollback-{Guid.NewGuid():N}.json");
        try
        {
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                core.PlanAsync(identity.AttachmentId, identity, profile, recordAddress, marketValueField, rawNew: 0, artifactPath, default));
        }
        finally
        {
            if (File.Exists(artifactPath)) File.Delete(artifactPath);
        }
    }

    [Fact]
    public void RollbackArtifact_IsWrittenAtomically()
    {
        var path = Path.Combine(Path.GetTempPath(), $"player-rollback-{Guid.NewGuid():N}.json");
        try
        {
            var artifact = new PlayerRollbackArtifact(
                RollbackId: "abc", PlanId: "plan", RecordAddress: 0x1000,
                FieldOffset: 372, FieldWidth: 4,
                OriginalHex: "20A10700", OriginalSha256: "0000",
                RawOld: 500_000, RawNew: 0, FieldName: "marketValue",
                CreatedAtUtc: DateTimeOffset.UtcNow);
            Overmem.Extensions.Pes2021.Cli.Pes2021AtomicFileWriter.WriteJson(path, artifact, new System.Text.Json.JsonSerializerOptions
            {
                WriteIndented = true,
                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
            });
            Assert.True(File.Exists(path));
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    private static (FakeProcessMemoryGateway Gateway, byte[] Region, Pes2021PlayerProfile Profile, Pes2021PlayerFieldDefinition Field)
        SetupRegionWithMarketRecord(out ulong recordAddress, out Pes2021PlayerFieldDefinition marketValueField)
    {
        var gateway = new FakeProcessMemoryGateway();
        var profile = Pes2021PlayerProfileDefaults.BuildBuiltIn();
        marketValueField = profile.RecordLayout.Fields.Single(f => f.Name == "marketValue");
        var region = new byte[profile.Stride * 2];
        gateway.MapRegion(0x1000, region);
        recordAddress = 0x1000UL + (ulong)profile.Stride;
        System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(
            region.AsSpan(profile.Stride + marketValueField.Offset, 4), 500_000);
        return (gateway, region, profile, marketValueField);
    }

    private static int ReadInt32LE(byte[] buffer, int offset)
        => System.Buffers.Binary.BinaryPrimitives.ReadInt32LittleEndian(buffer.AsSpan(offset, 4));
}