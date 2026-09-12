using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Overmem.Abstractions;
using Overmem.Abstractions.Processes;
using Overmem.Extensions.Pes2021.Players;
using Overmem.Extensions.Pes2021.Players.FamilyDiscovery;
using Overmem.Runtime;

namespace Overmem.Extensions.Pes2021.Tests.FamilyDiscovery;

public sealed class FamilyCatalogRoundTripTests
{
    [Fact]
    public async Task ExplicitCatalogPathsRoundTripAndCompareWithoutSessionIdInFilename()
    {
        var root = Path.Combine(Path.GetTempPath(), "overmem-catalog-" + Guid.NewGuid().ToString("N"));
        try
        {
            var before = Path.Combine(root, "before", "catalog.json");
            var after = Path.Combine(root, "after", "catalog.json");
            var diagnostics = new FamilyDiscoveryDiagnostics(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
                new Dictionary<string, int>(), new Dictionary<string, double>(), []);
            var result = new FamilyDiscoveryResult([], [], [], diagnostics);
            await FamilyCatalog.FromFile(before).SaveAsync(result, CancellationToken.None);
            await FamilyCatalog.FromFile(after).SaveAsync(result, CancellationToken.None);
            Assert.True(File.Exists(before));
            Assert.True(File.Exists(before + ".sha256"));
            Assert.NotNull(await FamilyCatalog.FromFile(before).LoadAsync(CancellationToken.None));

            var gateway = new Mock<IProcessMemoryGateway>(MockBehavior.Strict);
            var service = new Pes2021FamilyDiscoveryService(gateway.Object,
                NullLogger<Pes2021FamilyDiscoveryService>.Instance,
                new Pes2021PlayerAnchorFinder(gateway.Object, SystemClock.Instance));
            var comparison = await service.CompareSessionsAsync(new AttachmentId(Guid.NewGuid()), before, after, CancellationToken.None);
            Assert.Contains("Persistent Families: 0", comparison);
            Assert.Contains("New Families: 0", comparison);
            gateway.VerifyNoOtherCalls();
            await Assert.ThrowsAsync<FileNotFoundException>(() => service.CompareSessionsAsync(
                new AttachmentId(Guid.NewGuid()), before, Path.Combine(root, "missing.json"), CancellationToken.None));
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }
}
