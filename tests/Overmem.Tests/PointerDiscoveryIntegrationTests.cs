using Overmem.Abstractions.Memory;
using Overmem.Abstractions.Processes;
using Overmem.Application.Pointers;
using Overmem.Runtime.Attachments;
using Overmem.Tests.Support;
using Overmem.Windows.Processes;

namespace Overmem.Tests;

public sealed class PointerDiscoveryIntegrationTests
{
    [Fact]
    public async Task DiscoverAsync_FindsKnownTwoLevelPointerChain()
    {
        await using var target = await TestTargetHost.StartAsync();
        using var gateway = new WindowsProcessMemoryGateway();
        var sessionRegistry = new InMemoryAttachmentSessionRegistry();

        var attachment = await gateway.AttachAsync(new ProcessSelector(ProcessId: target.Info.Pid));
        sessionRegistry.Register(attachment, DateTimeOffset.UtcNow);

        var service = new PointerDiscoveryService(gateway, sessionRegistry);
        var result = await service.DiscoverAsync(new DiscoverPointersRequest(
            attachment.AttachmentId,
            target.Info.Values.Int32.Address,
            MaxDepth: 2,
            MaxResults: 256));

        Assert.Contains(result.Candidates, candidate =>
            candidate.BaseAddress == target.Info.Values.PointerChain.BaseAddress &&
            candidate.Offsets.SequenceEqual(target.Info.Values.PointerChain.Offsets) &&
            candidate.IsValidated &&
            candidate.ResolvedAddress == target.Info.Values.Int32.Address);
    }
}