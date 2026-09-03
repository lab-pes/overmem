using System;
using System.Buffers.Binary;
using System.Text.Json;
using System.Text;
using Overmem.Extensions.Pes2021.Players;
using Overmem.Extensions.Pes2021.Players.FamilyDiscovery;

namespace Overmem.Extensions.Pes2021.Tests.FamilyDiscovery;

public class FingerprintBuilderTests
{
    private static Pes2021PlayerProfile CreateTestProfile()
    {
        var fields = new[]
        {
            new Pes2021PlayerFieldDefinition(
                Name: "playerId",
                Offset: 0,
                Width: 4,
                Type: Pes2021PlayerFieldType.U32Le,
                Signedness: "unsigned",
                Endianness: "le",
                Transform: Pes2021PlayerTransform.None,
                ReadStatus: Pes2021PlayerEvidenceStatus.Confirmed,
                WriteStatus: Pes2021PlayerEvidenceStatus.Confirmed,
                ValidContexts: Array.Empty<Pes2021PlayerContext>(),
                SharedBitfield: false,
                Bits: null,
                Notes: null),
            new Pes2021PlayerFieldDefinition(
                Name: "playerName",
                Offset: 44,
                Width: 46,
                Type: Pes2021PlayerFieldType.FixedAscii,
                Signedness: "unsigned",
                Endianness: "le",
                Transform: Pes2021PlayerTransform.TrimAsciiZ,
                ReadStatus: Pes2021PlayerEvidenceStatus.Confirmed,
                WriteStatus: Pes2021PlayerEvidenceStatus.Confirmed,
                ValidContexts: Array.Empty<Pes2021PlayerContext>(),
                SharedBitfield: false,
                Bits: null,
                Notes: null),
            new Pes2021PlayerFieldDefinition(
                Name: "stamina",
                Offset: 120,
                Width: 1,
                Type: Pes2021PlayerFieldType.U8,
                Signedness: "unsigned",
                Endianness: "le",
                Transform: Pes2021PlayerTransform.None,
                ReadStatus: Pes2021PlayerEvidenceStatus.Candidate, // Not Confirmed -> should be masked out
                WriteStatus: Pes2021PlayerEvidenceStatus.Unknown,
                ValidContexts: Array.Empty<Pes2021PlayerContext>(),
                SharedBitfield: false,
                Bits: null,
                Notes: null)
        };

        var layout = new Pes2021PlayerRecordLayout(
            Stride: 380,
            StartOffset: 0,
            Fields: fields);

        var validation = new Pes2021PlayerRecordValidation(
            MinimumHeight: 150,
            MaximumHeight: 210,
            MinimumWeight: 50,
            MaximumWeight: 120,
            MinimumPlayerId: 1,
            MaximumPlayerId: 200000);

        var filter = new Pes2021PlayerRegionFilter(
            States: new[] { "MEM_COMMIT" },
            Types: new[] { "Private" },
            RequireReadable: true,
            RequireWritable: true,
            AllowExecutable: false,
            ChunkBytes: 1048576);

        var anchor = new Pes2021PlayerAnchorValidation(
            RecordsBefore: 4,
            RecordsAfter: 8,
            MinimumRun: 3,
            MinimumAnchorScore: 5,
            MediumScore: 8,
            HighScore: 12,
            MinimumControlsForStridePromotion: 3,
            ControlPlayerIds: new uint[] { 101473 });

        var limits = new Pes2021PlayerLimits(256, 1024, 50000, 10000);
        var sources = new Pes2021PlayerProfileSources(null, null, null);

        return new Pes2021PlayerProfile(
            SchemaVersion: "1.0",
            ProfileId: "test-profile",
            ProfileVersion: "1.0",
            EvidenceStatus: Pes2021PlayerEvidenceStatus.Candidate,
            ProcessNames: new[] { "PES2021.exe" },
            RecordLayout: layout,
            RecordValidation: validation,
            RegionFilter: filter,
            AnchorValidation: anchor,
            Limits: limits,
            Sources: sources,
            Sha256: "test-hash",
            SourcePath: "");
    }

    private static DecodedPlayerRecord CreateTestRecord(uint playerId, string name)
    {
        var raw = new byte[380];
        BinaryPrimitives.WriteUInt32LittleEndian(raw.AsSpan(0, 4), playerId);
        var nameBytes = Encoding.UTF8.GetBytes(name);
        Array.Copy(nameBytes, 0, raw, 44, Math.Min(nameBytes.Length, 46));
        raw[120] = 85; // stamina - dynamic

        return new DecodedPlayerRecord(
            Address: 0x1000,
            RecordIndex: 0,
            PlayerId: playerId,
            PlayerName: name,
            ClubShirtName: null,
            NationalShirtName: null,
            Fields: Array.Empty<DecodedFieldValue>(),
            RawRecord: raw,
            RawRecordSha256: "sha",
            Warnings: Array.Empty<string>());
    }

    [Fact]
    public void Build_RejectsEmptyControlList()
    {
        var profile = CreateTestProfile();
        Assert.Throws<ArgumentException>(() => FingerprintBuilder.Build(profile, Array.Empty<DecodedPlayerRecord>()));
    }

    [Fact]
    public void Build_GeneratesIdBytesCorrectly()
    {
        var profile = CreateTestProfile();
        var record = CreateTestRecord(101473, "MESSI");

        var set = FingerprintBuilder.Build(profile, new[] { record });

        Assert.Single(set.Fingerprints);
        var fp = set.Fingerprints[0];
        
        Assert.Equal(101473U, fp.PlayerId);
        Assert.Equal(4, fp.IdBytes.Length);
        Assert.Equal(0x61, fp.IdBytes[0]); // 101473 = 0x018C61 -> LE: 61 8C 01 00
        Assert.Equal(0x8C, fp.IdBytes[1]);
        Assert.Equal(0x01, fp.IdBytes[2]);
        Assert.Equal(0x00, fp.IdBytes[3]);
    }

    [Fact]
    public void Build_PreservesHighBitsInPlayerId()
    {
        var profile = CreateTestProfile();
        var record = CreateTestRecord(0x80001234, "HIGH_BIT");

        var set = FingerprintBuilder.Build(profile, new[] { record });

        var fp = set.Fingerprints[0];
        Assert.Equal(0x80001234, fp.PlayerId);
        Assert.Equal(0x34, fp.IdBytes[0]);
        Assert.Equal(0x12, fp.IdBytes[1]);
        Assert.Equal(0x00, fp.IdBytes[2]);
        Assert.Equal(0x80, fp.IdBytes[3]);
    }

    [Fact]
    public void Build_MaskIgnoresDynamicFields()
    {
        var profile = CreateTestProfile();
        var record = CreateTestRecord(101473, "MESSI");

        var set = FingerprintBuilder.Build(profile, new[] { record });
        var fp = set.Fingerprints[0];

        // playerId (offset 0, width 4) -> Confirmed -> should be 0xFF
        Assert.Equal(0xFF, fp.Mask[0]);
        Assert.Equal(0xFF, fp.Mask[3]);

        // playerName (offset 44, width 46) -> Confirmed -> should be 0xFF
        Assert.Equal(0xFF, fp.Mask[44]);
        Assert.Equal(0xFF, fp.Mask[89]);

        // stamina (offset 120, width 1) -> Candidate -> should be 0x00 (ignored)
        Assert.Equal(0x00, fp.Mask[120]);

        // unmapped byte (offset 4) -> should be 0x00
        Assert.Equal(0x00, fp.Mask[4]);

        Assert.Contains(120, set.DynamicByteOffsets);
        Assert.Contains(4, set.DynamicByteOffsets);
        Assert.DoesNotContain(0, set.DynamicByteOffsets);
    }

    [Fact]
    public void Build_MaskedRecordHasZerosAtDynamicOffsets()
    {
        var profile = CreateTestProfile();
        var record = CreateTestRecord(101473, "MESSI");

        var set = FingerprintBuilder.Build(profile, new[] { record });
        var fp = set.Fingerprints[0];

        Assert.NotNull(fp.ExactRecord);
        Assert.NotNull(fp.MaskedRecord);

        // Original record has stamina = 85
        Assert.Equal(85, fp.ExactRecord[120]);
        
        // Masked record should have stamina = 0 because it's masked out
        Assert.Equal(0, fp.MaskedRecord[120]);

        // Player ID should remain intact
        Assert.Equal(0x61, fp.MaskedRecord[0]);
    }

    [Fact]
    public void FalseCandidateOffset3_FailsMaskedMatch()
    {
        var profile = CreateTestProfile();
        var record = CreateTestRecord(101473, "MESSI");

        var set = FingerprintBuilder.Build(profile, new[] { record });
        var fp = set.Fingerprints[0];
        var mask = fp.Mask;
        var maskedControl = fp.MaskedRecord!;

        // Simulate a window shifted by 3 bytes
        var shiftedMemory = new byte[380];
        Array.Copy(record.RawRecord, 3, shiftedMemory, 0, 377);

        // Apply mask to the shifted window
        var shiftedMasked = new byte[380];
        for (var i = 0; i < 380; i++)
        {
            shiftedMasked[i] = (byte)(shiftedMemory[i] & mask[i]);
        }

        // They must NOT match
        var match = true;
        for (var i = 0; i < 380; i++)
        {
            if (shiftedMasked[i] != maskedControl[i])
            {
                match = false;
                break;
            }
        }

        Assert.False(match);
    }
}
