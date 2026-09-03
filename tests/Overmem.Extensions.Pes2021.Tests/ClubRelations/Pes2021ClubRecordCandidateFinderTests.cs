using System.Buffers.Binary;
using System.Text;
using Overmem.Abstractions.Memory;
using Overmem.Extensions.Pes2021.ClubRelations;

namespace Overmem.Extensions.Pes2021.Tests.ClubRelations;

public sealed class Pes2021ClubRecordCandidateFinderTests
{
    [Fact]
    public void FindCandidates_PreservesCompositeIdentitiesSharingTeamId()
    {
        const int teamId = 32784;
        var buffer = new byte[512];
        BinaryPrimitives.WriteUInt16LittleEndian(buffer.AsSpan(100, 2), teamId);
        Encoding.UTF8.GetBytes("SANTOS A").CopyTo(buffer, 120);
        Encoding.UTF8.GetBytes("SANTOS B").CopyTo(buffer, 220);
        var catalog = new[]
        {
            Row(teamId, 313, "SANTOS A"),
            Row(teamId, 999, "SANTOS B"),
        };

        var result = Pes2021ClubRecordCandidateFinder.FindCandidates(
            [Snapshot(buffer)], catalog, new Dictionary<(int, int), string>());

        Assert.Collection(
            result,
            first => Assert.Equal((teamId, 313), (first.Candidate.TeamId, first.Candidate.SecondaryId)),
            second => Assert.Equal((teamId, 999), (second.Candidate.TeamId, second.Candidate.SecondaryId)));
    }

    [Fact]
    public void FindCandidates_DoesNotSilentlyDiscardAmbiguousCompositeRows()
    {
        const int teamId = 32784;
        var buffer = new byte[256];
        BinaryPrimitives.WriteUInt16LittleEndian(buffer.AsSpan(40, 2), teamId);
        Encoding.UTF8.GetBytes("SANTOS").CopyTo(buffer, 60);
        var catalog = new[]
        {
            Row(teamId, 313, "SANTOS"),
            Row(teamId, 777, "SANTOS"),
        };

        var result = Pes2021ClubRecordCandidateFinder.FindCandidates(
            [Snapshot(buffer)], catalog, new Dictionary<(int, int), string>());

        Assert.Equal(2, result.Count);
        Assert.Equal(new[] { 313, 777 }, result.Select(hit => hit.Candidate.SecondaryId));
    }

    [Fact]
    public void FindCandidates_ReportsAddressesFromTrueRegionOffsets()
    {
        const int teamId = 32784;
        var buffer = new byte[128];
        BinaryPrimitives.WriteUInt16LittleEndian(buffer.AsSpan(30, 2), teamId);
        Encoding.UTF8.GetBytes("SANTOS").CopyTo(buffer, 50);

        var hit = Assert.Single(Pes2021ClubRecordCandidateFinder.FindCandidates(
            [Snapshot(buffer)], [Row(teamId, 313, "SANTOS")],
            new Dictionary<(int, int), string>()));

        Assert.Equal(0x1032UL, hit.Candidate.NameMatchAddress);
        Assert.Equal(0x101EUL, hit.Candidate.IdMatchAddress);
        Assert.Equal(50, hit.Candidate.NameRelativeOffset);
        Assert.Equal(20, hit.Candidate.IdRelativeOffset);
    }

    private static RegionBlockSnapshot Snapshot(byte[] buffer)
    {
        var region = new MemoryRegionInfo(
            0x1000, (ulong)buffer.Length, "Commit", "ReadWrite", "Private",
            IsReadable: true, IsWritable: true, IsExecutable: false);
        return new RegionBlockSnapshot(region, [], buffer);
    }

    private static Pes2021ClubCatalogRow Row(int teamId, int secondaryId, string name)
        => new(teamId, secondaryId, name, string.Empty, null, 0, 0, 0, "fixture", "fixture");
}
