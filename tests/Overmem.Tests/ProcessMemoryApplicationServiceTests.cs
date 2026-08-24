using Overmem.Abstractions;
using Overmem.Abstractions.Freezing;
using Overmem.Abstractions.Memory;
using Overmem.Abstractions.Processes;
using Overmem.Application;
using Overmem.Application.Freezing;

namespace Overmem.Tests;

public sealed class ProcessMemoryApplicationServiceTests
{
    [Fact]
    public async Task AttachRejectsEmptySelector()
    {
        var service = new ProcessMemoryApplicationService(new FakeGateway(), new FakeFreezeCoordinator());

        await Assert.ThrowsAsync<ArgumentException>(() => service.AttachAsync(new ProcessSelector()));
    }

    [Fact]
    public async Task ScanPatternRejectsEmptyPattern()
    {
        var service = new ProcessMemoryApplicationService(new FakeGateway(), new FakeFreezeCoordinator());

        await Assert.ThrowsAsync<ArgumentException>(() => service.ScanPatternAsync(new PatternScanRequest(new AttachmentId(Guid.NewGuid()), string.Empty)));
    }

    [Fact]
    public async Task ResolveModulePointerRejectsEmptyModuleName()
    {
        var service = new ProcessMemoryApplicationService(new FakeGateway(), new FakeFreezeCoordinator());

        await Assert.ThrowsAsync<ArgumentException>(() => service.ResolveModulePointerAsync(new ResolveModulePointerRequest(new AttachmentId(Guid.NewGuid()), string.Empty, 0, [])));
    }

    [Fact]
    public async Task FreezeRejectsNonPositiveInterval()
    {
        var service = new ProcessMemoryApplicationService(new FakeGateway(), new FakeFreezeCoordinator());

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => service.FreezeAsync(new FreezeRequest(
            new AttachmentId(Guid.NewGuid()),
            new AbsoluteAddressSource(0x1000),
            MemoryValueKind.Int32,
            "1",
            IntervalMs: 0)));
    }

    private sealed class FakeGateway : IProcessMemoryGateway
    {
        public Task<AttachmentInfo> AttachAsync(ProcessSelector selector, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task DetachAsync(AttachmentId attachmentId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<IReadOnlyList<ModuleInfo>> ListModulesAsync(AttachmentId attachmentId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<IReadOnlyList<MemoryRegionInfo>> ListRegionsAsync(AttachmentId attachmentId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<ResolvePointerResult> ResolvePointerAsync(ResolvePointerRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<ResolvePointerResult> ResolveModulePointerAsync(ResolveModulePointerRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<PatternScanResult> ScanPatternAsync(PatternScanRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<ReadMemoryResult> ReadAsync(ReadMemoryRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<WriteMemoryResult> WriteAsync(WriteMemoryRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    }

    private sealed class FakeFreezeCoordinator : IProcessFreezeCoordinator
    {
        public Task<FreezeInfo> FreezeAsync(FreezeRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<bool> UnfreezeAsync(FreezeId freezeId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<int> UnfreezeByAttachmentAsync(AttachmentId attachmentId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<IReadOnlyList<FreezeInfo>> ListAsync(CancellationToken cancellationToken = default) => throw new NotImplementedException();
    }
}