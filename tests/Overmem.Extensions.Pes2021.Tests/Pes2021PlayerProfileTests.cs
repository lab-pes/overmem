using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using Overmem.Extensions.Pes2021.Players;

namespace Overmem.Extensions.Pes2021.Tests;

public sealed class Pes2021PlayerProfileTests
{
    [Fact]
    public void BuiltInProfile_LoadsAndExposesExpectedStride()
    {
        var profile = Pes2021PlayerProfileDefaults.BuildBuiltIn();

        Assert.Equal("pes2021.player-record.v1", profile.SchemaVersion);
        Assert.Equal("pes2021-player-edit-v1", profile.ProfileId);
        Assert.Equal(380, profile.Stride);
        Assert.Equal(0, profile.RecordLayout.StartOffset);
        Assert.NotEmpty(profile.RecordLayout.Fields);
    }

    [Fact]
    public void BuiltInProfile_ContainsMandatoryNeutralUnknownFields()
    {
        var profile = Pes2021PlayerProfileDefaults.BuildBuiltIn();
        var names = profile.RecordLayout.Fields.Select(f => f.Name).ToList();

        Assert.Contains("unknown_12c", names);
        Assert.Contains("unknown_12e", names);
        Assert.Contains("unknown_178", names);
        Assert.Contains("unknown_179", names);
    }

    [Fact]
    public void BuiltInProfile_NeutralUnknownFieldsRemainUnknown()
    {
        var profile = Pes2021PlayerProfileDefaults.BuildBuiltIn();

        foreach (var name in new[] { "unknown_12c", "unknown_12e", "unknown_178", "unknown_179" })
        {
            var field = profile.RecordLayout.Fields.Single(f => f.Name == name);
            Assert.Equal(Pes2021PlayerEvidenceStatus.Unknown, field.ReadStatus);
        }
    }

    [Fact]
    public void BuiltInProfile_NoFieldDefaultsToConfirmedRead()
    {
        var profile = Pes2021PlayerProfileDefaults.BuildBuiltIn();

        var confirmedReads = profile.RecordLayout.Fields
            .Where(f => f.ReadStatus == Pes2021PlayerEvidenceStatus.Confirmed)
            .Select(f => f.Name)
            .ToList();

        Assert.True(confirmedReads.Count <= 4,
            $"Only structural fields should be confirmed; saw {string.Join(",", confirmedReads)}.");
        Assert.Subset(
            new HashSet<string> { "height", "weight", "playerId", "playerName" },
            new HashSet<string>(confirmedReads));
    }

    [Fact]
    public void BuiltInProfile_HasNoUnjustifiedOverlap()
    {
        var profile = Pes2021PlayerProfileDefaults.BuildBuiltIn();
        var ranges = profile.RecordLayout.Fields
            .Select(f => new { f.Name, f.Offset, End = f.Offset + f.Width, f.SharedBitfield })
            .OrderBy(r => r.Offset)
            .ToList();

        for (var i = 1; i < ranges.Count; i++)
        {
            var previous = ranges[i - 1];
            var current = ranges[i];
            if (current.Offset < previous.End)
            {
                Assert.True(previous.SharedBitfield && current.SharedBitfield,
                    $"Overlap between '{previous.Name}' and '{current.Name}' without sharedBitfield.");
            }
        }
    }

    [Fact]
    public void Loader_RejectsNonMatchingStride()
    {
        var json = CreateJson(("schemaVersion", "pes2021.player-record.v1"),
            ("profileId", "test"), ("profileVersion", "1.0.0"), ("evidenceStatus", "CANDIDATE"),
            ("recordLayout", new { stride = 128, startOffset = 0, fields = new object[0] }));

        var ex = Assert.Throws<Pes2021PlayerProfileException>(
            () => Pes2021PlayerProfileLoader.LoadFromBytes(Encoding.UTF8.GetBytes(json), "<inline>"));
        Assert.Equal("PES2021_PLAYER_PROFILE_INVALID", ex.Code);
        Assert.Contains("stride", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Loader_RejectsOffsetOutsideStride()
    {
        var json = CreateJson(
            ("schemaVersion", "pes2021.player-record.v1"),
            ("profileId", "test"), ("profileVersion", "1.0.0"), ("evidenceStatus", "CANDIDATE"),
            ("recordLayout", new
            {
                stride = 380,
                startOffset = 0,
                fields = new[]
                {
                    new
                    {
                        name = "too_far", offset = 400, width = 1, type = "u8",
                        signedness = "unsigned", endianness = "n/a", transform = "none",
                        readStatus = "CANDIDATE", writeStatus = "CANDIDATE",
                        validContexts = new[] { "EDIT_BASE_CONFIRMED" },
                        sharedBitfield = false,
                    },
                },
            }));

        var ex = Assert.Throws<Pes2021PlayerProfileException>(
            () => Pes2021PlayerProfileLoader.LoadFromBytes(Encoding.UTF8.GetBytes(json), "<inline>"));
        Assert.Equal("PES2021_PLAYER_PROFILE_INVALID", ex.Code);
    }

    [Fact]
    public void Loader_RejectsBitOverflow()
    {
        var json = CreateJson(
            ("schemaVersion", "pes2021.player-record.v1"),
            ("profileId", "test"), ("profileVersion", "1.0.0"), ("evidenceStatus", "CANDIDATE"),
            ("recordLayout", new
            {
                stride = 380,
                startOffset = 0,
                fields = new[]
                {
                    new
                    {
                        name = "bad_bits", offset = 0, width = 1, type = "u8",
                        signedness = "unsigned", endianness = "n/a", transform = "bitfield",
                        readStatus = "CANDIDATE", writeStatus = "CANDIDATE",
                        validContexts = new[] { "EDIT_BASE_CONFIRMED" },
                        sharedBitfield = true,
                        bits = new[]
                        {
                            new { name = "oversize", bitStart = 4, bitLength = 8,
                                readStatus = "CANDIDATE", writeStatus = "CANDIDATE" },
                        },
                    },
                },
            }));

        var ex = Assert.Throws<Pes2021PlayerProfileException>(
            () => Pes2021PlayerProfileLoader.LoadFromBytes(Encoding.UTF8.GetBytes(json), "<inline>"));
        Assert.Equal("PES2021_PLAYER_PROFILE_INVALID", ex.Code);
        Assert.Contains("exceeds", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Loader_RejectsDuplicateFieldKey()
    {
        var json = CreateJson(
            ("schemaVersion", "pes2021.player-record.v1"),
            ("profileId", "test"), ("profileVersion", "1.0.0"), ("evidenceStatus", "CANDIDATE"),
            ("recordLayout", new
            {
                stride = 380,
                startOffset = 0,
                fields = new[]
                {
                    BuildField(name: "dup", offset: 0),
                    BuildField(name: "dup", offset: 1),
                },
            }));

        var ex = Assert.Throws<Pes2021PlayerProfileException>(
            () => Pes2021PlayerProfileLoader.LoadFromBytes(Encoding.UTF8.GetBytes(json), "<inline>"));
        Assert.Equal("PES2021_PLAYER_PROFILE_INVALID", ex.Code);
        Assert.Contains("duplicate", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Loader_RejectsUnjustifiedOverlap()
    {
        var json = CreateJson(
            ("schemaVersion", "pes2021.player-record.v1"),
            ("profileId", "test"), ("profileVersion", "1.0.0"), ("evidenceStatus", "CANDIDATE"),
            ("recordLayout", new
            {
                stride = 380,
                startOffset = 0,
                fields = new[]
                {
                    BuildField(name: "a", offset: 0),
                    BuildField(name: "b", offset: 0),
                },
            }));

        var ex = Assert.Throws<Pes2021PlayerProfileException>(
            () => Pes2021PlayerProfileLoader.LoadFromBytes(Encoding.UTF8.GetBytes(json), "<inline>"));
        Assert.Equal("PES2021_PLAYER_PROFILE_INVALID", ex.Code);
        Assert.True(ex.Message.Contains("overlap", StringComparison.OrdinalIgnoreCase),
            $"Expected 'overlap' in '{ex.Message}'");
    }

    [Fact]
    public void Loader_RejectsUnsupportedType()
    {
        var json = CreateJson(
            ("schemaVersion", "pes2021.player-record.v1"),
            ("profileId", "test"), ("profileVersion", "1.0.0"), ("evidenceStatus", "CANDIDATE"),
            ("recordLayout", new
            {
                stride = 380,
                startOffset = 0,
                fields = new[]
                {
                    new
                    {
                        name = "bad_type", offset = 0, width = 4, type = "f32le",
                        signedness = "signed", endianness = "little", transform = "none",
                        readStatus = "CANDIDATE", writeStatus = "CANDIDATE",
                        validContexts = new[] { "EDIT_BASE_CONFIRMED" },
                        sharedBitfield = false,
                    },
                },
            }));

        var ex = Assert.Throws<Pes2021PlayerProfileException>(
            () => Pes2021PlayerProfileLoader.LoadFromBytes(Encoding.UTF8.GetBytes(json), "<inline>"));
        Assert.Equal("PES2021_PLAYER_PROFILE_INVALID", ex.Code);
        Assert.Contains("type", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Loader_RejectsBadEvidenceStatus()
    {
        var json = CreateJson(
            ("schemaVersion", "pes2021.player-record.v1"),
            ("profileId", "test"), ("profileVersion", "1.0.0"), ("evidenceStatus", "CANDIDATE"),
            ("recordLayout", new
            {
                stride = 380,
                startOffset = 0,
                fields = new[]
                {
                    new
                    {
                        name = "bad_status", offset = 0, width = 1, type = "u8",
                        signedness = "unsigned", endianness = "n/a", transform = "none",
                        readStatus = "MAYBE", writeStatus = "CANDIDATE",
                        validContexts = new[] { "EDIT_BASE_CONFIRMED" },
                        sharedBitfield = false,
                    },
                },
            }));

        var ex = Assert.Throws<Pes2021PlayerProfileException>(
            () => Pes2021PlayerProfileLoader.LoadFromBytes(Encoding.UTF8.GetBytes(json), "<inline>"));
        Assert.Equal("PES2021_PLAYER_PROFILE_INVALID", ex.Code);
        Assert.Contains("status", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Loader_RejectsInvalidTransform()
    {
        var json = CreateJson(
            ("schemaVersion", "pes2021.player-record.v1"),
            ("profileId", "test"), ("profileVersion", "1.0.0"), ("evidenceStatus", "CANDIDATE"),
            ("recordLayout", new
            {
                stride = 380,
                startOffset = 0,
                fields = new[]
                {
                    new
                    {
                        name = "bad_transform", offset = 0, width = 1, type = "u8",
                        signedness = "unsigned", endianness = "n/a", transform = "rawMul1kEur",
                        readStatus = "CANDIDATE", writeStatus = "CANDIDATE",
                        validContexts = new[] { "EDIT_BASE_CONFIRMED" },
                        sharedBitfield = false,
                    },
                },
            }));

        var ex = Assert.Throws<Pes2021PlayerProfileException>(
            () => Pes2021PlayerProfileLoader.LoadFromBytes(Encoding.UTF8.GetBytes(json), "<inline>"));
        Assert.Equal("PES2021_PLAYER_PROFILE_INVALID", ex.Code);
    }

    [Fact]
    public void Loader_RoundTripPreservesSemanticProfile()
    {
        var profile = Pes2021PlayerProfileDefaults.BuildBuiltIn();
        var bytes = Encoding.UTF8.GetBytes(Serialize(profile));
        var reloaded = Pes2021PlayerProfileLoader.LoadFromBytes(bytes, "<roundtrip>");

        Assert.Equal(profile.ProfileId, reloaded.ProfileId);
        Assert.Equal(profile.ProfileVersion, reloaded.ProfileVersion);
        Assert.Equal(profile.Stride, reloaded.Stride);
        Assert.Equal(profile.RecordLayout.Fields.Count, reloaded.RecordLayout.Fields.Count);
        for (var i = 0; i < profile.RecordLayout.Fields.Count; i++)
        {
            Assert.Equal(profile.RecordLayout.Fields[i].Name, reloaded.RecordLayout.Fields[i].Name);
            Assert.Equal(profile.RecordLayout.Fields[i].Offset, reloaded.RecordLayout.Fields[i].Offset);
            Assert.Equal(profile.RecordLayout.Fields[i].Width, reloaded.RecordLayout.Fields[i].Width);
            Assert.Equal(profile.RecordLayout.Fields[i].ReadStatus, reloaded.RecordLayout.Fields[i].ReadStatus);
            Assert.Equal(profile.RecordLayout.Fields[i].WriteStatus, reloaded.RecordLayout.Fields[i].WriteStatus);
            Assert.Equal(profile.RecordLayout.Fields[i].Transform, reloaded.RecordLayout.Fields[i].Transform);
        }
    }

    [Fact]
    public void ShippedProfileJson_LoadsAndMatchesSchemaVersion()
    {
        var docsJsonPath = ResolveRepoRelative(Path.Combine(
            "files", "pes2021", "player-memory", "pes2021-player-record-v1.json"));
        Assert.True(File.Exists(docsJsonPath), $"missing docs profile at '{docsJsonPath}'");

        var profile = Pes2021PlayerProfileLoader.LoadFromFile(docsJsonPath);
        Assert.Equal("pes2021.player-record.v1", profile.SchemaVersion);
        Assert.Equal("pes2021-player-edit-v1", profile.ProfileId);
        Assert.Equal(380, profile.Stride);
        Assert.False(string.IsNullOrEmpty(profile.Sha256));
        Assert.Equal(Pes2021PlayerEvidenceStatus.Candidate, profile.EvidenceStatus);
    }

    [Fact]
    public void ShippedProfileJson_HasAllMandatoryFieldsFromFeasibilityStudy()
    {
        var docsJsonPath = ResolveRepoRelative(Path.Combine(
            "files", "pes2021", "player-memory", "pes2021-player-record-v1.json"));
        var profile = Pes2021PlayerProfileLoader.LoadFromFile(docsJsonPath);

        var names = profile.RecordLayout.Fields.Select(f => f.Name).ToHashSet(StringComparer.Ordinal);
        var mandatory = new[]
        {
            "height", "weight", "playerId", "commentaryId",
            "playerName", "clubShirtName", "nationalShirtName",
            "nationality", "marketValue",
            "contractEndYear", "contractEndMonth", "contractEndDay",
            "affection", "affectionFlags", "teamRoleLevel", "staminaBar",
            "currentFormArrow", "unavailableDays", "transferFlags",
            "teamRole", "personalityAxes", "impact", "annualSalary",
            "unknown_12c", "unknown_12e", "unknown_178", "unknown_179",
        };

        foreach (var field in mandatory)
        {
            Assert.Contains(field, names);
        }
    }

    [Fact]
    public void Defaults_BuiltInProfile_HasSourcePathBuiltin()
    {
        var profile = Pes2021PlayerProfileDefaults.BuildBuiltIn();
        Assert.Equal("<builtin>", profile.SourcePath);
        Assert.Equal("pes2021-player-edit-v1", profile.ProfileId);
    }

    [Fact]
    public void FieldTableRenderer_RendersAllFieldsFromProfile_WithoutHandTypedOffsets()
    {
        var profile = Pes2021PlayerProfileDefaults.BuildBuiltIn();
        var md = RenderFieldTable(profile);

        Assert.Contains("| name | offset | width | type | read | write |", md);
        foreach (var field in profile.RecordLayout.Fields)
        {
            Assert.Contains($"| {field.Name} |", md);
        }
    }

    private static object BuildField(string name, int offset, int width = 1, bool sharedBitfield = false) => new
    {
        name,
        offset,
        width,
        type = "u8",
        signedness = "unsigned",
        endianness = "n/a",
        transform = "none",
        readStatus = "CANDIDATE",
        writeStatus = "CANDIDATE",
        validContexts = new[] { "EDIT_BASE_CONFIRMED" },
        sharedBitfield,
    };

    private static string CreateJson(params (string Key, object Value)[] entries)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            foreach (var (key, value) in entries)
            {
                WriteValue(writer, key, value);
            }

            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private static void WriteValue(Utf8JsonWriter writer, string key, object value)
    {
        writer.WritePropertyName(key);
        switch (value)
        {
            case string s:
                writer.WriteStringValue(s);
                break;
            case bool b:
                writer.WriteBooleanValue(b);
                break;
            case int i:
                writer.WriteNumberValue(i);
                break;
            case long l:
                writer.WriteNumberValue(l);
                break;
            case IEnumerable<object> seq:
                writer.WriteStartArray();
                foreach (var item in seq)
                {
                    WriteAnon(writer, item);
                }

                writer.WriteEndArray();
                break;
            default:
                WriteAnon(writer, value);
                break;
        }
    }

    private static void WriteAnon(Utf8JsonWriter writer, object value)
    {
        if (value is string s)
        {
            writer.WriteStringValue(s);
            return;
        }

        if (value is bool b)
        {
            writer.WriteBooleanValue(b);
            return;
        }

        if (value is int i)
        {
            writer.WriteNumberValue(i);
            return;
        }

        var properties = value.GetType().GetProperties();
        writer.WriteStartObject();
        foreach (var property in properties)
        {
            var propertyValue = property.GetValue(value);
            WriteValue(writer, property.Name, propertyValue!);
        }

        writer.WriteEndObject();
    }

    private static string Serialize(Pes2021PlayerProfile profile)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            writer.WriteString("schemaVersion", profile.SchemaVersion);
            writer.WriteString("profileId", profile.ProfileId);
            writer.WriteString("profileVersion", profile.ProfileVersion);
            writer.WriteString("evidenceStatus", "CANDIDATE");
            writer.WriteStartArray("processNames");
            foreach (var name in profile.ProcessNames)
            {
                writer.WriteStringValue(name);
            }

            writer.WriteEndArray();
            writer.WriteStartObject("recordLayout");
            writer.WriteNumber("stride", profile.Stride);
            writer.WriteNumber("startOffset", profile.RecordLayout.StartOffset);
            writer.WriteStartArray("fields");
            foreach (var field in profile.RecordLayout.Fields)
            {
                writer.WriteStartObject();
                writer.WriteString("name", field.Name);
                writer.WriteNumber("offset", field.Offset);
                writer.WriteNumber("width", field.Width);
                writer.WriteString("type", field.Type switch
                {
                    Pes2021PlayerFieldType.U8 => "u8",
                    Pes2021PlayerFieldType.I8 => "i8",
                    Pes2021PlayerFieldType.U16Le => "u16le",
                    Pes2021PlayerFieldType.U32Le => "u32le",
                    Pes2021PlayerFieldType.I32Le => "i32le",
                    Pes2021PlayerFieldType.FixedAscii => "fixedascii",
                    Pes2021PlayerFieldType.I8X4 => "i8x4",
                    _ => "unknown",
                });
                writer.WriteString("signedness", field.Signedness);
                writer.WriteString("endianness", field.Endianness);
                writer.WriteString("transform", field.Transform switch
                {
                    Pes2021PlayerTransform.None => "none",
                    Pes2021PlayerTransform.RawMul100Eur => "rawMul100Eur",
                    Pes2021PlayerTransform.TrimAsciiZ => "trimAsciiZ",
                    Pes2021PlayerTransform.Bitfield => "bitfield",
                    _ => "none",
                });
                writer.WriteString("readStatus", field.ReadStatus.ToString().ToUpperInvariant());
                writer.WriteString("writeStatus", field.WriteStatus.ToString().ToUpperInvariant());
                writer.WriteStartArray("validContexts");
                foreach (var context in field.ValidContexts)
                {
                    writer.WriteStringValue(context switch
                    {
                        Pes2021PlayerContext.EditBaseCandidate => "EDIT_BASE_CANDIDATE",
                        Pes2021PlayerContext.EditBaseConfirmed => "EDIT_BASE_CONFIRMED",
                        Pes2021PlayerContext.MasterLeagueCandidate => "MASTER_LEAGUE_CANDIDATE",
                        Pes2021PlayerContext.MasterLeagueConfirmed => "MASTER_LEAGUE_CONFIRMED",
                        Pes2021PlayerContext.UiOrRuntimeCache => "UI_OR_RUNTIME_CACHE",
                        Pes2021PlayerContext.UnknownContext => "UNKNOWN_CONTEXT",
                        _ => "UNKNOWN_CONTEXT",
                    });
                }

                writer.WriteEndArray();
                writer.WriteBoolean("sharedBitfield", field.SharedBitfield);
                if (field.Bits is { Count: > 0 })
                {
                    writer.WriteStartArray("bits");
                    foreach (var bit in field.Bits)
                    {
                        writer.WriteStartObject();
                        writer.WriteString("name", bit.Name);
                        writer.WriteNumber("bitStart", bit.BitStart);
                        writer.WriteNumber("bitLength", bit.BitLength);
                        writer.WriteString("readStatus", bit.ReadStatus.ToString().ToUpperInvariant());
                        writer.WriteString("writeStatus", bit.WriteStatus.ToString().ToUpperInvariant());
                        writer.WriteEndObject();
                    }

                    writer.WriteEndArray();
                }

                writer.WriteString("notes", field.Notes ?? string.Empty);
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
            writer.WriteEndObject();

            writer.WriteStartObject("recordValidation");
            writer.WriteNumber("minimumHeight", profile.RecordValidation.MinimumHeight);
            writer.WriteNumber("maximumHeight", profile.RecordValidation.MaximumHeight);
            writer.WriteNumber("minimumWeight", profile.RecordValidation.MinimumWeight);
            writer.WriteNumber("maximumWeight", profile.RecordValidation.MaximumWeight);
            writer.WriteNumber("minimumPlayerId", profile.RecordValidation.MinimumPlayerId);
            writer.WriteNumber("maximumPlayerId", profile.RecordValidation.MaximumPlayerId);
            writer.WriteEndObject();

            writer.WriteStartObject("regionFilter");
            WriteStringArray(writer, "states", profile.RegionFilter.States);
            WriteStringArray(writer, "types", profile.RegionFilter.Types);
            writer.WriteBoolean("requireReadable", profile.RegionFilter.RequireReadable);
            writer.WriteBoolean("requireWritable", profile.RegionFilter.RequireWritable);
            writer.WriteBoolean("allowExecutable", profile.RegionFilter.AllowExecutable);
            writer.WriteNumber("chunkBytes", profile.RegionFilter.ChunkBytes);
            writer.WriteEndObject();

            writer.WriteStartObject("anchorValidation");
            writer.WriteNumber("recordsBefore", profile.AnchorValidation.RecordsBefore);
            writer.WriteNumber("recordsAfter", profile.AnchorValidation.RecordsAfter);
            writer.WriteNumber("minimumRun", profile.AnchorValidation.MinimumRun);
            writer.WriteNumber("minimumAnchorScore", profile.AnchorValidation.MinimumAnchorScore);
            writer.WriteNumber("mediumScore", profile.AnchorValidation.MediumScore);
            writer.WriteNumber("highScore", profile.AnchorValidation.HighScore);
            writer.WriteStartArray("controlPlayerIds");
            foreach (var id in profile.AnchorValidation.ControlPlayerIds)
            {
                writer.WriteNumberValue(id);
            }

            writer.WriteEndArray();
            writer.WriteEndObject();

            writer.WriteStartObject("limits");
            writer.WriteNumber("defaultBlockRecords", profile.Limits.DefaultBlockRecords);
            writer.WriteNumber("maxBlockRecords", profile.Limits.MaxBlockRecords);
            writer.WriteNumber("maxRecordsReturned", profile.Limits.MaxRecordsReturned);
            writer.WriteNumber("scanBudgetMs", profile.Limits.ScanBudgetMs);
            writer.WriteEndObject();

            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private static void WriteStringArray(Utf8JsonWriter writer, string name, IReadOnlyList<string> values)
    {
        writer.WriteStartArray(name);
        foreach (var value in values)
        {
            writer.WriteStringValue(value);
        }

        writer.WriteEndArray();
    }

    private static string RenderFieldTable(Pes2021PlayerProfile profile)
    {
        var builder = new StringBuilder();
        builder.AppendLine("| name | offset | width | type | read | write |");
        builder.AppendLine("|---|---:|---:|---|---|---|");
        foreach (var field in profile.RecordLayout.Fields)
        {
            builder.Append("| ")
                .Append(field.Name)
                .Append(" | 0x").Append(field.Offset.ToString("X"))
                .Append(" | ")
                .Append(field.Width)
                .Append(" | ")
                .Append(field.Type)
                .Append(" | ")
                .Append(field.ReadStatus)
                .Append(" | ")
                .Append(field.WriteStatus)
                .AppendLine(" |");
        }

        return builder.ToString();
    }

    private static string ResolveRepoRelative(string relative)
    {
        var assemblyDirectory = Path.GetDirectoryName(typeof(Pes2021PlayerProfileTests).Assembly.Location)
            ?? AppContext.BaseDirectory;
        var root = Path.GetFullPath(Path.Combine(assemblyDirectory, "..", "..", "..", "..", ".."));
        return Path.Combine(root, relative.Replace('\\', Path.DirectorySeparatorChar));
    }
}
