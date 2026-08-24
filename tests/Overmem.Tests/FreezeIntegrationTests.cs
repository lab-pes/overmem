using Overmem.Abstractions;
using Overmem.Abstractions.Freezing;
using Overmem.Abstractions.Memory;
using Overmem.Abstractions.Processes;
using Overmem.Application;
using Overmem.Application.Freezing;
using Overmem.Tests.Support;
using Overmem.Windows.Processes;

namespace Overmem.Tests;

public sealed class FreezeIntegrationTests
{
    [Fact]
    public async Task FreezeRestoresMutatingValueAndUnfreezeReleasesIt()
    {
        await using var target = await TestTargetHost.StartAsync();
        using var gateway = new WindowsProcessMemoryGateway();
        using var coordinator = new ProcessFreezeCoordinator(gateway);
        var service = new ProcessMemoryApplicationService(gateway, coordinator);

        var attachment = await service.AttachAsync(new ProcessSelector(ProcessId: target.Info.Pid));
        var freeze = await service.FreezeAsync(new FreezeRequest(
            attachment.AttachmentId,
            new AbsoluteAddressSource(target.Info.Values.MutableInt.Address),
            MemoryValueKind.Int32,
            target.Info.Values.MutableInt.FrozenValue.ToString(),
            IntervalMs: 10));

        await WaitUntilAsync(async () =>
        {
            var value = await gateway.ReadAsync(new ReadMemoryRequest(
                attachment.AttachmentId,
                target.Info.Values.MutableInt.Address,
                MemoryValueKind.Int32));
            return value.Value == target.Info.Values.MutableInt.FrozenValue.ToString();
        }, TimeSpan.FromSeconds(1));

        Assert.True(await service.UnfreezeAsync(freeze.FreezeId));

        await Task.Delay(target.Info.Values.MutableInt.MutationIntervalMs * 3);
        var after = await gateway.ReadAsync(new ReadMemoryRequest(
            attachment.AttachmentId,
            target.Info.Values.MutableInt.Address,
            MemoryValueKind.Int32));

        Assert.NotEqual(target.Info.Values.MutableInt.FrozenValue.ToString(), after.Value);
    }

    private static async Task WaitUntilAsync(Func<Task<bool>> condition, TimeSpan timeout)
    {
        var start = DateTime.UtcNow;
        while (!await condition())
        {
            if (DateTime.UtcNow - start > timeout)
            {
                throw new TimeoutException("Condition was not satisfied before the timeout elapsed.");
            }

            await Task.Delay(10);
        }
    }
}