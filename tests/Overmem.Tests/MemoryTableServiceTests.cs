using Overmem.Abstractions;
using Overmem.Abstractions.Memory;
using Overmem.Abstractions.Processes;
using Overmem.Application.Tables;

namespace Overmem.Tests;

public sealed class MemoryTableServiceTests
{
    [Fact]
    public void ValidateEntryRejectsMissingModuleName()
    {
        var entry = new MemoryTableEntry("ammo", "Ammo", MemoryValueKind.Int32, MemoryTableAddressKind.ModulePointer);

        Assert.Throws<ArgumentException>(() => MemoryTableService.ValidateEntry(entry));
    }

    [Fact]
    public async Task RefreshReturnsCurrentValueSnapshot()
    {
        var attachmentId = new AttachmentId(Guid.NewGuid());
        var gateway = new FakeGateway(attachmentId);
        gateway.SetInt32(0x1234, 321);
        var service = new MemoryTableService(gateway, new JsonMemoryTableRepository());
        var document = new MemoryTableDocument(
            MemoryTableDocument.CurrentSchemaVersion,
            "Test",
            [new MemoryTableEntry("value", "Value", MemoryValueKind.Int32, MemoryTableAddressKind.Absolute, AbsoluteAddress: 0x1234)]);

        var snapshot = await service.RefreshAsync(attachmentId, document);

        Assert.Single(snapshot.Entries);
        Assert.Equal("321", snapshot.Entries[0].Value);
        Assert.Null(snapshot.Entries[0].ErrorMessage);
    }

    private sealed class FakeGateway(AttachmentId attachmentId) : IProcessMemoryGateway
    {
        private readonly Dictionary<ulong, byte[]> _memory = [];

        public void SetInt32(ulong address, int value) => _memory[address] = BitConverter.GetBytes(value);

        public Task<AttachmentInfo> AttachAsync(ProcessSelector selector, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task DetachAsync(AttachmentId attachmentId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<IReadOnlyList<ModuleInfo>> ListModulesAsync(AttachmentId attachmentId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<IReadOnlyList<MemoryRegionInfo>> ListRegionsAsync(AttachmentId attachmentId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<ResolvePointerResult> ResolvePointerAsync(ResolvePointerRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<ResolvePointerResult> ResolveModulePointerAsync(ResolveModulePointerRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<PatternScanResult> ScanPatternAsync(PatternScanRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();

        public Task<ReadMemoryResult> ReadAsync(ReadMemoryRequest request, CancellationToken cancellationToken = default)
        {
            if (request.AttachmentId != attachmentId)
            {
                throw new InvalidOperationException("Unexpected attachment identifier.");
            }

            var bytes = _memory[request.Address];
            return Task.FromResult(new ReadMemoryResult(request.Address, request.ValueKind, BitConverter.ToInt32(bytes).ToString(), bytes.Length));
        }

        public Task<WriteMemoryResult> WriteAsync(WriteMemoryRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    }
}