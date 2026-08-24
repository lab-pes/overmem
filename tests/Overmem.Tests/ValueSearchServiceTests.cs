using Overmem.Abstractions;
using Overmem.Abstractions.Memory;
using Overmem.Abstractions.Processes;
using Overmem.Abstractions.Search;
using Overmem.Runtime;
using Overmem.Runtime.Diagnostics;
using Overmem.Search;

namespace Overmem.Tests;

public sealed class ValueSearchServiceTests
{
    [Fact]
    public async Task StartExactSearch_FindsMatchingInt32Address()
    {
        var attachmentId = new AttachmentId(Guid.NewGuid());
        var gateway = new FakeGateway(attachmentId);
        gateway.AddRegion(0x1000, [
            0x01, 0x00, 0x00, 0x00,
            0x39, 0x05, 0x00, 0x00,
            0x39, 0x05, 0x00, 0x00
        ]);
        var service = new ValueSearchService(gateway, SystemClock.Instance, new InMemoryOperationJournal(), Microsoft.Extensions.Logging.Abstractions.NullLogger<ValueSearchService>.Instance);

        var result = await service.StartExactSearchAsync(new StartValueSearchRequest(attachmentId, MemoryValueKind.Int32, "1337"));

        Assert.Equal(2, result.ResultCount);
        Assert.Contains(result.Matches, match => match.Address == 0x1004);
        Assert.Contains(result.Matches, match => match.Address == 0x1008);
    }

    [Fact]
    public async Task RefineSearch_Changed_KeepsOnlyChangedMatches()
    {
        var attachmentId = new AttachmentId(Guid.NewGuid());
        var gateway = new FakeGateway(attachmentId);
        gateway.AddRegion(0x2000, [
            0x39, 0x05, 0x00, 0x00,
            0x39, 0x05, 0x00, 0x00
        ]);
        var service = new ValueSearchService(gateway, SystemClock.Instance, new InMemoryOperationJournal(), Microsoft.Extensions.Logging.Abstractions.NullLogger<ValueSearchService>.Instance);

        var started = await service.StartExactSearchAsync(new StartValueSearchRequest(attachmentId, MemoryValueKind.Int32, "1337"));
        gateway.WriteInt32(0x2004, 7331);

        var refined = await service.RefineAsync(new RefineValueSearchRequest(started.SessionId, ValueSearchComparison.Changed));

        var match = Assert.Single(refined.Matches);
        Assert.Equal(0x2004UL, match.Address);
        Assert.Equal("7331", match.Value);
    }

    [Fact]
    public async Task RefineSearch_Increased_KeepsOnlyIncreasedMatches()
    {
        var attachmentId = new AttachmentId(Guid.NewGuid());
        var gateway = new FakeGateway(attachmentId);
        gateway.AddRegion(0x2400, [
            0x39, 0x05, 0x00, 0x00,
            0x39, 0x05, 0x00, 0x00
        ]);
        var service = new ValueSearchService(gateway, SystemClock.Instance, new InMemoryOperationJournal(), Microsoft.Extensions.Logging.Abstractions.NullLogger<ValueSearchService>.Instance);

        var started = await service.StartExactSearchAsync(new StartValueSearchRequest(attachmentId, MemoryValueKind.Int32, "1337"));
        gateway.WriteInt32(0x2400, 1400);
        gateway.WriteInt32(0x2404, 1000);

        var refined = await service.RefineAsync(new RefineValueSearchRequest(started.SessionId, ValueSearchComparison.Increased));

        var match = Assert.Single(refined.Matches);
        Assert.Equal(0x2400UL, match.Address);
        Assert.Equal("1400", match.Value);
    }

    [Fact]
    public async Task RefineSearch_Decreased_KeepsOnlyDecreasedMatches()
    {
        var attachmentId = new AttachmentId(Guid.NewGuid());
        var gateway = new FakeGateway(attachmentId);
        gateway.AddRegion(0x2800, [
            0x39, 0x05, 0x00, 0x00,
            0x39, 0x05, 0x00, 0x00
        ]);
        var service = new ValueSearchService(gateway, SystemClock.Instance, new InMemoryOperationJournal(), Microsoft.Extensions.Logging.Abstractions.NullLogger<ValueSearchService>.Instance);

        var started = await service.StartExactSearchAsync(new StartValueSearchRequest(attachmentId, MemoryValueKind.Int32, "1337"));
        gateway.WriteInt32(0x2800, 1200);
        gateway.WriteInt32(0x2804, 1600);

        var refined = await service.RefineAsync(new RefineValueSearchRequest(started.SessionId, ValueSearchComparison.Decreased));

        var match = Assert.Single(refined.Matches);
        Assert.Equal(0x2800UL, match.Address);
        Assert.Equal("1200", match.Value);
    }

    [Fact]
    public async Task RefineSearch_Increased_RejectsNonNumericKinds()
    {
        var attachmentId = new AttachmentId(Guid.NewGuid());
        var gateway = new FakeGateway(attachmentId);
        gateway.AddRegion(0x2C00, [0x41, 0x42, 0x43]);
        var service = new ValueSearchService(gateway, SystemClock.Instance, new InMemoryOperationJournal(), Microsoft.Extensions.Logging.Abstractions.NullLogger<ValueSearchService>.Instance);

        var started = await service.StartExactSearchAsync(new StartValueSearchRequest(attachmentId, MemoryValueKind.Bytes, "414243"));

        var exception = await Assert.ThrowsAsync<NotSupportedException>(() => service.RefineAsync(new RefineValueSearchRequest(started.SessionId, ValueSearchComparison.Increased)));

        Assert.Contains("numeric", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RefineSearch_NotEqual_KeepsOnlyDifferentMatches()
    {
        var attachmentId = new AttachmentId(Guid.NewGuid());
        var gateway = new FakeGateway(attachmentId);
        gateway.AddRegion(0x2D00, [
            0x39, 0x05, 0x00, 0x00,
            0x39, 0x05, 0x00, 0x00
        ]);
        var service = new ValueSearchService(gateway, SystemClock.Instance, new InMemoryOperationJournal(), Microsoft.Extensions.Logging.Abstractions.NullLogger<ValueSearchService>.Instance);

        var started = await service.StartExactSearchAsync(new StartValueSearchRequest(attachmentId, MemoryValueKind.Int32, "1337"));
        gateway.WriteInt32(0x2D04, 9999);

        var refined = await service.RefineAsync(new RefineValueSearchRequest(started.SessionId, ValueSearchComparison.NotEqual, "1337"));

        var match = Assert.Single(refined.Matches);
        Assert.Equal(0x2D04UL, match.Address);
        Assert.Equal("9999", match.Value);
    }

    [Fact]
    public async Task RefineSearch_Between_KeepsOnlyValuesInsideRange()
    {
        var attachmentId = new AttachmentId(Guid.NewGuid());
        var gateway = new FakeGateway(attachmentId);
        gateway.AddRegion(0x2E00, [
            0x39, 0x05, 0x00, 0x00,
            0x39, 0x05, 0x00, 0x00,
            0x39, 0x05, 0x00, 0x00
        ]);
        var service = new ValueSearchService(gateway, SystemClock.Instance, new InMemoryOperationJournal(), Microsoft.Extensions.Logging.Abstractions.NullLogger<ValueSearchService>.Instance);

        var started = await service.StartExactSearchAsync(new StartValueSearchRequest(attachmentId, MemoryValueKind.Int32, "1337"));
        gateway.WriteInt32(0x2E00, 100);
        gateway.WriteInt32(0x2E04, 500);
        gateway.WriteInt32(0x2E08, 5000);

        var refined = await service.RefineAsync(new RefineValueSearchRequest(started.SessionId, ValueSearchComparison.Between, "200", "1000"));

        var match = Assert.Single(refined.Matches);
        Assert.Equal(0x2E04UL, match.Address);
        Assert.Equal("500", match.Value);
    }

    [Fact]
    public async Task RefineSearch_IncreasedBy_KeepsExactDelta()
    {
        var attachmentId = new AttachmentId(Guid.NewGuid());
        var gateway = new FakeGateway(attachmentId);
        gateway.AddRegion(0x2F00, [
            0x39, 0x05, 0x00, 0x00,
            0x39, 0x05, 0x00, 0x00
        ]);
        var service = new ValueSearchService(gateway, SystemClock.Instance, new InMemoryOperationJournal(), Microsoft.Extensions.Logging.Abstractions.NullLogger<ValueSearchService>.Instance);

        var started = await service.StartExactSearchAsync(new StartValueSearchRequest(attachmentId, MemoryValueKind.Int32, "1337"));
        gateway.WriteInt32(0x2F00, 1347);
        gateway.WriteInt32(0x2F04, 1500);

        var refined = await service.RefineAsync(new RefineValueSearchRequest(started.SessionId, ValueSearchComparison.IncreasedBy, "10"));

        var match = Assert.Single(refined.Matches);
        Assert.Equal(0x2F00UL, match.Address);
    }

    [Fact]
    public async Task RefineSearch_DecreasedBy_KeepsExactDelta()
    {
        var attachmentId = new AttachmentId(Guid.NewGuid());
        var gateway = new FakeGateway(attachmentId);
        gateway.AddRegion(0x3100, [
            0x39, 0x05, 0x00, 0x00,
            0x39, 0x05, 0x00, 0x00
        ]);
        var service = new ValueSearchService(gateway, SystemClock.Instance, new InMemoryOperationJournal(), Microsoft.Extensions.Logging.Abstractions.NullLogger<ValueSearchService>.Instance);

        var started = await service.StartExactSearchAsync(new StartValueSearchRequest(attachmentId, MemoryValueKind.Int32, "1337"));
        gateway.WriteInt32(0x3100, 1300);
        gateway.WriteInt32(0x3104, 1200);

        var refined = await service.RefineAsync(new RefineValueSearchRequest(started.SessionId, ValueSearchComparison.DecreasedBy, "37"));

        var match = Assert.Single(refined.Matches);
        Assert.Equal(0x3100UL, match.Address);
    }

    [Fact]
    public async Task RefineSearch_ChangedBy_MatchesAbsoluteDelta()
    {
        var attachmentId = new AttachmentId(Guid.NewGuid());
        var gateway = new FakeGateway(attachmentId);
        gateway.AddRegion(0x3200, [
            0x39, 0x05, 0x00, 0x00,
            0x39, 0x05, 0x00, 0x00
        ]);
        var service = new ValueSearchService(gateway, SystemClock.Instance, new InMemoryOperationJournal(), Microsoft.Extensions.Logging.Abstractions.NullLogger<ValueSearchService>.Instance);

        var started = await service.StartExactSearchAsync(new StartValueSearchRequest(attachmentId, MemoryValueKind.Int32, "1337"));
        gateway.WriteInt32(0x3200, 1342);
        gateway.WriteInt32(0x3204, 1332);

        var refined = await service.RefineAsync(new RefineValueSearchRequest(started.SessionId, ValueSearchComparison.ChangedBy, "5"));

        Assert.Equal(2, refined.ResultCount);
    }

    [Fact]
    public async Task RefineSearch_Between_RequiresBothBounds()
    {
        var attachmentId = new AttachmentId(Guid.NewGuid());
        var gateway = new FakeGateway(attachmentId);
        gateway.AddRegion(0x3300, [0x39, 0x05, 0x00, 0x00]);
        var service = new ValueSearchService(gateway, SystemClock.Instance, new InMemoryOperationJournal(), Microsoft.Extensions.Logging.Abstractions.NullLogger<ValueSearchService>.Instance);

        var started = await service.StartExactSearchAsync(new StartValueSearchRequest(attachmentId, MemoryValueKind.Int32, "1337"));

        await Assert.ThrowsAsync<ArgumentException>(() => service.RefineAsync(
            new RefineValueSearchRequest(started.SessionId, ValueSearchComparison.Between, "100")));
    }

    [Fact]
    public async Task StartUnknownSearch_CapturesAllAlignedAddresses()
    {
        var attachmentId = new AttachmentId(Guid.NewGuid());
        var gateway = new FakeGateway(attachmentId);
        gateway.AddRegion(0x4000, [
            0x01, 0x00, 0x00, 0x00,
            0x02, 0x00, 0x00, 0x00,
            0x03, 0x00, 0x00, 0x00
        ]);
        var service = new ValueSearchService(gateway, SystemClock.Instance, new InMemoryOperationJournal(), Microsoft.Extensions.Logging.Abstractions.NullLogger<ValueSearchService>.Instance);

        var result = await service.StartUnknownSearchAsync(new StartUnknownValueSearchRequest(attachmentId, MemoryValueKind.Int32, Alignment: 4));

        Assert.Equal(3, result.ResultCount);
        Assert.Contains(result.Matches, m => m.Address == 0x4000 && m.Value == "1");
        Assert.Contains(result.Matches, m => m.Address == 0x4004 && m.Value == "2");
        Assert.Contains(result.Matches, m => m.Address == 0x4008 && m.Value == "3");
    }

    [Fact]
    public async Task StartUnknownSearch_RefineByChanged_KeepsOnlyModifiedAddresses()
    {
        var attachmentId = new AttachmentId(Guid.NewGuid());
        var gateway = new FakeGateway(attachmentId);
        gateway.AddRegion(0x4100, [
            0x64, 0x00, 0x00, 0x00,
            0x64, 0x00, 0x00, 0x00
        ]);
        var service = new ValueSearchService(gateway, SystemClock.Instance, new InMemoryOperationJournal(), Microsoft.Extensions.Logging.Abstractions.NullLogger<ValueSearchService>.Instance);

        var started = await service.StartUnknownSearchAsync(new StartUnknownValueSearchRequest(attachmentId, MemoryValueKind.Int32, Alignment: 4));
        Assert.Equal(2, started.ResultCount);

        gateway.WriteInt32(0x4104, 200);
        var refined = await service.RefineAsync(new RefineValueSearchRequest(started.SessionId, ValueSearchComparison.Changed));

        var match = Assert.Single(refined.Matches);
        Assert.Equal(0x4104UL, match.Address);
        Assert.Equal("200", match.Value);
    }

    [Fact]
    public async Task StartUnknownSearch_RefineByBetween_FiltersExpectedRange()
    {
        var attachmentId = new AttachmentId(Guid.NewGuid());
        var gateway = new FakeGateway(attachmentId);
        gateway.AddRegion(0x4200, [
            0x05, 0x00, 0x00, 0x00,
            0x32, 0x00, 0x00, 0x00,
            0xC8, 0x00, 0x00, 0x00
        ]);
        var service = new ValueSearchService(gateway, SystemClock.Instance, new InMemoryOperationJournal(), Microsoft.Extensions.Logging.Abstractions.NullLogger<ValueSearchService>.Instance);

        var started = await service.StartUnknownSearchAsync(new StartUnknownValueSearchRequest(attachmentId, MemoryValueKind.Int32, Alignment: 4));
        var refined = await service.RefineAsync(new RefineValueSearchRequest(started.SessionId, ValueSearchComparison.Between, "10", "100"));

        var match = Assert.Single(refined.Matches);
        Assert.Equal(0x4204UL, match.Address);
        Assert.Equal("50", match.Value);
    }

    [Fact]
    public async Task StartUnknownSearch_IsUnknownStart_IsTrue()
    {
        var attachmentId = new AttachmentId(Guid.NewGuid());
        var gateway = new FakeGateway(attachmentId);
        gateway.AddRegion(0x4300, [0x01, 0x00, 0x00, 0x00]);
        var service = new ValueSearchService(gateway, SystemClock.Instance, new InMemoryOperationJournal(), Microsoft.Extensions.Logging.Abstractions.NullLogger<ValueSearchService>.Instance);

        await service.StartUnknownSearchAsync(new StartUnknownValueSearchRequest(attachmentId, MemoryValueKind.Int32, Alignment: 4));

        var sessions = await service.ListSessionsAsync();
        Assert.True(Assert.Single(sessions).IsUnknownStart);
    }

    [Fact]
    public async Task CloseSession_RemovesTrackedSession()
    {
        var attachmentId = new AttachmentId(Guid.NewGuid());
        var gateway = new FakeGateway(attachmentId);
        gateway.AddRegion(0x3000, [0x39, 0x05, 0x00, 0x00]);
        var service = new ValueSearchService(gateway, SystemClock.Instance, new InMemoryOperationJournal(), Microsoft.Extensions.Logging.Abstractions.NullLogger<ValueSearchService>.Instance);

        var started = await service.StartExactSearchAsync(new StartValueSearchRequest(attachmentId, MemoryValueKind.Int32, "1337"));

        Assert.True(await service.CloseSessionAsync(started.SessionId));
        Assert.Empty(await service.ListSessionsAsync());
    }

    private sealed class FakeGateway(AttachmentId attachmentId) : IProcessMemoryGateway
    {
        private readonly List<MemoryRegionInfo> _regions = [];
        private readonly Dictionary<ulong, byte> _memory = [];

        public void AddRegion(ulong baseAddress, byte[] data)
        {
            _regions.Add(new MemoryRegionInfo(baseAddress, (ulong)data.Length, "Commit", "ReadWrite", "Private", true, true, false));
            for (var index = 0; index < data.Length; index++)
            {
                _memory[baseAddress + (ulong)index] = data[index];
            }
        }

        public void WriteInt32(ulong address, int value)
        {
            var bytes = BitConverter.GetBytes(value);
            for (var index = 0; index < bytes.Length; index++)
            {
                _memory[address + (ulong)index] = bytes[index];
            }
        }

        public Task<AttachmentInfo> AttachAsync(ProcessSelector selector, CancellationToken cancellationToken = default)
            => Task.FromResult(new AttachmentInfo(attachmentId, 1234, "fake", ProcessArchitecture.X64));

        public Task DetachAsync(AttachmentId attachmentId, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<IReadOnlyList<ModuleInfo>> ListModulesAsync(AttachmentId attachmentId, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<IReadOnlyList<MemoryRegionInfo>> ListRegionsAsync(AttachmentId attachmentId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<MemoryRegionInfo>>(_regions.ToArray());

        public Task<ResolvePointerResult> ResolvePointerAsync(ResolvePointerRequest request, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<ResolvePointerResult> ResolveModulePointerAsync(ResolveModulePointerRequest request, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<PatternScanResult> ScanPatternAsync(PatternScanRequest request, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<ReadMemoryResult> ReadAsync(ReadMemoryRequest request, CancellationToken cancellationToken = default)
        {
            var bytes = Enumerable.Range(0, request.Size)
                .Select(index => _memory[request.Address + (ulong)index])
                .ToArray();

            var value = request.ValueKind == MemoryValueKind.Bytes
                ? Convert.ToHexString(bytes)
                : BitConverter.ToInt32(bytes, 0).ToString();

            return Task.FromResult(new ReadMemoryResult(request.Address, request.ValueKind, value, bytes.Length));
        }

        public Task<WriteMemoryResult> WriteAsync(WriteMemoryRequest request, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();
    }
}