using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Overmem.Abstractions;
using Overmem.Extensions.Pes2021.Players;
using Overmem.Extensions.Pes2021.Players.FamilyDiscovery;
using Overmem.Abstractions.Memory;
using Overmem.Abstractions.Processes;
using Xunit;

namespace Overmem.Extensions.Pes2021.Tests.FamilyDiscovery;

public class MissingComplianceTests
{
    private static readonly Pes2021PlayerProfile Profile = Pes2021PlayerProfileDefaults.GetOrLoad();

    [Fact]
    public async Task ZeroWriteAsyncCalls_FDSNeverModifiesMemory()
    {
        var gatewayMock = new Mock<IProcessMemoryGateway>();
        gatewayMock.Setup(g => g.ReadAsync(It.IsAny<ReadMemoryRequest>(), It.IsAny<CancellationToken>()))
                   .ReturnsAsync(new ReadMemoryResult(0, MemoryValueKind.Bytes, "00", 1));
        gatewayMock.Setup(g => g.ListRegionsAsync(It.IsAny<AttachmentId>(), It.IsAny<CancellationToken>()))
                   .ReturnsAsync(new List<MemoryRegionInfo>());

        var scanner = new MultiAnchorScanner(gatewayMock.Object);
        var budget = new FamilyScanBudget(1024 * 1024, 100, 1000, 100, 1000);
        
        var fingerprints = new FingerprintSet(Profile.ProfileId, Profile.ProfileVersion, new List<PlayerFingerprint>(), new List<int>());
        await scanner.ScanAsync(new AttachmentId(Guid.NewGuid()), fingerprints, Profile, RegionPolicy.All, budget, null, CancellationToken.None);

        gatewayMock.Verify(g => g.WriteAsync(It.IsAny<WriteMemoryRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ScanAsync_RespectsCancellation()
    {
        var gatewayMock = new Mock<IProcessMemoryGateway>();
        var tcs = new TaskCompletionSource<ReadMemoryResult>();
        
        gatewayMock.Setup(g => g.ReadAsync(It.IsAny<ReadMemoryRequest>(), It.IsAny<CancellationToken>()))
                   .Returns(tcs.Task);
        gatewayMock.Setup(g => g.ListRegionsAsync(It.IsAny<AttachmentId>(), It.IsAny<CancellationToken>()))
                   .ReturnsAsync(new List<MemoryRegionInfo>());

        var scanner = new MultiAnchorScanner(gatewayMock.Object);
        var budget = new FamilyScanBudget(1024, 1, 1, 1, 0);
        
        var cts = new CancellationTokenSource();
        cts.Cancel(); // Pre-cancel

        var fingerprints = new FingerprintSet(Profile.ProfileId, Profile.ProfileVersion, new List<PlayerFingerprint>(), new List<int>());
        await Assert.ThrowsAsync<OperationCanceledException>(async () => 
            await scanner.ScanAsync(new AttachmentId(Guid.NewGuid()), fingerprints, Profile, RegionPolicy.All, budget, null, cts.Token)
        );
    }

    [Fact]
    public async Task ResilientBlockReader_PartialRead_IsCounted()
    {
        var gatewayMock = new Mock<IProcessMemoryGateway>();
        
        // Return 1 byte instead of the requested chunk
        gatewayMock.Setup(g => g.ReadAsync(It.Is<ReadMemoryRequest>(r => r.Size == 4096), It.IsAny<CancellationToken>()))
                   .ReturnsAsync(new ReadMemoryResult(0x1000, MemoryValueKind.Bytes, "AA", 1));

        // Let the fallback page read also return 1 byte
        gatewayMock.Setup(g => g.ReadAsync(It.Is<ReadMemoryRequest>(r => r.Size != 4096), It.IsAny<CancellationToken>()))
                   .ReturnsAsync(new ReadMemoryResult(0, MemoryValueKind.Bytes, "BB", 1));

        var reader = new ResilientBlockReader(gatewayMock.Object);
        var budget = new FamilyScanBudget(4096, 1, 1, 1, 0);
        
        var result = await reader.ReadRegionAsync(new AttachmentId(Guid.NewGuid()), 0x1000, 0x2000, 4096, budget, CancellationToken.None);

        Assert.True(result.PagesPartialRead > 0);
    }
}
