using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Overmem.Abstractions;
using Overmem.Abstractions.Memory;
using Overmem.Abstractions.Processes;
using Overmem.Extensions.Pes2021.Fixtures;
using Overmem.Extensions.Pes2021.Players;
using Overmem.Runtime;

namespace Overmem.Extensions.Pes2021.Tests;

public sealed class Pes2021PlayerDiscoveryTests
{
    [Fact]
    public async Task AnchorFinder_FindsAnchorAtPlayerId_AndClassifiesCandidates()
    {
        var gateway = new FakeProcessMemoryGateway();
        var profile = Pes2021PlayerProfileDefaults.BuildBuiltIn();

        var region = BuildRegionWithFiveRecords(out var offset);
        gateway.MapRegion(0x1000, region);

        var clock = new FakeSystemClock();
        var finder = new Pes2021PlayerAnchorFinder(gateway, clock);
        var attachment = new AttachmentInfo(AttachmentId.New(), 1234, "PES2021",
            ProcessArchitecture.X64, clock.UtcNow);

        var result = await finder.FindAsync(attachment.AttachmentId,
            new ProcessInstanceIdentity(attachment.AttachmentId, attachment.ProcessId, attachment.ProcessStartedAtUtc, attachment.ProcessName),
            profile, 58120, regions: null, CancellationToken.None);

        Assert.NotNull(result);
        Assert.NotNull(result.AnchorAddress);
        Assert.False(result.Ambiguous);
        Assert.Equal(58120u, result.PlayerId);
        Assert.Equal($"0x{(ulong)(0x1000 + offset):X}", result.AnchorAddress);
        Assert.Contains(result.Candidates, c => c.PlayerId == 58120u);
    }

    [Fact]
    public async Task AnchorFinder_ReturnsNullAnchor_WhenNoCandidateMatches()
    {
        var gateway = new FakeProcessMemoryGateway();
        var profile = Pes2021PlayerProfileDefaults.BuildBuiltIn();

        var region = BuildRegionWithFiveRecords(out _);
        gateway.MapRegion(0x1000, region);

        var clock = new FakeSystemClock();
        var finder = new Pes2021PlayerAnchorFinder(gateway, clock);
        var attachment = new AttachmentInfo(AttachmentId.New(), 9999, "PES2021",
            ProcessArchitecture.X64, clock.UtcNow);

        var result = await finder.FindAsync(attachment.AttachmentId,
            new ProcessInstanceIdentity(attachment.AttachmentId, attachment.ProcessId, attachment.ProcessStartedAtUtc, attachment.ProcessName),
            profile, 1, regions: null, CancellationToken.None);

        Assert.Null(result.AnchorAddress);
        Assert.Equal("low", result.Confidence.Level);
        Assert.Contains("no_candidate", result.Diagnostics.RejectionReasons.Keys);
    }

    [Fact]
    public async Task AnchorFinder_RespectsProfileRegionFilter()
    {
        var gateway = new FakeProcessMemoryGateway();
        var profile = Pes2021PlayerProfileDefaults.BuildBuiltIn();

        var region = BuildRegionWithFiveRecords(out _);
        gateway.MapRegion(0x1000, region);

        var clock = new FakeSystemClock();
        var finder = new Pes2021PlayerAnchorFinder(gateway, clock);
        var attachment = new AttachmentInfo(AttachmentId.New(), 1234, "PES2021",
            ProcessArchitecture.X64, clock.UtcNow);

        var regions = new List<MemoryRegionInfo>
        {
            new(0x9000, 1024UL, "Free", "RW", "Private", true, true, false),
        };

        var result = await finder.FindAsync(attachment.AttachmentId,
            new ProcessInstanceIdentity(attachment.AttachmentId, attachment.ProcessId, attachment.ProcessStartedAtUtc, attachment.ProcessName),
            profile, 58120, regions, CancellationToken.None);

        Assert.Null(result.AnchorAddress);
        Assert.Equal(1, result.Diagnostics.RegionsRejected);
    }

    [Fact]
    public async Task AnchorFinder_RejectsIsolatedShiftedCandidate_AndKeepsNeighborConfirmedGrid()
    {
        var gateway = new FakeProcessMemoryGateway();
        var profile = Pes2021PlayerProfileDefaults.BuildBuiltIn();
        var realRegion = BuildRegionWithFiveRecords(out var realAnchorOffset);
        gateway.MapRegion(0x1000, realRegion);

        var shiftedRegion = new byte[profile.Stride + 3];
        var isolatedCandidate = BuildRecord(profile, 58120, "as Al-buraikan", 181, 75, 500_000);
        System.Buffer.BlockCopy(isolatedCandidate, 0, shiftedRegion, 3, profile.Stride);
        gateway.MapRegion(0x9000, shiftedRegion);

        var clock = new FakeSystemClock();
        var finder = new Pes2021PlayerAnchorFinder(gateway, clock);
        var attachment = new AttachmentInfo(AttachmentId.New(), 1234, "PES2021",
            ProcessArchitecture.X64, clock.UtcNow);

        var result = await finder.FindAsync(attachment.AttachmentId,
            new ProcessInstanceIdentity(attachment.AttachmentId, attachment.ProcessId, attachment.ProcessStartedAtUtc, attachment.ProcessName),
            profile, 58120, regions: null, CancellationToken.None);

        Assert.Equal($"0x{0x1000UL + (ulong)realAnchorOffset:X}", result.AnchorAddress);
        Assert.False(result.Ambiguous);
        Assert.Single(result.Candidates);
        Assert.Contains(PlayerRecordRejectionReasons.NeighborStrideMismatch, result.Diagnostics.RejectionReasons.Keys);
        Assert.Empty(gateway.Writes);
    }

    [Fact]
    public async Task Discovery_PreservesHighBitPlayerIds()
    {
        var gateway = new FakeProcessMemoryGateway();
        var profile = Pes2021PlayerProfileDefaults.BuildBuiltIn();
        const uint controlId = 0x8000003E;
        const int prefix = 0x10;
        var records = new[]
        {
            BuildRecord(profile, 0x4001FAFF, "Firas Al-buraikan", 181, 75, 0),
            BuildRecord(profile, controlId, "Franz Gonzales", 180, 74, 0),
            BuildRecord(profile, 0x40000001, "Marked Neighbor", 178, 72, 0),
        };
        var bytes = new byte[prefix + (records.Length * profile.Stride)];
        for (var index = 0; index < records.Length; index++)
        {
            System.Buffer.BlockCopy(records[index], 0, bytes, prefix + (index * profile.Stride), profile.Stride);
        }
        gateway.MapRegion(0xA000, bytes);

        var clock = new FakeSystemClock();
        var attachment = new AttachmentInfo(AttachmentId.New(), 1234, "PES2021",
            ProcessArchitecture.X64, clock.UtcNow);
        var process = new ProcessInstanceIdentity(attachment.AttachmentId, attachment.ProcessId,
            attachment.ProcessStartedAtUtc, attachment.ProcessName);
        var finder = new Pes2021PlayerAnchorFinder(gateway, clock);
        var anchor = await finder.FindAsync(attachment.AttachmentId, process, profile, controlId,
            regions: null, CancellationToken.None);

        Assert.Equal($"0x{0xA000UL + prefix + (ulong)profile.Stride:X}", anchor.AnchorAddress);
        var scanner = new Pes2021PlayerRegionScanner(gateway, clock);
        var result = await scanner.ScanAsync(attachment.AttachmentId, process, profile, anchor.Session,
            regions: null, CancellationToken.None);

        Assert.Equal(3, result.Players.Count);
        Assert.Contains(result.Players, player => player.PlayerId == controlId);
        Assert.Contains(result.Players, player => player.PlayerId == 0x4001FAFF);
        Assert.Empty(gateway.Writes);
    }

    [Fact]
    public async Task RegionScanner_DecodesAllFiveRecords_AndCountsDuplicates()
    {
        var gateway = new FakeProcessMemoryGateway();
        var profile = Pes2021PlayerProfileDefaults.BuildBuiltIn();

        var region = BuildRegionWithFiveRecords(out var anchorOffset);
        gateway.MapRegion(0x1000, region);

        var clock = new FakeSystemClock();
        var session = new PlayerSession(
            new PlayerProcessInstanceIdentity(AttachmentId.New(), 1234, clock.UtcNow, "PES2021"),
            profile.ProfileId, profile.ProfileVersion, profile.Sha256, profile.Stride,
            "0x1000", $"0x{0x1000UL + (ulong)region.Length:X}",
            $"0x{0x1000UL + (ulong)anchorOffset:X}", 58120u, "Piero Hincapie", string.Empty,
            clock.UtcNow, CacheDisposition.Discovered);

        var scanner = new Pes2021PlayerRegionScanner(gateway, clock);
        var attachment = new AttachmentInfo(AttachmentId.New(), 1234, "PES2021",
            ProcessArchitecture.X64, clock.UtcNow);

        var result = await scanner.ScanAsync(attachment.AttachmentId,
            new ProcessInstanceIdentity(attachment.AttachmentId, attachment.ProcessId, attachment.ProcessStartedAtUtc, attachment.ProcessName),
            profile, session, regions: null, CancellationToken.None);

        Assert.Equal(5, result.Players.Count);
        Assert.Equal(5, result.Diagnostics.RecordsDecoded);
        Assert.Equal(5, result.Diagnostics.RecordsAccepted);
        Assert.Equal(0, result.Diagnostics.RecordsRejected);
        Assert.Equal(0, result.Diagnostics.DuplicatePlayerIds);
    }

    [Fact]
    public async Task RegionScanner_ReportsDuplicatePlayerIds_WhenAddressesOverlap()
    {
        var gateway = new FakeProcessMemoryGateway();
        var profile = Pes2021PlayerProfileDefaults.BuildBuiltIn();

        var first = BuildRecord(profile, 58120, "Piero Hincapie", 184, 74, 500_000);
        var second = BuildRecord(profile, 58121, "Jhon Sanchez", 175, 74, 0);
        var duplicate = BuildRecord(profile, 58120, "Piero Hincapie", 184, 74, 500_000);

        var bytes = new byte[first.Length + second.Length + duplicate.Length];
        System.Buffer.BlockCopy(first, 0, bytes, 0, first.Length);
        System.Buffer.BlockCopy(second, 0, bytes, first.Length, second.Length);
        System.Buffer.BlockCopy(duplicate, 0, bytes, first.Length + second.Length, duplicate.Length);
        gateway.MapRegion(0x2000, bytes);

        var clock = new FakeSystemClock();
        var session = new PlayerSession(
            new PlayerProcessInstanceIdentity(AttachmentId.New(), 1234, clock.UtcNow, "PES2021"),
            profile.ProfileId, profile.ProfileVersion, profile.Sha256, profile.Stride,
            "0x2000", $"0x{0x2000UL + (ulong)bytes.Length:X}", "0x2000", 58120u,
            "Piero Hincapie", string.Empty, clock.UtcNow, CacheDisposition.Discovered);

        var scanner = new Pes2021PlayerRegionScanner(gateway, clock);
        var attachment = new AttachmentInfo(AttachmentId.New(), 1234, "PES2021",
            ProcessArchitecture.X64, clock.UtcNow);

        var result = await scanner.ScanAsync(attachment.AttachmentId,
            new ProcessInstanceIdentity(attachment.AttachmentId, attachment.ProcessId, attachment.ProcessStartedAtUtc, attachment.ProcessName),
            profile, session, regions: null, CancellationToken.None);

        Assert.Equal(3, result.Players.Count);
        Assert.Equal(1, result.Diagnostics.DuplicatePlayerIds);
    }

    [Fact]
    public async Task Discovery_UsesAnchorResidue_AndAccountsForReservedTail()
    {
        var gateway = new FakeProcessMemoryGateway();
        var profile = Pes2021PlayerProfileDefaults.BuildBuiltIn();
        const int regionPrefix = 0x10;
        const int emptySlots = 3;

        var populated = BuildRegionWithFiveRecords(profile, out var anchorOffset);
        var bytes = new byte[regionPrefix + populated.Length + (emptySlots * profile.Stride) + 17];
        System.Buffer.BlockCopy(populated, 0, bytes, regionPrefix, populated.Length);
        gateway.MapRegion(0x3000, bytes);

        var clock = new FakeSystemClock();
        var anchorAddress = 0x3000UL + regionPrefix + (ulong)anchorOffset;
        var session = new PlayerSession(
            new PlayerProcessInstanceIdentity(AttachmentId.New(), 1234, clock.UtcNow, "PES2021"),
            profile.ProfileId, profile.ProfileVersion, profile.Sha256, profile.Stride,
            "0x3000", $"0x{0x3000UL + (ulong)bytes.Length:X}", $"0x{anchorAddress:X}", 58120u,
            "Piero Hincapie", string.Empty, clock.UtcNow, CacheDisposition.Discovered);

        var scanner = new Pes2021PlayerRegionScanner(gateway, clock);
        var attachment = new AttachmentInfo(AttachmentId.New(), 1234, "PES2021",
            ProcessArchitecture.X64, clock.UtcNow);

        var result = await scanner.ScanAsync(attachment.AttachmentId,
            new ProcessInstanceIdentity(attachment.AttachmentId, attachment.ProcessId, attachment.ProcessStartedAtUtc, attachment.ProcessName),
            profile, session, regions: null, CancellationToken.None);

        Assert.Equal(5, result.Players.Count);
        Assert.Equal("0x3010", result.ArenaCoverage?.FirstRecordAddress);
        Assert.Equal(profile.Stride, result.ArenaCoverage?.RecordStride);
        Assert.Equal(5, result.ArenaCoverage?.PopulatedSlots);
        Assert.Equal(emptySlots, result.ArenaCoverage?.EmptyReservedSlots);
        Assert.Equal(8, result.ArenaCoverage?.TheoreticalSlots);
        Assert.Equal("NON_PLAYER_DATA", result.ArenaCoverage?.BoundaryClassification);
        Assert.False(string.IsNullOrWhiteSpace(result.ArenaCoverage?.EmptyRecordSha256));
        Assert.Empty(gateway.Writes);
    }

    [Fact]
    public async Task SessionCache_StoresAndReuses_AfterRevalidation()
    {
        var gateway = new FakeProcessMemoryGateway();
        var profile = Pes2021PlayerProfileDefaults.BuildBuiltIn();
        var region = BuildRegionWithFiveRecords(out var offset);
        gateway.MapRegion(0x1000, region);

        var clock = new FakeSystemClock();
        var cache = new Pes2021PlayerSessionCache(gateway);

        var attachmentId = AttachmentId.New();
        var key = new PlayerSessionCacheKey(attachmentId, 1234, clock.UtcNow,
            profile.ProfileId, profile.ProfileVersion, profile.Sha256);

        var anchorAddress = $"0x{(ulong)(0x1000 + offset):X}";
        var sampleHash = ComputeSampleHash(gateway, anchorAddress, profile.Stride);

        cache.Store(key, new PlayerSessionCacheEntry(
            CacheDisposition.Discovered, "0x1000", $"0x{0x1000UL + (ulong)region.Length:X}",
            anchorAddress, 58120, "Piero Hincapie", sampleHash, clock.UtcNow));

        var disposition = await cache.TryReuseAsync(key, CancellationToken.None);
        Assert.Equal(CacheDisposition.Reused, disposition);
        Assert.Equal(1, cache.Count);
    }

    [Fact]
    public async Task SessionCache_InvalidatesWhenAnchorBytesChange()
    {
        var gateway = new FakeProcessMemoryGateway();
        var profile = Pes2021PlayerProfileDefaults.BuildBuiltIn();
        var region = BuildRegionWithFiveRecords(out var offset);
        gateway.MapRegion(0x1000, region);

        var clock = new FakeSystemClock();
        var cache = new Pes2021PlayerSessionCache(gateway);

        var attachmentId = AttachmentId.New();
        var key = new PlayerSessionCacheKey(attachmentId, 1234, clock.UtcNow,
            profile.ProfileId, profile.ProfileVersion, profile.Sha256);

        var anchorAddress = $"0x{(ulong)(0x1000 + offset):X}";
        cache.Store(key, new PlayerSessionCacheEntry(
            CacheDisposition.Discovered, "0x1000", $"0x{0x1000UL + (ulong)region.Length:X}",
            anchorAddress, 58120, "Piero Hincapie", "stale-hash", clock.UtcNow));

        var disposition = await cache.TryReuseAsync(key, CancellationToken.None);
        Assert.Equal(CacheDisposition.Refused, disposition);
        Assert.Equal(0, cache.Count);
    }

    [Fact]
    public async Task SessionCache_InvalidateByAttachment_RemovesOnlyMatchingEntries()
    {
        var gateway = new FakeProcessMemoryGateway();
        var profile = Pes2021PlayerProfileDefaults.BuildBuiltIn();

        var clock = new FakeSystemClock();
        var cache = new Pes2021PlayerSessionCache(gateway);

        var attachmentA = AttachmentId.New();
        var attachmentB = AttachmentId.New();

        cache.Store(new PlayerSessionCacheKey(attachmentA, 1, clock.UtcNow, profile.ProfileId, profile.ProfileVersion, profile.Sha256),
            new PlayerSessionCacheEntry(CacheDisposition.Discovered, "0x0", "0x0", "0x0", 1, "x", "y", clock.UtcNow));
        cache.Store(new PlayerSessionCacheKey(attachmentB, 2, clock.UtcNow, profile.ProfileId, profile.ProfileVersion, profile.Sha256),
            new PlayerSessionCacheEntry(CacheDisposition.Discovered, "0x0", "0x0", "0x0", 2, "x", "y", clock.UtcNow));

        cache.InvalidateByAttachment(attachmentA);
        Assert.Equal(1, cache.Count);
        Assert.True(cache.TryGet(new PlayerSessionCacheKey(attachmentB, 2, clock.UtcNow, profile.ProfileId, profile.ProfileVersion, profile.Sha256), out _));
    }

    [Fact]
    public void DiagnosticsCollector_AggregatesReadCallsAndRejections()
    {
        var collector = new Pes2021PlayerDiscoveryDiagnosticsCollector();
        collector.AddRegions(new[]
        {
            new PlayerRegionDiagnostic("0x0", "0x100", 256, "Commit", "Private", "RW", true, true, false, "accepted", null),
            new PlayerRegionDiagnostic("0x100", "0x200", 256, "Free", "Private", "RW", true, true, false, "rejected", "state_mismatch"),
        });
        collector.AddReadCall(1024, 1024);
        collector.AddReadCall(512, 512);
        collector.AddRecords(10, 9, 1);
        collector.AddRejection(PlayerRecordRejectionReasons.HeightOutOfRange);
        collector.AddDuplicatePlayerIds(2);
        collector.AddAmbiguousResolutions(1);

        var diag = collector.Build();
        Assert.Equal(2, diag.RegionsEnumerated);
        Assert.Equal(1, diag.RegionsAccepted);
        Assert.Equal(1, diag.RegionsRejected);
        Assert.Equal(2, diag.ReadCalls);
        Assert.Equal(10, diag.RecordsDecoded);
        Assert.Equal(9, diag.RecordsAccepted);
        Assert.Equal(1, diag.RecordsRejected);
        Assert.Equal(2, diag.DuplicatePlayerIds);
        Assert.Equal(1, diag.AmbiguousResolutions);
        Assert.Contains(PlayerRecordRejectionReasons.HeightOutOfRange, diag.RejectionReasons.Keys);
    }

    private static byte[] BuildRegionWithFiveRecords(Pes2021PlayerProfile profile, out int anchorOffset)
    {
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

        anchorOffset = 2 * profile.Stride;
        return buffer;
    }

    private static byte[] BuildRegionWithFiveRecords(out int anchorOffset)
        => BuildRegionWithFiveRecords(Pes2021PlayerProfileDefaults.BuildBuiltIn(), out anchorOffset);

    private static byte[] BuildRecord(
        Pes2021PlayerProfile profile,
        uint playerId,
        string? name,
        byte height,
        byte weight,
        int marketValueRaw)
    {
        var bytes = new byte[profile.Stride];

        var heightField = profile.RecordLayout.Fields.Single(f => f.Name == "height");
        bytes[heightField.Offset] = height;

        var weightField = profile.RecordLayout.Fields.Single(f => f.Name == "weight");
        bytes[weightField.Offset] = weight;

        var playerIdField = profile.RecordLayout.Fields.Single(f => f.Name == "playerId");
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(
            bytes.AsSpan(playerIdField.Offset, 4), playerId);

        if (name is not null)
        {
            var nameField = profile.RecordLayout.Fields.Single(f => f.Name == "playerName");
            var max = System.Math.Min(name.Length, nameField.Width - 1);
            var ascii = System.Text.Encoding.ASCII.GetBytes(name.Substring(0, max));
            for (var i = 0; i < max; i++)
            {
                bytes[nameField.Offset + i] = ascii[i];
            }

            bytes[nameField.Offset + max] = 0;
        }

        var marketField = profile.RecordLayout.Fields.Single(f => f.Name == "marketValue");
        System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(
            bytes.AsSpan(marketField.Offset, 4), marketValueRaw);

        return bytes;
    }

    private static string ComputeSampleHash(FakeProcessMemoryGateway gateway, string anchorAddressHex, int stride)
    {
        var addressHex = anchorAddressHex.StartsWith("0x")
            ? anchorAddressHex.Substring(2)
            : anchorAddressHex;
        var address = ulong.Parse(addressHex, System.Globalization.NumberStyles.HexNumber);
        var first = gateway.ReadAsync(new ReadMemoryRequest(AttachmentId.New(), address, MemoryValueKind.Bytes, stride * 2), default).GetAwaiter().GetResult();
        var bytes = System.Convert.FromHexString(first.Value);
        return System.Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes)).ToLowerInvariant();
    }
}
