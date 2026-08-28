using System.Collections.Generic;
using Overmem.Abstractions.Memory;
using Overmem.Extensions.Pes2021.ClubRelations;

namespace Overmem.Extensions.Pes2021.Tests.ClubRelations;

public sealed class Pes2021PrivateRegionFilterTests
{
    [Fact]
    public void FilterReadablePrivate_KeepsOnlyReadablePrivateNonExecutable()
    {
        var regions = new List<MemoryRegionInfo>
        {
            new(0x1000, 0x2000, "Commit", "ReadWrite", "Private", IsReadable: true, IsWritable: true, IsExecutable: false),
            new(0x3000, 0x1000, "Commit", "ReadOnly", "Private", IsReadable: true, IsWritable: false, IsExecutable: false),
            new(0x4000, 0x1000, "Commit", "ExecuteRead", "Private", IsReadable: true, IsWritable: false, IsExecutable: true),
            new(0x5000, 0x1000, "Commit", "ReadWrite", "Mapped", IsReadable: true, IsWritable: true, IsExecutable: false),
            new(0x6000, 0x1000, "Commit", "ReadWrite", "Private", IsReadable: false, IsWritable: true, IsExecutable: false),
            new(0x7000, 0x1000, "Commit", "Guard", "Private", IsReadable: true, IsWritable: false, IsExecutable: false),
            new(0x8000, 0x1000, "Commit", "NoAccess", "Private", IsReadable: true, IsWritable: false, IsExecutable: false),
        };

        var filtered = Pes2021PrivateRegionFilter.FilterReadablePrivate(regions);

        Assert.Equal(2, filtered.Count);
        Assert.Equal(0x1000UL, filtered[0].BaseAddress);
        Assert.Equal(0x3000UL, filtered[1].BaseAddress);
    }

    [Fact]
    public void FilterReadablePrivate_ReturnsEmptyWhenNoMatch()
    {
        var regions = new List<MemoryRegionInfo>
        {
            new(0x1000, 0x2000, "Commit", "ExecuteRead", "Private", IsReadable: true, IsWritable: false, IsExecutable: true),
        };

        var filtered = Pes2021PrivateRegionFilter.FilterReadablePrivate(regions);

        Assert.Empty(filtered);
    }
}
