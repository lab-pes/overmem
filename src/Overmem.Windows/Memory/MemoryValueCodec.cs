using Overmem.Abstractions.Memory;
using System.Globalization;
using System.Text;

namespace Overmem.Windows.Memory;

public static class MemoryValueCodec
{
    public static int ResolveByteCount(MemoryValueKind valueKind, int explicitSize) => valueKind switch
    {
        MemoryValueKind.Bytes when explicitSize > 0 => explicitSize,
        MemoryValueKind.Int32 => sizeof(int),
        MemoryValueKind.Int64 => sizeof(long),
        MemoryValueKind.Float => sizeof(float),
        MemoryValueKind.Double => sizeof(double),
        MemoryValueKind.Utf8String when explicitSize > 0 => explicitSize,
        MemoryValueKind.Utf16String when explicitSize > 0 => explicitSize,
        _ => throw new ArgumentOutOfRangeException(nameof(explicitSize), "This value kind requires a positive size."),
    };

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

    public static byte[] ParseValue(MemoryValueKind valueKind, string value, int explicitSize) => valueKind switch
    {
        MemoryValueKind.Bytes => Convert.FromHexString(value),
        MemoryValueKind.Int32 => BitConverter.GetBytes(int.Parse(value, CultureInfo.InvariantCulture)),
        MemoryValueKind.Int64 => BitConverter.GetBytes(long.Parse(value, CultureInfo.InvariantCulture)),
        MemoryValueKind.Float => BitConverter.GetBytes(float.Parse(value, CultureInfo.InvariantCulture)),
        MemoryValueKind.Double => BitConverter.GetBytes(double.Parse(value, CultureInfo.InvariantCulture)),
        MemoryValueKind.Utf8String => PadString(Encoding.UTF8.GetBytes(value), explicitSize),
        MemoryValueKind.Utf16String => PadString(Encoding.Unicode.GetBytes(value), explicitSize),
        _ => throw new ArgumentOutOfRangeException(nameof(valueKind)),
    };

    private static byte[] PadString(byte[] bytes, int explicitSize)
    {
        if (explicitSize <= 0)
        {
            return bytes;
        }

        if (bytes.Length > explicitSize)
        {
            throw new ArgumentOutOfRangeException(nameof(explicitSize), "The value is longer than the requested write size.");
        }

        var buffer = new byte[explicitSize];
        bytes.CopyTo(buffer, 0);
        return buffer;
    }
}