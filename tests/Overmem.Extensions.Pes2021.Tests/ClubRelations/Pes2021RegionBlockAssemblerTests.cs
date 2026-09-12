using Overmem.Extensions.Pes2021.ClubRelations;

namespace Overmem.Extensions.Pes2021.Tests.ClubRelations;

public sealed class Pes2021RegionBlockAssemblerTests
{
    [Fact]
    public void AssembleContiguousPrefix_MergesOverlapWithoutShiftingOffsets()
    {
        var blocks = new[]
        {
            Block(offset: 0, [0, 1, 2, 3, 4, 5]),
            Block(offset: 4, [4, 5, 6, 7]),
        };

        var result = Pes2021RegionBlockAssembler.AssembleContiguousPrefix(blocks);

        Assert.Equal(new byte[] { 0, 1, 2, 3, 4, 5, 6, 7 }, result);
    }

    [Fact]
    public void AssembleContiguousPrefix_RejectsDisagreeingOverlap()
    {
        var blocks = new[]
        {
            Block(offset: 0, [0, 1, 2, 3]),
            Block(offset: 2, [9, 3, 4]),
        };

        var exception = Assert.Throws<InvalidDataException>(
            () => Pes2021RegionBlockAssembler.AssembleContiguousPrefix(blocks));

        Assert.Contains("disagree", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AssembleContiguousPrefix_RejectsGap()
    {
        var blocks = new[]
        {
            Block(offset: 0, [0, 1]),
            Block(offset: 3, [3, 4]),
        };

        Assert.Throws<InvalidDataException>(
            () => Pes2021RegionBlockAssembler.AssembleContiguousPrefix(blocks));
    }

    private static RegionBlockRead Block(ulong offset, byte[] payload)
        => new(0x1000, offset, payload.Length, string.Empty, payload.Length, payload);
}
