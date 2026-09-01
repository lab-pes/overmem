using System.Collections.Generic;
using System.Linq;
using System.Text;
using Overmem.Extensions.Pes2021.Players;

namespace Overmem.Extensions.Pes2021.Tests;

public sealed class Pes2021PlayerParserTests
{
    [Fact]
    public void Parser_DecodesFiveControlRecords_FromFeasibilityStudy()
    {
        var profile = Pes2021PlayerProfileDefaults.BuildBuiltIn();

        var controls = new (uint Id, string Name, byte Height, byte Weight, int MarketRaw)[]
        {
            (58118, "Luis Segovia", 182, 74, 0),
            (58119, "Anthony Landazuri", 179, 73, 0),
            (58120, "Piero Hincapie", 184, 74, 500_000),
            (58121, "Jhon Sanchez", 175, 74, 0),
            (58122, "Jonathan Bauman", 178, 73, 0),
        };

        foreach (var (id, name, height, weight, marketRaw) in controls)
        {
            var bytes = BuildRecord(profile, id, name, height, weight, marketRaw);
            var result = Pes2021PlayerRecordParser.TryParse(bytes, 0, 0xCAFEUL, profile);

            Assert.True(result.Success, $"control id={id} should parse, reason={result.RejectionReason}");
            Assert.NotNull(result.Record);
            Assert.Equal(id, result.Record!.PlayerId);
            Assert.Equal(name, result.Record.PlayerName);
            Assert.Equal(height, result.Record.Fields.Single(f => f.Name == "height").RawLong);
            Assert.Equal(weight, result.Record.Fields.Single(f => f.Name == "weight").RawLong);
            Assert.Equal(marketRaw, result.Record.Fields.Single(f => f.Name == "marketValue").RawLong);
            Assert.Equal(380, result.Record.RawRecord.Length);
            Assert.Equal(64, result.Record.RawRecordSha256.Length);
        }
    }

    [Fact]
    public void Parser_MarketValueRawMulHundredEur_DisplayIsRawTimesHundred()
    {
        var profile = Pes2021PlayerProfileDefaults.BuildBuiltIn();
        var bytes = BuildRecord(profile, 58120, "Piero Hincapie", 184, 74, 500_000);

        var result = Pes2021PlayerRecordParser.TryParse(bytes, 0, 0, profile);
        Assert.True(result.Success);
        var market = result.Record!.Fields.Single(f => f.Name == "marketValue");
        Assert.Equal(500_000L, market.RawLong);
        Assert.Equal(50_000_000.0, market.Display);
    }

    [Fact]
    public void Parser_RejectsBufferTooSmall()
    {
        var profile = Pes2021PlayerProfileDefaults.BuildBuiltIn();
        var truncated = new byte[100];

        var result = Pes2021PlayerRecordParser.TryParse(truncated, 0, 0, profile);
        Assert.False(result.Success);
        Assert.Equal(PlayerRecordRejectionReasons.BufferTooSmall, result.RejectionReason);
    }

    [Fact]
    public void Parser_RejectsHeightOutOfRange()
    {
        var profile = Pes2021PlayerProfileDefaults.BuildBuiltIn();
        var bytes = BuildRecord(profile, 58120, "Test Player", 99, 74, 0);

        var result = Pes2021PlayerRecordParser.TryParse(bytes, 0, 0, profile);
        Assert.False(result.Success);
        Assert.Equal(PlayerRecordRejectionReasons.HeightOutOfRange, result.RejectionReason);
    }

    [Fact]
    public void Parser_RejectsWeightOutOfRange()
    {
        var profile = Pes2021PlayerProfileDefaults.BuildBuiltIn();
        var bytes = BuildRecord(profile, 58120, "Test Player", 184, 0, 0);

        var result = Pes2021PlayerRecordParser.TryParse(bytes, 0, 0, profile);
        Assert.False(result.Success);
        Assert.Equal(PlayerRecordRejectionReasons.WeightOutOfRange, result.RejectionReason);
    }

    [Fact]
    public void Parser_RejectsPlayerIdOutOfRange()
    {
        var profile = Pes2021PlayerProfileDefaults.BuildBuiltIn();
        var bytes = BuildRecord(profile, 0, "Test Player", 184, 74, 0);

        var result = Pes2021PlayerRecordParser.TryParse(bytes, 0, 0, profile);
        Assert.False(result.Success);
        Assert.Equal(PlayerRecordRejectionReasons.PlayerIdOutOfRange, result.RejectionReason);
    }

    [Fact]
    public void Parser_RejectsUnterminatedPlayerName()
    {
        var profile = Pes2021PlayerProfileDefaults.BuildBuiltIn();
        var bytes = BuildRecord(profile, 58120, name: null, 184, 74, 0);

        var result = Pes2021PlayerRecordParser.TryParse(bytes, 0, 0, profile);
        Assert.False(result.Success);
        Assert.True(
            result.RejectionReason == PlayerRecordRejectionReasons.NameEmpty
            || result.RejectionReason == PlayerRecordRejectionReasons.NameUnterminated
            || result.RejectionReason == PlayerRecordRejectionReasons.NameContainsControlBytes);
    }

    [Fact]
    public void Parser_RejectsNameWithControlBytes()
    {
        var profile = Pes2021PlayerProfileDefaults.BuildBuiltIn();
        var bytes = BuildRecord(profile, 58120, "Test\x01Player", 184, 74, 0);

        var result = Pes2021PlayerRecordParser.TryParse(bytes, 0, 0, profile);
        Assert.False(result.Success);
        Assert.Equal(PlayerRecordRejectionReasons.NameContainsControlBytes, result.RejectionReason);
    }

    [Fact]
    public void Parser_RejectsImplausibleMarketValue()
    {
        var profile = Pes2021PlayerProfileDefaults.BuildBuiltIn();
        var bytes = BuildRecord(profile, 58120, "Test Player", 184, 74, int.MaxValue);

        var result = Pes2021PlayerRecordParser.TryParse(bytes, 0, 0, profile);
        Assert.False(result.Success);
        Assert.Equal(PlayerRecordRejectionReasons.MarketValueImplausible, result.RejectionReason);
    }

    [Fact]
    public void Parser_PreservesRawRecordBytes_AndComputesStableSha256()
    {
        var profile = Pes2021PlayerProfileDefaults.BuildBuiltIn();
        var bytes = BuildRecord(profile, 58120, "Piero Hincapie", 184, 74, 500_000);

        var result = Pes2021PlayerRecordParser.TryParse(bytes, 0, 0, profile);
        Assert.True(result.Success);
        Assert.Equal(bytes, result.Record!.RawRecord);

        var secondPass = Pes2021PlayerRecordParser.TryParse(bytes, 0, 0, profile);
        Assert.Equal(result.Record.RawRecordSha256, secondPass.Record!.RawRecordSha256);
    }

    [Fact]
    public void Parser_DecodesClubAndNationalShirtNames_AsTrimmedAscii()
    {
        var profile = Pes2021PlayerProfileDefaults.BuildBuiltIn();
        var bytes = BuildRecord(profile, 58120, "Piero Hincapie", 184, 74, 0,
            clubShirtName: "BAYER", nationalShirtName: "ECU");

        var result = Pes2021PlayerRecordParser.TryParse(bytes, 0, 0, profile);
        Assert.True(result.Success);
        var club = result.Record!.Fields.Single(f => f.Name == "clubShirtName");
        Assert.Equal("BAYER", club.RawString);
        var national = result.Record.Fields.Single(f => f.Name == "nationalShirtName");
        Assert.Equal("ECU", national.RawString);
    }

    [Fact]
    public void Parser_SignedI8BoundaryValues_DecodeWithoutOverflow()
    {
        var profile = Pes2021PlayerProfileDefaults.BuildBuiltIn();
        var bytes = BuildRecord(profile, 58120, "Piero Hincapie", 184, 74, 0);
        var unknown178 = profile.RecordLayout.Fields.Single(f => f.Name == "unknown_178");
        bytes[unknown178.Offset] = 0xFF;

        var result = Pes2021PlayerRecordParser.TryParse(bytes, 0, 0, profile);
        Assert.True(result.Success);
        var field = result.Record!.Fields.Single(f => f.Name == "unknown_178");
        Assert.Equal(-1L, field.RawLong);
    }

    [Fact]
    public void Parser_SignedI32BoundaryValues_DecodeWithoutOverflow()
    {
        var profile = Pes2021PlayerProfileDefaults.BuildBuiltIn();
        var bytes = BuildRecord(profile, 58120, "Piero Hincapie", 184, 74, int.MinValue);

        var result = Pes2021PlayerRecordParser.TryParse(bytes, 0, 0, profile);
        Assert.True(result.Success);
        var market = result.Record!.Fields.Single(f => f.Name == "marketValue");
        Assert.Equal((long)int.MinValue, market.RawLong);
        Assert.Equal((double)int.MinValue * 100.0, market.Display);
    }

    [Fact]
    public void Parser_DefaultStatusIsCandidateOrUnknown_NeverConfirmed()
    {
        var profile = Pes2021PlayerProfileDefaults.BuildBuiltIn();
        var bytes = BuildRecord(profile, 58120, "Piero Hincapie", 184, 74, 500_000);

        var result = Pes2021PlayerRecordParser.TryParse(bytes, 0, 0, profile);
        Assert.True(result.Success);
        Assert.Contains(result.Record!.Fields, f =>
            f.Name == "marketValue" && f.EvidenceStatus == Pes2021PlayerEvidenceStatus.Candidate);
    }

    [Fact]
    public void Validator_HappyPath_HasMaxScoreAndAccepts()
    {
        var profile = Pes2021PlayerProfileDefaults.BuildBuiltIn();
        var bytes = BuildRecord(profile, 58120, "Piero Hincapie", 184, 74, 500_000);

        var result = Pes2021PlayerRecordParser.TryParse(bytes, 0, 0, profile);
        var validation = Pes2021PlayerRecordValidator.Validate(result.Record!, profile);

        Assert.True(validation.Accept);
        Assert.Equal(validation.MaxScore, validation.Score);
        Assert.Contains("height_in_range", validation.Reasons);
        Assert.Contains("weight_in_range", validation.Reasons);
        Assert.Contains("player_id_in_range", validation.Reasons);
        Assert.Contains("market_value_plausible", validation.Reasons);
    }

    [Fact]
    public void Validator_RejectsHeightOutOfRange_WithoutReturningFalseSilently()
    {
        var profile = Pes2021PlayerProfileDefaults.BuildBuiltIn();
        var bytes = BuildRecord(profile, 58120, "Piero Hincapie", 99, 74, 500_000);

        var result = Pes2021PlayerRecordParser.TryParse(bytes, 0, 0, profile);
        Assert.False(result.Success);

        var record = new DecodedPlayerRecord(
            Address: 0,
            RecordIndex: 0,
            PlayerId: 58120,
            PlayerName: "Piero Hincapie",
            ClubShirtName: null,
            NationalShirtName: null,
            Fields: new List<DecodedFieldValue>
            {
                new("height", 99, null, null, Pes2021PlayerEvidenceStatus.Confirmed,
                    Pes2021PlayerTransform.None, System.Array.Empty<string>()),
            },
            RawRecord: bytes,
            RawRecordSha256: "0000",
            Warnings: System.Array.Empty<string>());

        var validation = Pes2021PlayerRecordValidator.Validate(record, profile);
        Assert.Contains("height_out_of_range", validation.Reasons);
        Assert.True(validation.Score < validation.MaxScore);
    }

    private static byte[] BuildRecord(
        Pes2021PlayerProfile profile,
        uint playerId,
        string? name,
        byte height,
        byte weight,
        int marketValueRaw,
        string? clubShirtName = null,
        string? nationalShirtName = null)
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
            WriteFixedAscii(bytes, nameField.Offset, nameField.Width, name);
        }

        if (clubShirtName is not null)
        {
            var field = profile.RecordLayout.Fields.Single(f => f.Name == "clubShirtName");
            WriteFixedAscii(bytes, field.Offset, field.Width, clubShirtName);
        }

        if (nationalShirtName is not null)
        {
            var field = profile.RecordLayout.Fields.Single(f => f.Name == "nationalShirtName");
            WriteFixedAscii(bytes, field.Offset, field.Width, nationalShirtName);
        }

        var marketField = profile.RecordLayout.Fields.Single(f => f.Name == "marketValue");
        System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(
            bytes.AsSpan(marketField.Offset, 4), marketValueRaw);

        return bytes;
    }

    private static void WriteFixedAscii(byte[] bytes, int offset, int width, string text)
    {
        var max = Math.Min(text.Length, width - 1);
        var ascii = Encoding.ASCII.GetBytes(text.Substring(0, max));
        for (var i = 0; i < max; i++)
        {
            bytes[offset + i] = ascii[i];
        }

        bytes[offset + max] = 0;
    }
}