using Overmem.Abstractions;
using Overmem.Abstractions.Freezing;
using Overmem.Abstractions.Memory;
using Overmem.Abstractions.Processes;
using Overmem.Application;
using Overmem.Application.Freezing;
using Overmem.Windows.Memory;

namespace Overmem.Tests;

public sealed class ProcessFreezeCoordinatorTests
{
    [Fact]
    public async Task FreezeRestoresMutatedAbsoluteValueUntilUnfrozen()
    {
        var attachmentId = new AttachmentId(Guid.NewGuid());
        var gateway = new FakeGateway(attachmentId);
        gateway.SetInt32(0x1000, 1);

        using var coordinator = new ProcessFreezeCoordinator(gateway);
        var freeze = await coordinator.FreezeAsync(new FreezeRequest(
            attachmentId,
            new AbsoluteAddressSource(0x1000),
            MemoryValueKind.Int32,
            "777",
            IntervalMs: 20));

        gateway.SetInt32(0x1000, 9);
        await WaitUntilAsync(() => gateway.GetInt32(0x1000) == 777, TimeSpan.FromSeconds(1));

        Assert.True(await coordinator.UnfreezeAsync(freeze.FreezeId));

        gateway.SetInt32(0x1000, 55);
        await Task.Delay(80);
        Assert.Equal(55, gateway.GetInt32(0x1000));
    }

    [Fact]
    public async Task DetachStopsActiveFreezes()
    {
        var attachmentId = new AttachmentId(Guid.NewGuid());
        var gateway = new FakeGateway(attachmentId);
        gateway.SetInt32(0x2000, 1);

        using var coordinator = new ProcessFreezeCoordinator(gateway);
        var service = new ProcessMemoryApplicationService(gateway, coordinator);
        await service.FreezeAsync(new FreezeRequest(
            attachmentId,
            new AbsoluteAddressSource(0x2000),
            MemoryValueKind.Int32,
            "888",
            IntervalMs: 20));

        gateway.SetInt32(0x2000, 3);
        await WaitUntilAsync(() => gateway.GetInt32(0x2000) == 888, TimeSpan.FromSeconds(1));

        await service.DetachAsync(attachmentId);
        gateway.SetInt32(0x2000, 42);

        await Task.Delay(80);
        Assert.Equal(42, gateway.GetInt32(0x2000));
    }

    private static async Task WaitUntilAsync(Func<bool> condition, TimeSpan timeout)
    {
        var start = DateTime.UtcNow;
        while (!condition())
        {
            if (DateTime.UtcNow - start > timeout)
            {
                throw new TimeoutException("Condition was not satisfied before the timeout elapsed.");
            }

            await Task.Delay(10);
        }
    }

    private sealed class FakeGateway(AttachmentId attachmentId) : IProcessMemoryGateway
    {
        private readonly Dictionary<ulong, byte[]> _memory = [];

        public void SetInt32(ulong address, int value) => _memory[address] = BitConverter.GetBytes(value);

        public int GetInt32(ulong address) => BitConverter.ToInt32(_memory[address]);

        public Task<AttachmentInfo> AttachAsync(ProcessSelector selector, CancellationToken cancellationToken = default) => throw new NotImplementedException();

        public Task DetachAsync(AttachmentId attachmentId, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<IReadOnlyList<ModuleInfo>> ListModulesAsync(AttachmentId attachmentId, CancellationToken cancellationToken = default) => throw new NotImplementedException();

        public Task<IReadOnlyList<MemoryRegionInfo>> ListRegionsAsync(AttachmentId attachmentId, CancellationToken cancellationToken = default) => throw new NotImplementedException();

        public Task<ResolvePointerResult> ResolvePointerAsync(ResolvePointerRequest request, CancellationToken cancellationToken = default)
            => Task.FromResult(new ResolvePointerResult(request.BaseAddress, request.Offsets, request.BaseAddress));

        public Task<ResolvePointerResult> ResolveModulePointerAsync(ResolveModulePointerRequest request, CancellationToken cancellationToken = default)
            => Task.FromResult(new ResolvePointerResult(0, request.Offsets, 0));

        public Task<PatternScanResult> ScanPatternAsync(PatternScanRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();

        public Task<ReadMemoryResult> ReadAsync(ReadMemoryRequest request, CancellationToken cancellationToken = default)
        {
            var bytes = _memory[request.Address];
            return Task.FromResult(new ReadMemoryResult(
                request.Address,
                request.ValueKind,
                MemoryValueCodec.FormatValue(request.ValueKind, bytes),
                bytes.Length));
        }

        public Task<WriteMemoryResult> WriteAsync(WriteMemoryRequest request, CancellationToken cancellationToken = default)
        {
            if (request.AttachmentId != attachmentId)
            {
                throw new InvalidOperationException("Unexpected attachment identifier.");
            }

            var bytes = MemoryValueCodec.ParseValue(request.ValueKind, request.Value, request.Size);
            _memory[request.Address] = bytes;
            return Task.FromResult(new WriteMemoryResult(request.Address, request.ValueKind, bytes.Length));
        }
    }
}