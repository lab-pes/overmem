using Microsoft.Extensions.Logging.Abstractions;
using Overmem.Abstractions;
using Overmem.Abstractions.Freezing;
using Overmem.Abstractions.Memory;
using Overmem.Abstractions.Processes;
using Overmem.Application;
using Overmem.Application.Freezing;
using Overmem.Runtime;
using Overmem.Runtime.Attachments;
using Overmem.Runtime.Diagnostics;

namespace Overmem.Tests;

public sealed class ProcessMemoryApplicationServiceRuntimeTests
{
    [Fact]
    public async Task AttachAndDetach_UpdateRuntimeTracking()
    {
        var gateway = new TrackingGateway();
        var freezeCoordinator = new FakeFreezeCoordinator();
        var sessionRegistry = new InMemoryAttachmentSessionRegistry();
        var operationJournal = new InMemoryOperationJournal();
        var clock = new FakeClock(DateTimeOffset.Parse("2026-05-12T10:00:00+00:00"));
        var service = new ProcessMemoryApplicationService(
            gateway,
            freezeCoordinator,
            sessionRegistry,
            operationJournal,
            clock,
            NullLogger<ProcessMemoryApplicationService>.Instance);

        var attachment = await service.AttachAsync(new ProcessSelector(ProcessId: 4242));
        clock.Advance(TimeSpan.FromSeconds(5));
        await service.DetachAsync(attachment.AttachmentId);

        Assert.Empty(sessionRegistry.ListActive());

        var operations = operationJournal.ListRecent();
        Assert.Collection(operations,
            operation =>
            {
                Assert.Equal("detach_process", operation.Name);
                Assert.Equal("Succeeded", operation.Outcome);
                Assert.Equal(attachment.AttachmentId.ToString(), operation.AttachmentId);
            },
            operation =>
            {
                Assert.Equal("attach_process", operation.Name);
                Assert.Equal("Succeeded", operation.Outcome);
                Assert.Equal(attachment.AttachmentId.ToString(), operation.AttachmentId);
            });
    }

    private sealed class TrackingGateway : IProcessMemoryGateway
    {
        private readonly AttachmentInfo _attachment = new(AttachmentId.New(), 4242, "tracked", ProcessArchitecture.X64);

        public Task<AttachmentInfo> AttachAsync(ProcessSelector selector, CancellationToken cancellationToken = default)
            => Task.FromResult(_attachment);

        public Task DetachAsync(AttachmentId attachmentId, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<IReadOnlyList<ModuleInfo>> ListModulesAsync(AttachmentId attachmentId, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<IReadOnlyList<MemoryRegionInfo>> ListRegionsAsync(AttachmentId attachmentId, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<ResolvePointerResult> ResolvePointerAsync(ResolvePointerRequest request, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<ResolvePointerResult> ResolveModulePointerAsync(ResolveModulePointerRequest request, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<PatternScanResult> ScanPatternAsync(PatternScanRequest request, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<ReadMemoryResult> ReadAsync(ReadMemoryRequest request, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<WriteMemoryResult> WriteAsync(WriteMemoryRequest request, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();
    }

    private sealed class FakeFreezeCoordinator : IProcessFreezeCoordinator
    {
        public Task<FreezeInfo> FreezeAsync(FreezeRequest request, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<IReadOnlyList<FreezeInfo>> ListAsync(CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<bool> UnfreezeAsync(FreezeId freezeId, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<int> UnfreezeByAttachmentAsync(AttachmentId attachmentId, CancellationToken cancellationToken = default)
            => Task.FromResult(0);
    }

    private sealed class FakeClock(DateTimeOffset timestampUtc) : ISystemClock
    {
        private DateTimeOffset _timestampUtc = timestampUtc;

        public DateTimeOffset UtcNow => _timestampUtc;

        public void Advance(TimeSpan delta)
            => _timestampUtc = _timestampUtc.Add(delta);
    }
}