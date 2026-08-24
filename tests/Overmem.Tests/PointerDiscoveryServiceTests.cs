using Overmem.Abstractions;
using Overmem.Abstractions.Memory;
using Overmem.Abstractions.Processes;
using Overmem.Application.Pointers;
using Overmem.Runtime.Attachments;

namespace Overmem.Tests;

public sealed class PointerDiscoveryServiceTests
{
    [Fact]
    public async Task DiscoverAsync_FindsTwoLevelZeroOffsetChain()
    {
        var attachmentId = new AttachmentId(Guid.NewGuid());
        var targetAddress = 0x5000UL;
        var level1Address = 0x4000UL;
        var level2Address = 0x3000UL;
        var gateway = new FakeGateway(attachmentId);
        gateway.AddRegion(0x3000, CreatePointerRegion(0x3000, level2Address, level1Address, level1Address, targetAddress));
        var sessionRegistry = CreateRegistry(attachmentId, ProcessArchitecture.X64);
        var service = new PointerDiscoveryService(gateway, sessionRegistry);

        var result = await service.DiscoverAsync(new DiscoverPointersRequest(attachmentId, targetAddress, MaxDepth: 2, MaxResults: 10));

        Assert.Contains(result.Candidates, candidate =>
            candidate.BaseAddress == level1Address &&
            candidate.Offsets.SequenceEqual([0L]) &&
            candidate.IsValidated &&
            candidate.ResolvedAddress == targetAddress);
        Assert.Contains(result.Candidates, candidate =>
            candidate.BaseAddress == level2Address &&
            candidate.Offsets.SequenceEqual([0L, 0L]) &&
            candidate.IsValidated &&
            candidate.ResolvedAddress == targetAddress);
    }

    [Fact]
    public async Task DiscoverAsync_FindsOffsetAdjustedCandidate()
    {
        var attachmentId = new AttachmentId(Guid.NewGuid());
        var targetAddress = 0x5020UL;
        var baseAddress = 0x3000UL;
        var gateway = new FakeGateway(attachmentId);
        gateway.AddRegion(0x3000, CreatePointerRegion(baseAddress, baseAddress, 0x5000UL));
        var sessionRegistry = CreateRegistry(attachmentId, ProcessArchitecture.X64);
        var service = new PointerDiscoveryService(gateway, sessionRegistry);

        var result = await service.DiscoverAsync(new DiscoverPointersRequest(attachmentId, targetAddress, MaxDepth: 1, MaxOffset: 0x40, MaxResults: 10));

        Assert.Contains(result.Candidates, candidate =>
            candidate.BaseAddress == baseAddress &&
            candidate.Offsets.SequenceEqual([0x20L]) &&
            candidate.IsValidated &&
            candidate.ResolvedAddress == targetAddress);
    }

    [Fact]
    public async Task DiscoverAsync_CanFilterByBaseModuleName()
    {
        var attachmentId = new AttachmentId(Guid.NewGuid());
        var targetAddress = 0x5000UL;
        var moduleBaseAddress = 0x1000UL;
        var moduleCellAddress = 0x1010UL;
        var otherCellAddress = 0x3000UL;
        var gateway = new FakeGateway(attachmentId);
        gateway.AddModule("demo.exe", moduleBaseAddress, 0x200);
        gateway.AddRegion(moduleBaseAddress, CreatePointerRegion(moduleBaseAddress, moduleCellAddress, targetAddress));
        gateway.AddRegion(otherCellAddress, CreatePointerRegion(otherCellAddress, otherCellAddress, targetAddress));
        var sessionRegistry = CreateRegistry(attachmentId, ProcessArchitecture.X64);
        var service = new PointerDiscoveryService(gateway, sessionRegistry);

        var result = await service.DiscoverAsync(new DiscoverPointersRequest(
            attachmentId,
            targetAddress,
            MaxDepth: 1,
            MaxResults: 10,
            BaseModuleName: "demo.exe"));

        var candidate = Assert.Single(result.Candidates);
        Assert.Equal(moduleCellAddress, candidate.BaseAddress);
        Assert.Equal("demo.exe", candidate.ModuleName);
        Assert.Equal(0x10, candidate.ModuleRelativeBaseOffset);
    }

    [Fact]
    public async Task DiscoverAsync_ModuleRootedCandidateScoresHigherThanHeap()
    {
        var attachmentId = new AttachmentId(Guid.NewGuid());
        var targetAddress = 0x6000UL;
        var moduleBaseAddress = 0x1000UL;
        var moduleCellAddress = 0x1010UL;
        var heapCellAddress = 0x4000UL;
        var gateway = new FakeGateway(attachmentId);
        gateway.AddModule("game.exe", moduleBaseAddress, 0x200);
        gateway.AddRegion(moduleBaseAddress, CreatePointerRegion(moduleBaseAddress, moduleCellAddress, targetAddress));
        gateway.AddRegion(heapCellAddress, CreatePointerRegion(heapCellAddress, heapCellAddress, targetAddress));
        var sessionRegistry = CreateRegistry(attachmentId, ProcessArchitecture.X64);
        var service = new PointerDiscoveryService(gateway, sessionRegistry);

        var result = await service.DiscoverAsync(new DiscoverPointersRequest(
            attachmentId,
            targetAddress,
            MaxDepth: 1,
            MaxResults: 10,
            RevalidateCandidates: false));

        Assert.Equal(2, result.Candidates.Count);
        var first = result.Candidates[0];
        var second = result.Candidates[1];
        Assert.NotNull(first.ModuleName);
        Assert.Null(second.ModuleName);
        Assert.True(first.Score > second.Score);
    }

    [Fact]
    public async Task DiscoverAsync_ValidatedCandidateScoresHigherThanUnvalidated()
    {
        var attachmentId = new AttachmentId(Guid.NewGuid());
        var targetAddress = 0x7000UL;
        var cellAddress = 0x2000UL;
        var gateway = new FakeGateway(attachmentId);
        gateway.AddRegion(cellAddress, CreatePointerRegion(cellAddress, cellAddress, targetAddress));
        var sessionRegistry = CreateRegistry(attachmentId, ProcessArchitecture.X64);
        var service = new PointerDiscoveryService(gateway, sessionRegistry);

        var resultValidated = await service.DiscoverAsync(new DiscoverPointersRequest(
            attachmentId,
            targetAddress,
            MaxDepth: 1,
            MaxResults: 10,
            RevalidateCandidates: true));

        var resultRaw = await service.DiscoverAsync(new DiscoverPointersRequest(
            attachmentId,
            targetAddress,
            MaxDepth: 1,
            MaxResults: 10,
            RevalidateCandidates: false));

        var validatedScore = resultValidated.Candidates[0].Score;
        var rawScore = resultRaw.Candidates[0].Score;
        Assert.True(validatedScore > rawScore, $"Expected validated score {validatedScore} > raw score {rawScore}");
    }

    private static InMemoryAttachmentSessionRegistry CreateRegistry(AttachmentId attachmentId, ProcessArchitecture architecture)
    {
        var registry = new InMemoryAttachmentSessionRegistry();
        registry.Register(new AttachmentInfo(attachmentId, 1234, "fake", architecture), DateTimeOffset.UtcNow);
        return registry;
    }

    private static byte[] CreatePointerRegion(ulong regionBaseAddress, ulong cellAddress, ulong pointedAddress)
    {
        var buffer = new byte[checked((int)(cellAddress - regionBaseAddress) + sizeof(ulong))];
        BitConverter.GetBytes(pointedAddress).CopyTo(buffer, checked((int)(cellAddress - regionBaseAddress)));
        return buffer;
    }

    private static byte[] CreatePointerRegion(ulong regionBaseAddress, ulong firstCellAddress, ulong firstPointedAddress, ulong secondCellAddress, ulong secondPointedAddress)
    {
        var buffer = new byte[checked((int)(secondCellAddress - regionBaseAddress) + sizeof(ulong))];
        BitConverter.GetBytes(firstPointedAddress).CopyTo(buffer, checked((int)(firstCellAddress - regionBaseAddress)));
        BitConverter.GetBytes(secondPointedAddress).CopyTo(buffer, checked((int)(secondCellAddress - regionBaseAddress)));
        return buffer;
    }

    private sealed class FakeGateway(AttachmentId attachmentId) : IProcessMemoryGateway
    {
        private readonly List<ModuleInfo> _modules = [];
        private readonly List<MemoryRegionInfo> _regions = [];
        private readonly Dictionary<ulong, byte> _memory = [];

        public void AddModule(string name, ulong baseAddress, int size)
            => _modules.Add(new ModuleInfo(name, baseAddress, size));

        public void AddRegion(ulong baseAddress, byte[] data)
        {
            _regions.Add(new MemoryRegionInfo(baseAddress, (ulong)data.Length, "Commit", "ReadWrite", "Private", true, true, false));
            for (var index = 0; index < data.Length; index++)
            {
                _memory[baseAddress + (ulong)index] = data[index];
            }
        }

        public Task<AttachmentInfo> AttachAsync(ProcessSelector selector, CancellationToken cancellationToken = default)
            => Task.FromResult(new AttachmentInfo(attachmentId, 1234, "fake", ProcessArchitecture.X64));

        public Task DetachAsync(AttachmentId attachmentId, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<IReadOnlyList<ModuleInfo>> ListModulesAsync(AttachmentId attachmentId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<ModuleInfo>>(_modules.ToArray());

        public Task<IReadOnlyList<MemoryRegionInfo>> ListRegionsAsync(AttachmentId attachmentId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<MemoryRegionInfo>>(_regions.ToArray());

        public Task<ResolvePointerResult> ResolvePointerAsync(ResolvePointerRequest request, CancellationToken cancellationToken = default)
        {
            ulong currentAddress = request.BaseAddress;
            foreach (var offset in request.Offsets)
            {
                var buffer = Enumerable.Range(0, sizeof(ulong))
                    .Select(index => _memory[currentAddress + (ulong)index])
                    .ToArray();

                currentAddress = BitConverter.ToUInt64(buffer, 0);
                currentAddress = offset >= 0
                    ? checked(currentAddress + (ulong)offset)
                    : checked(currentAddress - (ulong)(-offset));
            }

            return Task.FromResult(new ResolvePointerResult(request.BaseAddress, request.Offsets, currentAddress));
        }

        public Task<ResolvePointerResult> ResolveModulePointerAsync(ResolveModulePointerRequest request, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<PatternScanResult> ScanPatternAsync(PatternScanRequest request, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<ReadMemoryResult> ReadAsync(ReadMemoryRequest request, CancellationToken cancellationToken = default)
        {
            var bytes = Enumerable.Range(0, request.Size)
                .Select(index => _memory[request.Address + (ulong)index])
                .ToArray();

            return Task.FromResult(new ReadMemoryResult(request.Address, request.ValueKind, Convert.ToHexString(bytes), bytes.Length));
        }

        public Task<WriteMemoryResult> WriteAsync(WriteMemoryRequest request, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();
    }
}