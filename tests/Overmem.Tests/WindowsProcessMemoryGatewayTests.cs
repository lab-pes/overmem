using Overmem.Abstractions.Memory;
using Overmem.Abstractions.Processes;
using Overmem.Tests.Support;
using Overmem.Windows.Processes;

namespace Overmem.Tests;

public sealed class WindowsProcessMemoryGatewayTests
{
    [Fact]
    public async Task AttachReadAndWriteInt32Value()
    {
        await using var target = await TestTargetHost.StartAsync();
        using var gateway = new WindowsProcessMemoryGateway();

        var attachment = await gateway.AttachAsync(new ProcessSelector(ProcessId: target.Info.Pid));

        var initial = await gateway.ReadAsync(new ReadMemoryRequest(
            attachment.AttachmentId,
            target.Info.Values.Int32.Address,
            MemoryValueKind.Int32));

        Assert.Equal("1337", initial.Value);

        var write = await gateway.WriteAsync(new WriteMemoryRequest(
            attachment.AttachmentId,
            target.Info.Values.Int32.Address,
            MemoryValueKind.Int32,
            "9001"));

        Assert.Equal(sizeof(int), write.BytesWritten);

        var updated = await gateway.ReadAsync(new ReadMemoryRequest(
            attachment.AttachmentId,
            target.Info.Values.Int32.Address,
            MemoryValueKind.Int32));

        Assert.Equal("9001", updated.Value);
    }

    [Fact]
    public async Task ReadUtf8StringValue()
    {
        await using var target = await TestTargetHost.StartAsync();
        using var gateway = new WindowsProcessMemoryGateway();

        var attachment = await gateway.AttachAsync(new ProcessSelector(ProcessId: target.Info.Pid));
        var result = await gateway.ReadAsync(new ReadMemoryRequest(
            attachment.AttachmentId,
            target.Info.Values.Utf8.Address,
            MemoryValueKind.Utf8String,
            target.Info.Values.Utf8.Size));

        Assert.Equal(target.Info.Values.Utf8.Value, result.Value);
    }

    [Fact]
    public async Task ResolvePointerChainToKnownAddress()
    {
        await using var target = await TestTargetHost.StartAsync();
        using var gateway = new WindowsProcessMemoryGateway();

        var attachment = await gateway.AttachAsync(new ProcessSelector(ProcessId: target.Info.Pid));
        var result = await gateway.ResolvePointerAsync(new ResolvePointerRequest(
            attachment.AttachmentId,
            target.Info.Values.PointerChain.BaseAddress,
            target.Info.Values.PointerChain.Offsets));

        Assert.Equal(target.Info.Values.PointerChain.ResolvedAddress, result.ResolvedAddress);
    }

    [Fact]
    public async Task ListRegionsIncludesRegionContainingKnownAddress()
    {
        await using var target = await TestTargetHost.StartAsync();
        using var gateway = new WindowsProcessMemoryGateway();

        var attachment = await gateway.AttachAsync(new ProcessSelector(ProcessId: target.Info.Pid));
        var regions = await gateway.ListRegionsAsync(attachment.AttachmentId);

        Assert.Contains(regions, region =>
            region.BaseAddress <= target.Info.Values.Int32.Address &&
            target.Info.Values.Int32.Address < region.BaseAddress + region.RegionSize);
    }

    [Fact]
    public async Task ResolveModulePointerChainToKnownAddress()
    {
        await using var target = await TestTargetHost.StartAsync();
        using var gateway = new WindowsProcessMemoryGateway();

        var attachment = await gateway.AttachAsync(new ProcessSelector(ProcessId: target.Info.Pid));
        var result = await gateway.ResolveModulePointerAsync(new ResolveModulePointerRequest(
            attachment.AttachmentId,
            target.Info.Values.ModulePointerChain.ModuleName,
            target.Info.Values.ModulePointerChain.BaseOffset,
            target.Info.Values.ModulePointerChain.Offsets));

        Assert.Equal(target.Info.Values.ModulePointerChain.ResolvedAddress, result.ResolvedAddress);
    }

    [Fact]
    public async Task ScanPatternFindsKnownSequence()
    {
        await using var target = await TestTargetHost.StartAsync();
        using var gateway = new WindowsProcessMemoryGateway();

        var attachment = await gateway.AttachAsync(new ProcessSelector(ProcessId: target.Info.Pid));
        var result = await gateway.ScanPatternAsync(new PatternScanRequest(
            attachment.AttachmentId,
            target.Info.Values.Pattern.Pattern,
            MaxResults: 5));

        Assert.Contains(target.Info.Values.Pattern.Address, result.Addresses);
    }

    [Fact]
    public async Task ScanPatternSupportsWildcards()
    {
        await using var target = await TestTargetHost.StartAsync();
        using var gateway = new WindowsProcessMemoryGateway();

        var attachment = await gateway.AttachAsync(new ProcessSelector(ProcessId: target.Info.Pid));
        var result = await gateway.ScanPatternAsync(new PatternScanRequest(
            attachment.AttachmentId,
            target.Info.Values.Pattern.WildcardPattern,
            MaxResults: 5));

        Assert.Contains(target.Info.Values.Pattern.Address, result.Addresses);
    }
}