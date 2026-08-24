using Overmem.Abstractions.Memory;
using System.Globalization;
using System.Text;

namespace Overmem.Search;

internal static class ValueSearchCodec
{
    public static string FormatValue(MemoryValueKind valueKind, byte[] buffer) => valueKind switch
    {
        MemoryValueKind.Bytes => Convert.ToHexString(buffer),
        MemoryValueKind.Int32 => BitConverter.ToInt32(buffer).ToString(CultureInfo.InvariantCulture),
        MemoryValueKind.Int64 => BitConverter.ToInt64(buffer).ToString(CultureInfo.InvariantCulture),
        MemoryValueKind.Float => BitConverter.ToSingle(buffer).ToString(CultureInfo.InvariantCulture),
        MemoryValueKind.Double => BitConverter.ToDouble(buffer).ToString(CultureInfo.InvariantCulture),
        MemoryValueKind.Utf8String => Encoding.UTF8.GetString(buffer).TrimEnd('\0'),
        MemoryValueKind.Utf16String => Encoding.Unicode.GetString(buffer).TrimEnd('\0'),
        _ => throw new ArgumentOutOfRangeException(nameof(valueKind)),
    };

    public static byte[] ParseExactValue(MemoryValueKind valueKind, string value, int explicitSize)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);

        return valueKind switch
        {
            MemoryValueKind.Bytes => ParseBytes(value, explicitSize),
            MemoryValueKind.Int32 => BitConverter.GetBytes(int.Parse(value, CultureInfo.InvariantCulture)),
            MemoryValueKind.Int64 => BitConverter.GetBytes(long.Parse(value, CultureInfo.InvariantCulture)),
            MemoryValueKind.Float => BitConverter.GetBytes(float.Parse(value, CultureInfo.InvariantCulture)),
            MemoryValueKind.Double => BitConverter.GetBytes(double.Parse(value, CultureInfo.InvariantCulture)),
            MemoryValueKind.Utf8String => PadString(Encoding.UTF8.GetBytes(value), explicitSize),
            MemoryValueKind.Utf16String => PadString(Encoding.Unicode.GetBytes(value), explicitSize),
            _ => throw new ArgumentOutOfRangeException(nameof(valueKind)),
        };
    }

    public static int ResolveByteCount(MemoryValueKind valueKind, string value, int explicitSize)
        => ParseExactValue(valueKind, value, explicitSize).Length;

    public static int Compare(MemoryValueKind valueKind, byte[] left, byte[] right)
        => valueKind switch
        {
            MemoryValueKind.Int32 => BitConverter.ToInt32(left).CompareTo(BitConverter.ToInt32(right)),
            MemoryValueKind.Int64 => BitConverter.ToInt64(left).CompareTo(BitConverter.ToInt64(right)),
            MemoryValueKind.Float => BitConverter.ToSingle(left).CompareTo(BitConverter.ToSingle(right)),
            MemoryValueKind.Double => BitConverter.ToDouble(left).CompareTo(BitConverter.ToDouble(right)),
            _ => throw new NotSupportedException($"Comparison '{valueKind}' does not support relative numeric refinement.")
        };

    public static double ToDouble(MemoryValueKind valueKind, byte[] buffer)
        => valueKind switch
        {
            MemoryValueKind.Int32 => BitConverter.ToInt32(buffer),
            MemoryValueKind.Int64 => BitConverter.ToInt64(buffer),
            MemoryValueKind.Float => BitConverter.ToSingle(buffer),
            MemoryValueKind.Double => BitConverter.ToDouble(buffer),
            _ => throw new NotSupportedException($"Comparison '{valueKind}' does not support numeric extraction.")
        };

    public static double ParseNumeric(MemoryValueKind valueKind, string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        return valueKind switch
        {
            MemoryValueKind.Int32 => int.Parse(value, CultureInfo.InvariantCulture),
            MemoryValueKind.Int64 => long.Parse(value, CultureInfo.InvariantCulture),
            MemoryValueKind.Float => float.Parse(value, CultureInfo.InvariantCulture),
            MemoryValueKind.Double => double.Parse(value, CultureInfo.InvariantCulture),
            _ => throw new NotSupportedException($"Comparison '{valueKind}' does not support numeric parsing.")
        };
    }

    private static byte[] ParseBytes(string value, int explicitSize)
    {
        var bytes = Convert.FromHexString(value);
        if (explicitSize > 0 && explicitSize != bytes.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(explicitSize), "Explicit byte size must match the hex payload length.");
        }

        return bytes;
    }

    private static byte[] PadString(byte[] bytes, int explicitSize)
    {
        if (explicitSize <= 0)
        {
            return bytes;
        }

        if (bytes.Length > explicitSize)
        {
            throw new ArgumentOutOfRangeException(nameof(explicitSize), "The value is longer than the requested size.");
        }

        var buffer = new byte[explicitSize];
        bytes.CopyTo(buffer, 0);
        return buffer;
    }
}