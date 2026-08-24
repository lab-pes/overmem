using Overmem.Abstractions.Memory;
using Overmem.Windows.Memory;

namespace Overmem.Tests;

public sealed class MemoryValueCodecTests
{
    [Fact]
    public void ParseAndFormatInt32RoundTrips()
    {
        var bytes = MemoryValueCodec.ParseValue(MemoryValueKind.Int32, "1337", 0);
        var value = MemoryValueCodec.FormatValue(MemoryValueKind.Int32, bytes);

        Assert.Equal("1337", value);
    }

    [Fact]
    public void ParseUtf8StringPadsToRequestedLength()
    {
        var bytes = MemoryValueCodec.ParseValue(MemoryValueKind.Utf8String, "abc", 5);

        Assert.Equal(5, bytes.Length);
        Assert.Equal("abc", MemoryValueCodec.FormatValue(MemoryValueKind.Utf8String, bytes));
    }

    [Fact]
    public void ResolveByteCountRejectsVariableLengthKindsWithoutExplicitSize()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => MemoryValueCodec.ResolveByteCount(MemoryValueKind.Bytes, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => MemoryValueCodec.ResolveByteCount(MemoryValueKind.Utf8String, 0));
    }
}