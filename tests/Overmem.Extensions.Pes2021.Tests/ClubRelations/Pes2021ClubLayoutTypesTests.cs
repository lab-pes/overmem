using System.Collections.Generic;
using Overmem.Extensions.Pes2021.ClubRelations;

namespace Overmem.Extensions.Pes2021.Tests.ClubRelations;

public sealed class Pes2021ClubLayoutTypesTests
{
    [Fact]
    public void WindowEntry_ExposesAllSizes()
    {
        var entry = new Pes2021ClubLayoutWindowEntry(0x10, 0x42, 0x4243, 0x42434445, unchecked((int)0x42434445));
        Assert.Equal(0x10, entry.Offset);
        Assert.Equal((byte)0x42, entry.AsByte);
        Assert.Equal((ushort)0x4243, entry.AsUInt16);
        Assert.Equal(0x42434445u, entry.AsUInt32);
        Assert.Equal(unchecked((int)0x42434445), entry.AsInt32);
    }

    [Fact]
    public void LayoutField_DefaultStabilityIsUnknown()
    {
        var field = new Pes2021ClubLayoutField(0x20, ClubLayoutFieldStability.Unknown, 0, 0, new int[0]);
        Assert.Equal(0x20, field.Offset);
        Assert.Equal(ClubLayoutFieldStability.Unknown, field.Stability);
        Assert.Empty(field.DistinctValues);
    }

    [Fact]
    public void Candidate_StoresWindowsAndSummary()
    {
        var candidate = new Pes2021ClubLayoutCandidate(
            32784,
            313,
            "SANTOS",
            0xEC6AF61UL,
            1,
            new List<Pes2021ClubLayoutWindow>(),
            new List<Pes2021ClubLayoutField>());
        Assert.Equal(32784, candidate.TeamId);
        Assert.Equal(313, candidate.SecondaryId);
        Assert.Equal("SANTOS", candidate.Name);
        Assert.Equal(0xEC6AF61UL, candidate.Address);
        Assert.Equal(1, candidate.ControlOrdinal);
        Assert.Empty(candidate.Windows);
        Assert.Empty(candidate.FieldSummary);
    }
}
