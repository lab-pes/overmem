using Microsoft.Extensions.DependencyInjection;
using Overmem.Abstractions;
using Overmem.Abstractions.Freezing;
using Overmem.Abstractions.Memory;
using Overmem.Abstractions.Processes;
using Overmem.Application;
using Overmem.Application.Freezing;
using Overmem.Application.Tables;
using Overmem.Cli;
using Overmem.Runtime;
using Overmem.Runtime.Attachments;
using Overmem.Runtime.Diagnostics;

namespace Overmem.Tests;

public sealed class CliApplicationTableTests
{
    [Fact]
    public async Task TableLoadCommand_WritesDocumentJson()
    {
        var services = BuildServices(new TableGateway(), new InMemoryTableRepository(CreateDocument()));
        var stdout = new StringWriter();
        var stderr = new StringWriter();

        var exitCode = await CliApplication.RunAsync([
            "table-load",
            "--file", "table.json"
        ], services, stdout, stderr);

        Assert.Equal(0, exitCode);
        Assert.Contains("Ammo Table", stdout.ToString());
        Assert.Equal(string.Empty, stderr.ToString());
    }

    [Fact]
    public async Task TableRefreshCommand_WritesSnapshotJson()
    {
        var gateway = new TableGateway();
        gateway.SetInt32(0x1234, 999);

        var services = BuildServices(gateway, new InMemoryTableRepository(CreateDocument()));
        var stdout = new StringWriter();
        var stderr = new StringWriter();

        var exitCode = await CliApplication.RunAsync([
            "table-refresh",
            "--pid", "42",
            "--file", "table.json"
        ], services, stdout, stderr);

        Assert.Equal(0, exitCode);
        Assert.Contains("999", stdout.ToString());
        Assert.Equal(string.Empty, stderr.ToString());
    }

    private static ServiceProvider BuildServices(IProcessMemoryGateway gateway, IMemoryTableRepository repository)
    {
        var services = new ServiceCollection();
        services.AddSingleton<ISystemClock>(SystemClock.Instance);
        services.AddSingleton<IAttachmentSessionRegistry, InMemoryAttachmentSessionRegistry>();
        services.AddSingleton<IOperationJournal>(_ => new InMemoryOperationJournal());
        services.AddSingleton(gateway);
        services.AddSingleton<IProcessMemoryGateway>(gateway);
        services.AddSingleton<IProcessFreezeCoordinator, FakeFreezeCoordinator>();
        services.AddSingleton(repository);
        services.AddSingleton<IMemoryTableRepository>(repository);
        services.AddSingleton<MemoryTableService>();
        services.AddSingleton<ProcessMemoryApplicationService>();
        return services.BuildServiceProvider();
    }

    private static MemoryTableDocument CreateDocument()
        => new(
            MemoryTableDocument.CurrentSchemaVersion,
            "Ammo Table",
            [new MemoryTableEntry("ammo", "Ammo", MemoryValueKind.Int32, MemoryTableAddressKind.Absolute, AbsoluteAddress: 0x1234)]);

    private sealed class InMemoryTableRepository(MemoryTableDocument document) : IMemoryTableRepository
    {
        private MemoryTableDocument _document = document;

        public Task<MemoryTableDocument> LoadAsync(string filePath, CancellationToken cancellationToken = default)
            => Task.FromResult(_document);

        public Task SaveAsync(string filePath, MemoryTableDocument document, CancellationToken cancellationToken = default)
        {
            _document = document;
            return Task.CompletedTask;
        }
    }

    private sealed class TableGateway : IProcessMemoryGateway
    {
        private readonly AttachmentInfo _attachment = new(AttachmentId.New(), 42, "demo", ProcessArchitecture.X64);
        private readonly Dictionary<ulong, byte[]> _memory = [];

        public void SetInt32(ulong address, int value)
            => _memory[address] = BitConverter.GetBytes(value);

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
        {
            var bytes = _memory[request.Address];
            return Task.FromResult(new ReadMemoryResult(request.Address, request.ValueKind, BitConverter.ToInt32(bytes, 0).ToString(), bytes.Length));
        }

        public Task<WriteMemoryResult> WriteAsync(WriteMemoryRequest request, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();
    }

    private sealed class FakeFreezeCoordinator : IProcessFreezeCoordinator
    {
        public Task<FreezeInfo> FreezeAsync(FreezeRequest request, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<IReadOnlyList<FreezeInfo>> ListAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<FreezeInfo>>([]);

        public Task<bool> UnfreezeAsync(FreezeId freezeId, CancellationToken cancellationToken = default)
            => Task.FromResult(false);

        public Task<int> UnfreezeByAttachmentAsync(AttachmentId attachmentId, CancellationToken cancellationToken = default)
            => Task.FromResult(0);
    }
}