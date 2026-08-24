using Overmem.Abstractions.Memory;
using Overmem.Abstractions.Processes;
using Overmem.Abstractions.Search;
using Overmem.Search;
using Overmem.Tests.Support;
using Overmem.Windows.Processes;

namespace Overmem.Tests;

public sealed class ValueSearchIntegrationTests
{
    [Fact]
    public async Task ExactSearch_FindsKnownInt32Address()
    {
        await using var target = await TestTargetHost.StartAsync();
        using var gateway = new WindowsProcessMemoryGateway();
        var searchService = new ValueSearchService(gateway);

        var attachment = await gateway.AttachAsync(new ProcessSelector(ProcessId: target.Info.Pid));
        var result = await searchService.StartExactSearchAsync(new StartValueSearchRequest(
            attachment.AttachmentId,
            MemoryValueKind.Int32,
            "1337",
            Alignment: sizeof(int),
            MaxResults: 256));

        Assert.Contains(result.Matches, match => match.Address == target.Info.Values.Int32.Address);
    }

    [Fact]
    public async Task RefineSearch_Changed_CanTrackMutableValue()
    {
        await using var target = await TestTargetHost.StartAsync();
        using var gateway = new WindowsProcessMemoryGateway();
        var searchService = new ValueSearchService(gateway);

        var attachment = await gateway.AttachAsync(new ProcessSelector(ProcessId: target.Info.Pid));
        var started = await searchService.StartExactSearchAsync(new StartValueSearchRequest(
            attachment.AttachmentId,
            MemoryValueKind.Int32,
            "1337",
            Alignment: sizeof(int),
            MaxResults: 512));

        await gateway.WriteAsync(new WriteMemoryRequest(
            attachment.AttachmentId,
            target.Info.Values.Int32.Address,
            MemoryValueKind.Int32,
            "7331"));

        var refined = await searchService.RefineAsync(new RefineValueSearchRequest(started.SessionId, ValueSearchComparison.Changed));

        Assert.Contains(refined.Matches, match => match.Address == target.Info.Values.Int32.Address && match.Value == "7331");
    }

    [Fact]
    public async Task RefineSearch_Increased_CanTrackRaisedValue()
    {
        await using var target = await TestTargetHost.StartAsync();
        using var gateway = new WindowsProcessMemoryGateway();
        var searchService = new ValueSearchService(gateway);

        var attachment = await gateway.AttachAsync(new ProcessSelector(ProcessId: target.Info.Pid));
        var started = await searchService.StartExactSearchAsync(new StartValueSearchRequest(
            attachment.AttachmentId,
            MemoryValueKind.Int32,
            "1337",
            Alignment: sizeof(int),
            MaxResults: 512));

        await gateway.WriteAsync(new WriteMemoryRequest(
            attachment.AttachmentId,
            target.Info.Values.Int32.Address,
            MemoryValueKind.Int32,
            "9000"));

        var refined = await searchService.RefineAsync(new RefineValueSearchRequest(started.SessionId, ValueSearchComparison.Increased));

        Assert.Contains(refined.Matches, match => match.Address == target.Info.Values.Int32.Address && match.Value == "9000");
    }
}