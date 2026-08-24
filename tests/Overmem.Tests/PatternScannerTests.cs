using Overmem.Windows.Memory;

namespace Overmem.Tests;

public sealed class PatternScannerTests
{
    [Fact]
    public void ParseCreatesWildcardMask()
    {
        var pattern = PatternScanner.Parse("DE AD ?? EF");

        Assert.Equal([0xDE, 0xAD, 0x00, 0xEF], pattern.Bytes);
        Assert.Equal([true, true, false, true], pattern.Mask);
    }

    [Fact]
    public void FindMatchesReturnsExpectedOffsets()
    {
        var pattern = PatternScanner.Parse("AA BB ?? DD");
        var buffer = new byte[] { 0x00, 0xAA, 0xBB, 0xCC, 0xDD, 0xAA, 0xBB, 0x99, 0xDD };

        var matches = PatternScanner.FindMatches(buffer, 0x1000, pattern, 10);

        Assert.Equal([0x1001UL, 0x1005UL], matches);
    }

    [Fact]
    public void ParseRejectsInvalidToken()
    {
        Assert.Throws<FormatException>(() => PatternScanner.Parse("GG"));
    }
}