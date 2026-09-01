using System.Buffers.Binary;

namespace Overmem.Extensions.Pes2021.Players;

/// <summary>
/// Read-modify-write codec for packed bitfields declared in a
/// <see cref="Pes2021PlayerFieldDefinition"/>. Every operation works on a copy of the
/// record so the caller's buffer is never mutated. Reads return an unsigned integer
/// (up to 32 bits). Writes accept any unsigned value up to the bit width and refuse to
/// write when the value would overflow.
/// </summary>
public static class Pes2021PlayerBitfieldCodec
{
    public static uint Read(
        ReadOnlySpan<byte> record,
        Pes2021PlayerFieldDefinition field,
        Pes2021PlayerBitField bit)
    {
        ValidateField(field);
        ValidateBit(field, bit);

        var span = record.Slice(field.Offset, field.Width);
        return ExtractBits(span, bit.BitStart, bit.BitLength);
    }

    /// <summary>
    /// Encodes <paramref name="value"/> into the given bit slot inside a copy of
    /// <paramref name="record"/>. Throws <see cref="ArgumentException"/> when the value
    /// would overflow the bit width or the field/byte range is invalid. The returned
    /// record is always a fresh copy.
    /// </summary>
    public static byte[] Write(
        ReadOnlySpan<byte> record,
        Pes2021PlayerFieldDefinition field,
        Pes2021PlayerBitField bit,
        uint value)
    {
        ValidateField(field);
        ValidateBit(field, bit);

        if (bit.BitLength < 32)
        {
            var max = (1u << bit.BitLength) - 1u;
            if (value > max)
            {
                throw new ArgumentException(
                    $"Value {value} exceeds the {bit.BitLength}-bit capacity of '{bit.Name}'.",
                    nameof(value));
            }
        }

        var copy = record.ToArray();
        var slice = copy.AsSpan(field.Offset, field.Width);
        InsertBits(slice, bit.BitStart, bit.BitLength, value);
        return copy;
    }

    /// <summary>
    /// Writes a sequence of bitfield patches over a single record copy. The patches are
    /// applied in the supplied sequence and the result is the final state of the record
    /// after every patch. Each patch is validated for non-overlap with prior patches in
    /// the same call.
    /// </summary>
    public static byte[] WriteMany(
        ReadOnlySpan<byte> record,
        Pes2021PlayerProfile profile,
        IReadOnlyList<(Pes2021PlayerFieldDefinition Field, Pes2021PlayerBitField Bit, uint Value)> patches)
    {
        if (patches.Count == 0)
        {
            return record.ToArray();
        }

        var copy = record.ToArray();
        foreach (var (field, bit, value) in patches)
        {
            if (!ReferenceEquals(field, patches[0].Field)
                && !FieldMatches(profile, field))
            {
                throw new ArgumentException(
                    $"Field '{field.Name}' does not match the supplied profile.",
                    nameof(patches));
            }

            if (bit.BitLength < 32)
            {
                var max = (1u << bit.BitLength) - 1u;
                if (value > max)
                {
                    throw new ArgumentException(
                        $"Value {value} exceeds the {bit.BitLength}-bit capacity of '{bit.Name}'.",
                        nameof(value));
                }
            }

            var slice = copy.AsSpan(field.Offset, field.Width);
            InsertBits(slice, bit.BitStart, bit.BitLength, value);
        }

        return copy;
    }

    /// <summary>
    /// Returns true when the bytes covered by <paramref name="field"/> are identical
    /// between <paramref name="before"/> and <paramref name="after"/>. Used by the
    /// executor to prove that a bitfield patch did not perturb neighbor bytes.
    /// </summary>
    public static bool FieldBytesUnchanged(
        ReadOnlySpan<byte> before,
        ReadOnlySpan<byte> after,
        Pes2021PlayerFieldDefinition field)
    {
        if (before.Length < field.Offset + field.Width) return false;
        if (after.Length < field.Offset + field.Width) return false;
        for (var i = 0; i < field.Width; i++)
        {
            if (before[field.Offset + i] != after[field.Offset + i]) return false;
        }

        return true;
    }

    private static uint ExtractBits(ReadOnlySpan<byte> bytes, int bitStart, int bitLength)
    {
        ulong value = 0;
        var totalBits = bytes.Length * 8;
        for (var i = 0; i < bitLength; i++)
        {
            var bitIndex = bitStart + i;
            var byteIndex = bitIndex >> 3;
            var bitInByte = bitIndex & 7;
            if (byteIndex >= bytes.Length) break;
            var bit = (bytes[byteIndex] >> bitInByte) & 1;
            value |= ((ulong)bit) << i;
        }

        _ = totalBits;
        return (uint)value;
    }

    private static void InsertBits(Span<byte> bytes, int bitStart, int bitLength, uint value)
    {
        var mask = bitLength >= 32 ? uint.MaxValue : (1u << bitLength) - 1u;
        var normalized = value & mask;
        for (var i = 0; i < bitLength; i++)
        {
            var bit = (normalized >> i) & 1u;
            var bitIndex = bitStart + i;
            var byteIndex = bitIndex >> 3;
            var bitInByte = bitIndex & 7;
            if (byteIndex >= bytes.Length) break;
            if (bit == 1)
            {
                bytes[byteIndex] |= (byte)(1 << bitInByte);
            }
            else
            {
                bytes[byteIndex] &= (byte)~(1 << bitInByte);
            }
        }
    }

    private static void ValidateField(Pes2021PlayerFieldDefinition field)
    {
        if (field.Width <= 0)
        {
            throw new ArgumentException($"Field '{field.Name}' has non-positive width.", nameof(field));
        }

        if (field.Offset < 0)
        {
            throw new ArgumentException($"Field '{field.Name}' has negative offset.", nameof(field));
        }
    }

    private static void ValidateBit(Pes2021PlayerFieldDefinition field, Pes2021PlayerBitField bit)
    {
        var capacity = field.Width * 8;
        if (bit.BitStart < 0 || bit.BitLength <= 0 || bit.BitStart + bit.BitLength > capacity)
        {
            throw new ArgumentException(
                $"Bit '{bit.Name}' range [{bit.BitStart}, {bit.BitStart + bit.BitLength}) does not fit in field '{field.Name}' ({capacity} bits).",
                nameof(bit));
        }
    }

    private static bool FieldMatches(Pes2021PlayerProfile profile, Pes2021PlayerFieldDefinition field)
    {
        foreach (var candidate in profile.RecordLayout.Fields)
        {
            if (candidate.Name == field.Name
                && candidate.Offset == field.Offset
                && candidate.Width == field.Width
                && candidate.Type == field.Type)
            {
                return true;
            }
        }

        return false;
    }
}