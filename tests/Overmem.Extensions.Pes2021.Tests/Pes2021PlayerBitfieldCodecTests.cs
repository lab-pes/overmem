using System.Linq;
using Overmem.Extensions.Pes2021.Players;

namespace Overmem.Extensions.Pes2021.Tests;

public sealed class Pes2021PlayerBitfieldCodecTests
{
    [Fact]
    public void Read_ExtractsLowBitsFromByte()
    {
        var profile = Pes2021PlayerProfileDefaults.BuildBuiltIn();
        var container = profile.RecordLayout.Fields.Single(f => f.Name == "staminaBar");
        var blinking = container.Bits!.Single(b => b.Name == "blinkingFormArrow");

        var bytes = new byte[profile.Stride];
        bytes[container.Offset] = 0b1010_0101;

        var value = Pes2021PlayerBitfieldCodec.Read(bytes, container, blinking);
        Assert.Equal(1u, value);
    }

    [Fact]
    public void Read_ExtractsMultiBitFieldFromContainer()
    {
        var profile = Pes2021PlayerProfileDefaults.BuildBuiltIn();
        var container = profile.RecordLayout.Fields.Single(f => f.Name == "staminaBar");
        var stamina = container.Bits!.Single(b => b.Name == "staminaBar");

        var bytes = new byte[profile.Stride];
        bytes[container.Offset] = 0b0010_1010;

        var value = Pes2021PlayerBitfieldCodec.Read(bytes, container, stamina);
        Assert.Equal(0b0101010u, value);
    }

    [Fact]
    public void Write_InsertsBits_AndPreservesOtherBitsInContainer()
    {
        var profile = Pes2021PlayerProfileDefaults.BuildBuiltIn();
        var container = profile.RecordLayout.Fields.Single(f => f.Name == "staminaBar");
        var stamina = container.Bits!.Single(b => b.Name == "staminaBar");
        var blinking = container.Bits!.Single(b => b.Name == "blinkingFormArrow");

        var bytes = new byte[profile.Stride];
        bytes[container.Offset] = 0b1111_1111;

        var written = Pes2021PlayerBitfieldCodec.Write(bytes, container, stamina, 0);

        var staminaRead = Pes2021PlayerBitfieldCodec.Read(written, container, stamina);
        var blinkingRead = Pes2021PlayerBitfieldCodec.Read(written, container, blinking);

        Assert.Equal(0u, staminaRead);
        Assert.Equal(1u, blinkingRead);
        Assert.Equal(0b1000_0000, written[container.Offset]);
    }

    [Fact]
    public void Write_PreservesAllOtherBytesInRecord()
    {
        var profile = Pes2021PlayerProfileDefaults.BuildBuiltIn();
        var container = profile.RecordLayout.Fields.Single(f => f.Name == "staminaBar");
        var stamina = container.Bits!.Single(b => b.Name == "staminaBar");

        var bytes = new byte[profile.Stride];
        for (var i = 0; i < bytes.Length; i++)
        {
            bytes[i] = (byte)(i & 0xFF);
        }

        var original = (byte[])bytes.Clone();
        var written = Pes2021PlayerBitfieldCodec.Write(bytes, container, stamina, 0);

        for (var i = 0; i < written.Length; i++)
        {
            if (i == container.Offset)
            {
                Assert.NotEqual(original[i], written[i]);
            }
            else
            {
                Assert.Equal(original[i], written[i]);
            }
        }
    }

    [Fact]
    public void Write_DoesNotMutateOriginalBuffer()
    {
        var profile = Pes2021PlayerProfileDefaults.BuildBuiltIn();
        var container = profile.RecordLayout.Fields.Single(f => f.Name == "staminaBar");
        var stamina = container.Bits!.Single(b => b.Name == "staminaBar");

        var bytes = new byte[profile.Stride];
        bytes[container.Offset] = 0xFF;

        var snapshot = (byte[])bytes.Clone();
        _ = Pes2021PlayerBitfieldCodec.Write(bytes, container, stamina, 0);

        Assert.Equal(snapshot, bytes);
    }

    [Fact]
    public void Write_RefusesValueLargerThanBitCapacity()
    {
        var profile = Pes2021PlayerProfileDefaults.BuildBuiltIn();
        var container = profile.RecordLayout.Fields.Single(f => f.Name == "staminaBar");
        var stamina = container.Bits!.Single(b => b.Name == "staminaBar");

        var bytes = new byte[profile.Stride];

        Assert.Throws<ArgumentException>(() =>
            Pes2021PlayerBitfieldCodec.Write(bytes, container, stamina, 0xFF));
    }

    [Fact]
    public void WriteMany_AppliesPatchesLeftToRight_AndProducesFinalState()
    {
        var profile = Pes2021PlayerProfileDefaults.BuildBuiltIn();
        var staminaContainer = profile.RecordLayout.Fields.Single(f => f.Name == "staminaBar");
        var stamina = staminaContainer.Bits!.Single(b => b.Name == "staminaBar");
        var blinking = staminaContainer.Bits!.Single(b => b.Name == "blinkingFormArrow");

        var bytes = new byte[profile.Stride];

        var patches = new System.Collections.Generic.List<(Pes2021PlayerFieldDefinition, Pes2021PlayerBitField, uint)>
        {
            (staminaContainer, stamina, 42),
            (staminaContainer, blinking, 1),
        };

        var result = Pes2021PlayerBitfieldCodec.WriteMany(bytes, profile, patches);
        var staminaRead = Pes2021PlayerBitfieldCodec.Read(result, staminaContainer, stamina);
        var blinkingRead = Pes2021PlayerBitfieldCodec.Read(result, staminaContainer, blinking);

        Assert.Equal(42u, staminaRead);
        Assert.Equal(1u, blinkingRead);
    }

    [Fact]
    public void Write_RoundTripPreservesValueAcrossReadAfterWrite()
    {
        var profile = Pes2021PlayerProfileDefaults.BuildBuiltIn();
        var container = profile.RecordLayout.Fields.Single(f => f.Name == "currentFormArrow");
        var bit = container.Bits!.Single(b => b.Name == "currentFormArrow");

        var bytes = new byte[profile.Stride];

        for (uint candidate = 0; candidate < 8; candidate++)
        {
            var written = Pes2021PlayerBitfieldCodec.Write(bytes, container, bit, candidate);
            var read = Pes2021PlayerBitfieldCodec.Read(written, container, bit);
            Assert.Equal(candidate, read);
        }
    }

    [Fact]
    public void FieldBytesUnchanged_ReturnsFalseWhenByteDiffers()
    {
        var profile = Pes2021PlayerProfileDefaults.BuildBuiltIn();
        var container = profile.RecordLayout.Fields.Single(f => f.Name == "staminaBar");

        var before = new byte[profile.Stride];
        var after = new byte[profile.Stride];
        after[container.Offset] = 1;

        Assert.False(Pes2021PlayerBitfieldCodec.FieldBytesUnchanged(before, after, container));
    }

    [Fact]
    public void FieldBytesUnchanged_ReturnsTrueWhenBytesIdentical()
    {
        var profile = Pes2021PlayerProfileDefaults.BuildBuiltIn();
        var container = profile.RecordLayout.Fields.Single(f => f.Name == "staminaBar");

        var before = new byte[profile.Stride];
        var after = new byte[profile.Stride];

        Assert.True(Pes2021PlayerBitfieldCodec.FieldBytesUnchanged(before, after, container));
    }

    [Fact]
    public void Validator_WithNeighbors_AddsNeighborScores()
    {
        var profile = Pes2021PlayerProfileDefaults.BuildBuiltIn();
        var bytes = BuildRecord(profile, 58120, "Piero Hincapie", 184, 74, 500_000);

        var result = Pes2021PlayerRecordParser.TryParse(bytes, 0, 0, profile);
        var neighbors = new[] { result.Record!, result.Record!, result.Record! };

        var validation = Pes2021PlayerRecordValidator.ValidateWithNeighbors(
            result.Record!, profile, neighbors, neighbors);

        Assert.True(validation.Accept);
        Assert.Contains("forward_neighbors_present", validation.Reasons);
        Assert.Contains("backward_neighbors_present", validation.Reasons);
        Assert.True(validation.MaxScore > 5);
    }

    private static byte[] BuildRecord(
        Pes2021PlayerProfile profile,
        uint playerId,
        string? name,
        byte height,
        byte weight,
        int marketValueRaw)
    {
        var bytes = new byte[profile.Stride];

        var heightField = profile.RecordLayout.Fields.Single(f => f.Name == "height");
        bytes[heightField.Offset] = height;

        var weightField = profile.RecordLayout.Fields.Single(f => f.Name == "weight");
        bytes[weightField.Offset] = weight;

        var playerIdField = profile.RecordLayout.Fields.Single(f => f.Name == "playerId");
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(
            bytes.AsSpan(playerIdField.Offset, 4), playerId);

        if (name is not null)
        {
            var nameField = profile.RecordLayout.Fields.Single(f => f.Name == "playerName");
            var max = System.Math.Min(name.Length, nameField.Width - 1);
            var ascii = System.Text.Encoding.ASCII.GetBytes(name.Substring(0, max));
            for (var i = 0; i < max; i++)
            {
                bytes[nameField.Offset + i] = ascii[i];
            }

            bytes[nameField.Offset + max] = 0;
        }

        var marketField = profile.RecordLayout.Fields.Single(f => f.Name == "marketValue");
        System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(
            bytes.AsSpan(marketField.Offset, 4), marketValueRaw);

        return bytes;
    }
}