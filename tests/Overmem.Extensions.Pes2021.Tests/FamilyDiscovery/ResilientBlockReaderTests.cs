using System;
using System.Threading;
using System.Threading.Tasks;
using Overmem.Abstractions;
using Overmem.Abstractions.Processes;
using Overmem.Extensions.Pes2021.Players.FamilyDiscovery;
using Xunit;

namespace Overmem.Extensions.Pes2021.Tests.FamilyDiscovery;

public class ResilientBlockReaderTests
{
    [Fact]
    public async Task ReadRegionAsync_FallsBackToPageReadsOnBlockFailure()
    {
        var gateway = new FakeProcessMemoryGateway();
        
        // Simular 3 páginas (12KB)
        var mem1 = new byte[4096];
        var mem3 = new byte[4096];
        mem1[0] = 0xAA;
        mem3[0] = 0xBB;

        gateway.MapRegion(0x1000, mem1);
        // Omitimos a página do meio (0x2000) para forçar falha no read do bloco inteiro de 12KB
        gateway.MapRegion(0x3000, mem3);

        var reader = new ResilientBlockReader(gateway);
        var budget = FamilyScanBudget.Unlimited;

        var result = await reader.ReadRegionAsync(
            AttachmentId.New(),
            0x1000,
            0x4000,
            12288,
            budget,
            CancellationToken.None);

        Assert.Equal(1, result.PagesUnreadable);
        Assert.Equal(2, result.Blocks.Count);
        
        Assert.Equal(0x1000UL, result.Blocks[0].Address);
        Assert.Equal(4096, result.Blocks[0].Data.Length);
        Assert.Equal(0xAA, result.Blocks[0].Data[0]);

        Assert.Equal(0x3000UL, result.Blocks[1].Address);
        Assert.Equal(4096, result.Blocks[1].Data.Length);
        Assert.Equal(0xBB, result.Blocks[1].Data[0]);
    }

    [Fact]
    public async Task ReadRegionAsync_RespectsBudget()
    {
        var gateway = new FakeProcessMemoryGateway();
        gateway.MapRegion(0x1000, new byte[8192]);

        var reader = new ResilientBlockReader(gateway);
        var budget = new FamilyScanBudget(4096, 0, 0, 0, 0); // Limite de 4KB

        var result = await reader.ReadRegionAsync(
            AttachmentId.New(),
            0x1000,
            0x3000,
            4096,
            budget,
            CancellationToken.None);

        Assert.Single(result.Blocks);
        Assert.Equal(0x1000UL, result.Blocks[0].Address);
        Assert.Equal(4096, result.Blocks[0].Data.Length);
    }
}
