using Overmem.Abstractions.Processes;
using Overmem.Application.Tables;
using Overmem.Tests.Support;
using Overmem.Windows.Processes;

namespace Overmem.Tests;

public sealed class MemoryTableIntegrationTests
{
    [Fact]
    public async Task RefreshResolvesMixedEntriesAgainstTestTarget()
    {
        await using var target = await TestTargetHost.StartAsync();
        using var gateway = new WindowsProcessMemoryGateway();
        var service = new MemoryTableService(gateway, new JsonMemoryTableRepository());

        var attachment = await gateway.AttachAsync(new ProcessSelector(ProcessId: target.Info.Pid));
        var document = new MemoryTableDocument(
            MemoryTableDocument.CurrentSchemaVersion,
            "Target",
            [
                new MemoryTableEntry("int32", "Int32", Overmem.Abstractions.Memory.MemoryValueKind.Int32, MemoryTableAddressKind.Absolute, AbsoluteAddress: target.Info.Values.Int32.Address),
                new MemoryTableEntry("utf8", "Utf8", Overmem.Abstractions.Memory.MemoryValueKind.Utf8String, MemoryTableAddressKind.Absolute, AbsoluteAddress: target.Info.Values.Utf8.Address, Size: target.Info.Values.Utf8.Size),
                new MemoryTableEntry("ptr", "Pointer", Overmem.Abstractions.Memory.MemoryValueKind.Int32, MemoryTableAddressKind.Pointer, BaseAddress: target.Info.Values.PointerChain.BaseAddress, Offsets: target.Info.Values.PointerChain.Offsets),
                new MemoryTableEntry("modulePtr", "Module Pointer", Overmem.Abstractions.Memory.MemoryValueKind.Int32, MemoryTableAddressKind.ModulePointer, ModuleName: target.Info.Values.ModulePointerChain.ModuleName, BaseOffset: target.Info.Values.ModulePointerChain.BaseOffset, Offsets: target.Info.Values.ModulePointerChain.Offsets)
            ]);

        var snapshot = await service.RefreshAsync(attachment.AttachmentId, document);

        Assert.Equal(4, snapshot.Entries.Count);
        Assert.All(snapshot.Entries, entry => Assert.Null(entry.ErrorMessage));
        Assert.Contains(snapshot.Entries, entry => entry.EntryId == "int32" && entry.Value == "1337");
        Assert.Contains(snapshot.Entries, entry => entry.EntryId == "utf8" && entry.Value == target.Info.Values.Utf8.Value);
        Assert.Contains(snapshot.Entries, entry => entry.EntryId == "ptr" && entry.Value == "1337");
        Assert.Contains(snapshot.Entries, entry => entry.EntryId == "modulePtr" && entry.Value == "1337");
    }
}