using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text.Json;

namespace Overmem.Extensions.Pes2021.Players;

/// <summary>
/// Loads and validates PES 2021 player-record profiles (schema
/// <c>pes2021.player-record.v1</c>). The loader is strict: any offset outside the stride,
/// any overlapping fields that are not declared <c>sharedBitfield</c>, any unknown layout
/// type, any invalid bit range, any unknown transform or evidence status, or any
/// unreadable path produces a <see cref="Pes2021PlayerProfileException"/> carrying the
/// stable code <c>PES2021_PLAYER_PROFILE_INVALID</c>. The caller must fail before any
/// memory work is attempted if the profile is invalid.
/// </summary>
public static class Pes2021PlayerProfileLoader
{
    public const string SupportedSchemaVersion = "pes2021.player-record.v1";
    public const string InvalidProfileCode = "PES2021_PLAYER_PROFILE_INVALID";
    public const int ExpectedStride = 380;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public static Pes2021PlayerProfile LoadFromFile(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new Pes2021PlayerProfileException(InvalidProfileCode, "Profile path is empty.");
        }

        var absolutePath = Path.GetFullPath(path);
        if (!File.Exists(absolutePath))
        {
            throw new Pes2021PlayerProfileException(InvalidProfileCode, $"Profile not found at '{absolutePath}'.");
        }

        var bytes = File.ReadAllBytes(absolutePath);
        return LoadFromBytes(bytes, absolutePath);
    }

    public static Pes2021PlayerProfile LoadFromBytes(byte[] bytes, string sourcePath)
    {
        if (bytes is null || bytes.Length == 0)
        {
            throw new Pes2021PlayerProfileException(InvalidProfileCode, "Profile bytes are empty.");
        }

        var sha = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        using var document = JsonDocument.Parse(bytes);
        var root = document.RootElement;

        var schemaVersion = ReadString(root, "schemaVersion") ?? string.Empty;
        if (!string.Equals(schemaVersion, SupportedSchemaVersion, StringComparison.Ordinal))
        {
            throw new Pes2021PlayerProfileException(
                InvalidProfileCode,
                $"Unsupported profile schemaVersion '{schemaVersion}'. Expected '{SupportedSchemaVersion}'.");
        }

        var profileId = RequireString(root, "profileId");
        var profileVersion = RequireString(root, "profileVersion");
        var evidenceStatus = ParseEvidenceStatus(ReadString(root, "evidenceStatus"));
        var processNames = ReadStringArray(root, "processNames") ?? new[] { "PES2021" };

        if (!root.TryGetProperty("recordLayout", out var layoutElement))
        {
            throw new Pes2021PlayerProfileException(InvalidProfileCode, "recordLayout is required.");
        }

        var (stride, startOffset, fields) = ParseLayout(layoutElement);
        var validation = ParseValidation(RequireObject(root, "recordValidation"));
        var regionFilter = ParseRegionFilter(RequireObject(root, "regionFilter"));
        var anchorValidation = ParseAnchorValidation(RequireObject(root, "anchorValidation"));
        var limits = ParseLimits(RequireObject(root, "limits"));
        var sources = ParseSources(ReadObject(root, "sources"));

        return new Pes2021PlayerProfile(
            schemaVersion,
            profileId,
            profileVersion,
            evidenceStatus,
            processNames,
            new Pes2021PlayerRecordLayout(stride, startOffset, fields),
            validation,
            regionFilter,
            anchorValidation,
            limits,
            sources,
            sha,
            sourcePath);
    }

    private static (int Stride, int StartOffset, IReadOnlyList<Pes2021PlayerFieldDefinition> Fields) ParseLayout(
        JsonElement layout)
    {
        var stride = ReadInt32(layout, "stride") ?? 0;
        if (stride != ExpectedStride)
        {
            throw new Pes2021PlayerProfileException(
                InvalidProfileCode,
                $"recordLayout.stride must be exactly {ExpectedStride} (0x{ExpectedStride:X}). Got {stride}.");
        }

        var startOffset = ReadInt32(layout, "startOffset") ?? 0;
        if (startOffset < 0 || startOffset >= stride)
        {
            throw new Pes2021PlayerProfileException(
                InvalidProfileCode,
                $"recordLayout.startOffset {startOffset} must be in [0, stride).");
        }

        if (!layout.TryGetProperty("fields", out var fieldsElement) || fieldsElement.ValueKind != JsonValueKind.Array)
        {
            throw new Pes2021PlayerProfileException(InvalidProfileCode, "recordLayout.fields must be an array.");
        }

        var seenNames = new HashSet<string>(System.StringComparer.Ordinal);
        var fields = new List<Pes2021PlayerFieldDefinition>();
        foreach (var fieldElement in fieldsElement.EnumerateArray())
        {
            var field = ParseField(fieldElement, stride);
            if (!seenNames.Add(field.Name))
            {
                throw new Pes2021PlayerProfileException(
                    InvalidProfileCode,
                    $"Duplicate field name '{field.Name}'.");
            }

            fields.Add(field);
        }

        ValidateNoUnjustifiedOverlap(fields, stride);
        return (stride, startOffset, fields);
    }

    private static Pes2021PlayerFieldDefinition ParseField(JsonElement fieldElement, int stride)
    {
        var name = RequireString(fieldElement, "name");
        var offset = ReadInt32(fieldElement, "offset") ?? -1;
        var width = ReadInt32(fieldElement, "width") ?? 0;
        var typeText = RequireString(fieldElement, "type");
        var signedness = ReadString(fieldElement, "signedness") ?? "unsigned";
        var endianness = ReadString(fieldElement, "endianness") ?? "n/a";
        var transformText = ReadString(fieldElement, "transform") ?? "none";
        var readStatusText = RequireString(fieldElement, "readStatus");
        var writeStatusText = RequireString(fieldElement, "writeStatus");
        var sharedBitfield = ReadBool(fieldElement, "sharedBitfield") ?? false;
        var notes = ReadString(fieldElement, "notes");

        if (offset < 0 || offset >= stride)
        {
            throw new Pes2021PlayerProfileException(
                InvalidProfileCode,
                $"field '{name}' offset {offset} is outside stride {stride}.");
        }

        if (width <= 0)
        {
            throw new Pes2021PlayerProfileException(
                InvalidProfileCode,
                $"field '{name}' width must be positive.");
        }

        if (!TryParseFieldType(typeText, out var fieldType))
        {
            throw new Pes2021PlayerProfileException(
                InvalidProfileCode,
                $"field '{name}' has unsupported type '{typeText}'.");
        }

        if (!TryParseTransform(transformText, out var transform))
        {
            throw new Pes2021PlayerProfileException(
                InvalidProfileCode,
                $"field '{name}' has unsupported transform '{transformText}'.");
        }

        var readStatus = ParseEvidenceStatus(readStatusText);
        var writeStatus = ParseEvidenceStatus(writeStatusText);

        if (!TryParseSignedness(signedness, out var signednessValue))
        {
            throw new Pes2021PlayerProfileException(
                InvalidProfileCode,
                $"field '{name}' has unsupported signedness '{signedness}'.");
        }

        if (!TryParseEndianness(endianness, out var endiannessValue))
        {
            throw new Pes2021PlayerProfileException(
                InvalidProfileCode,
                $"field '{name}' has unsupported endianness '{endianness}'.");
        }

        ValidateTypeMatches(fieldName: name, fieldType: fieldType, width: width, stride: stride, offset: offset,
            signedness: signednessValue, endianness: endiannessValue, transform: transform);

        var contexts = ParseContexts(fieldElement, name);

        IReadOnlyList<Pes2021PlayerBitField>? bits = null;
        if (fieldElement.TryGetProperty("bits", out var bitsElement) && bitsElement.ValueKind == JsonValueKind.Array)
        {
            bits = ParseBits(bitsElement, name, width);
        }

        if (fieldType == Pes2021PlayerFieldType.FixedAscii && transform != Pes2021PlayerTransform.TrimAsciiZ)
        {
            throw new Pes2021PlayerProfileException(
                InvalidProfileCode,
                $"field '{name}' of type fixedAscii must declare transform 'trimAsciiZ'.");
        }

        if (fieldType == Pes2021PlayerFieldType.I8X4 && width != 4)
        {
            throw new Pes2021PlayerProfileException(
                InvalidProfileCode,
                $"field '{name}' of type i8x4 must have width 4. Got {width}.");
        }

        return new Pes2021PlayerFieldDefinition(
            name,
            offset,
            width,
            fieldType,
            signednessValue,
            endiannessValue,
            transform,
            readStatus,
            writeStatus,
            contexts,
            sharedBitfield,
            bits,
            notes);
    }

    private static IReadOnlyList<Pes2021PlayerBitField> ParseBits(JsonElement bitsElement, string containerName, int width)
    {
        var bits = new List<Pes2021PlayerBitField>();
        var seenNames = new HashSet<string>(System.StringComparer.Ordinal);
        var bitCapacity = width * 8;
        foreach (var bitElement in bitsElement.EnumerateArray())
        {
            var name = RequireString(bitElement, "name");
            if (!seenNames.Add(name))
            {
                throw new Pes2021PlayerProfileException(
                    InvalidProfileCode,
                    $"bitfield '{name}' in '{containerName}' is duplicated.");
            }

            var bitStart = ReadInt32(bitElement, "bitStart") ?? -1;
            var bitLength = ReadInt32(bitElement, "bitLength") ?? 0;
            var readText = ReadString(bitElement, "readStatus") ?? "CANDIDATE";
            var writeText = ReadString(bitElement, "writeStatus") ?? "BLOCKED";

            if (bitStart < 0)
            {
                throw new Pes2021PlayerProfileException(
                    InvalidProfileCode,
                    $"bit '{name}' in '{containerName}' has negative bitStart {bitStart}.");
            }

            if (bitLength <= 0)
            {
                throw new Pes2021PlayerProfileException(
                    InvalidProfileCode,
                    $"bit '{name}' in '{containerName}' has non-positive bitLength {bitLength}.");
            }

            if (bitStart + bitLength > bitCapacity)
            {
                throw new Pes2021PlayerProfileException(
                    InvalidProfileCode,
                    $"bit '{name}' in '{containerName}' range [{bitStart}, {bitStart + bitLength}) exceeds container width {bitCapacity} bits.");
            }

            bits.Add(new Pes2021PlayerBitField(
                name,
                bitStart,
                bitLength,
                ParseEvidenceStatus(readText),
                ParseEvidenceStatus(writeText)));
        }

        return bits;
    }

    private static IReadOnlyList<Pes2021PlayerContext> ParseContexts(JsonElement fieldElement, string fieldName)
    {
        if (!fieldElement.TryGetProperty("validContexts", out var contextsElement)
            || contextsElement.ValueKind != JsonValueKind.Array)
        {
            throw new Pes2021PlayerProfileException(
                InvalidProfileCode,
                $"field '{fieldName}' must declare a validContexts array.");
        }

        var contexts = new List<Pes2021PlayerContext>();
        foreach (var contextElement in contextsElement.EnumerateArray())
        {
            if (contextElement.ValueKind != JsonValueKind.String)
            {
                throw new Pes2021PlayerProfileException(
                    InvalidProfileCode,
                    $"field '{fieldName}' validContexts must contain only string labels.");
            }

            var label = contextElement.GetString() ?? string.Empty;
            if (!TryParseContext(label, out var context))
            {
                throw new Pes2021PlayerProfileException(
                    InvalidProfileCode,
                    $"field '{fieldName}' has unsupported context '{label}'.");
            }

            contexts.Add(context);
        }

        if (contexts.Count == 0)
        {
            throw new Pes2021PlayerProfileException(
                InvalidProfileCode,
                $"field '{fieldName}' must declare at least one valid context.");
        }

        return contexts;
    }

    private static Pes2021PlayerRecordValidation ParseValidation(JsonElement validation)
    {
        var minHeight = ReadByte(validation, "minimumHeight") ?? 0;
        var maxHeight = ReadByte(validation, "maximumHeight") ?? 0;
        var minWeight = ReadByte(validation, "minimumWeight") ?? 0;
        var maxWeight = ReadByte(validation, "maximumWeight") ?? 0;
        var minId = ReadUInt32(validation, "minimumPlayerId") ?? 0;
        var maxId = ReadUInt32(validation, "maximumPlayerId") ?? 0;

        if (minHeight == 0 || maxHeight == 0 || maxHeight < minHeight)
        {
            throw new Pes2021PlayerProfileException(
                InvalidProfileCode,
                "recordValidation height range must satisfy minimumHeight <= maximumHeight with both > 0.");
        }

        if (minWeight == 0 || maxWeight == 0 || maxWeight < minWeight)
        {
            throw new Pes2021PlayerProfileException(
                InvalidProfileCode,
                "recordValidation weight range must satisfy minimumWeight <= maximumWeight with both > 0.");
        }

        if (minId == 0 || maxId == 0 || maxId < minId)
        {
            throw new Pes2021PlayerProfileException(
                InvalidProfileCode,
                "recordValidation playerId range must satisfy minimumPlayerId <= maximumPlayerId with both > 0.");
        }

        return new Pes2021PlayerRecordValidation(minHeight, maxHeight, minWeight, maxWeight, minId, maxId);
    }

    private static Pes2021PlayerRegionFilter ParseRegionFilter(JsonElement filter)
    {
        var states = ReadStringArray(filter, "states") ?? new[] { "Commit" };
        var types = ReadStringArray(filter, "types") ?? new[] { "Private" };
        var requireReadable = ReadBool(filter, "requireReadable") ?? true;
        var requireWritable = ReadBool(filter, "requireWritable") ?? true;
        var allowExecutable = ReadBool(filter, "allowExecutable") ?? false;
        var chunkBytes = ReadInt32(filter, "chunkBytes") ?? 1 << 20;
        if (chunkBytes <= 0)
        {
            throw new Pes2021PlayerProfileException(InvalidProfileCode, "regionFilter.chunkBytes must be positive.");
        }

        return new Pes2021PlayerRegionFilter(states, types, requireReadable, requireWritable, allowExecutable, chunkBytes);
    }

    private static Pes2021PlayerAnchorValidation ParseAnchorValidation(JsonElement anchor)
    {
        var before = ReadInt32(anchor, "recordsBefore") ?? 0;
        var after = ReadInt32(anchor, "recordsAfter") ?? 0;
        var minRun = ReadInt32(anchor, "minimumRun") ?? 0;
        var minScore = ReadInt32(anchor, "minimumAnchorScore") ?? 0;
        var medium = ReadInt32(anchor, "mediumScore") ?? 0;
        var high = ReadInt32(anchor, "highScore") ?? 0;

        if (before < 0 || after < 0)
        {
            throw new Pes2021PlayerProfileException(
                InvalidProfileCode,
                "anchorValidation.recordsBefore/recordsAfter must be >= 0.");
        }

        if (minRun <= 0)
        {
            throw new Pes2021PlayerProfileException(
                InvalidProfileCode,
                "anchorValidation.minimumRun must be positive.");
        }

        if (minScore <= 0)
        {
            throw new Pes2021PlayerProfileException(
                InvalidProfileCode,
                "anchorValidation.minimumAnchorScore must be positive.");
        }

        if (medium <= 0 || high <= medium)
        {
            throw new Pes2021PlayerProfileException(
                InvalidProfileCode,
                "anchorValidation must satisfy 0 < mediumScore < highScore.");
        }

        var controlIds = new List<uint>();
        if (anchor.TryGetProperty("controlPlayerIds", out var controlElement)
            && controlElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in controlElement.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.Number && item.TryGetUInt32(out var value))
                {
                    controlIds.Add(value);
                }
            }
        }

        return new Pes2021PlayerAnchorValidation(before, after, minRun, minScore, medium, high, controlIds);
    }

    private static Pes2021PlayerLimits ParseLimits(JsonElement limits)
    {
        var defaultBlock = ReadInt32(limits, "defaultBlockRecords") ?? 0;
        var maxBlock = ReadInt32(limits, "maxBlockRecords") ?? 0;
        var maxReturned = ReadInt32(limits, "maxRecordsReturned") ?? 0;
        var budget = ReadInt32(limits, "scanBudgetMs") ?? 0;

        if (defaultBlock <= 0)
        {
            throw new Pes2021PlayerProfileException(InvalidProfileCode, "limits.defaultBlockRecords must be positive.");
        }

        if (maxBlock < defaultBlock)
        {
            throw new Pes2021PlayerProfileException(
                InvalidProfileCode,
                "limits.maxBlockRecords must be >= limits.defaultBlockRecords.");
        }

        if (maxReturned <= 0)
        {
            throw new Pes2021PlayerProfileException(InvalidProfileCode, "limits.maxRecordsReturned must be positive.");
        }

        if (budget <= 0)
        {
            throw new Pes2021PlayerProfileException(InvalidProfileCode, "limits.scanBudgetMs must be positive.");
        }

        return new Pes2021PlayerLimits(defaultBlock, maxBlock, maxReturned, budget);
    }

    private static Pes2021PlayerProfileSources ParseSources(JsonElement? sourcesElement)
    {
        if (sourcesElement is null)
        {
            return new Pes2021PlayerProfileSources(null, null, null);
        }

        var element = sourcesElement.Value;
        string? ctPath = null;
        string? ctSha = null;
        string? schemaSha = null;

        if (element.TryGetProperty("ct", out var ctElement) && ctElement.ValueKind == JsonValueKind.Object)
        {
            ctPath = ReadString(ctElement, "path");
            ctSha = ReadString(ctElement, "sha256");
        }

        if (element.TryGetProperty("schema_v5_lua", out var schemaElement)
            && schemaElement.ValueKind == JsonValueKind.Object)
        {
            schemaSha = ReadString(schemaElement, "expectedSha256");
        }

        return new Pes2021PlayerProfileSources(ctPath, ctSha, schemaSha);
    }

    private static void ValidateNoUnjustifiedOverlap(
        IReadOnlyList<Pes2021PlayerFieldDefinition> fields,
        int stride)
    {
        var ranges = new List<(int Start, int End, string Name, bool Shared)>();
        foreach (var field in fields)
        {
            var end = field.Offset + field.Width;
            if (end > stride)
            {
                throw new Pes2021PlayerProfileException(
                    InvalidProfileCode,
                    $"field '{field.Name}' range [{field.Offset}, {end}) exceeds stride {stride}.");
            }

            ranges.Add((field.Offset, end, field.Name, field.SharedBitfield));
        }

        ranges.Sort((a, b) => a.Start.CompareTo(b.Start));
        for (var i = 1; i < ranges.Count; i++)
        {
            var previous = ranges[i - 1];
            var current = ranges[i];
            if (current.Start < previous.End)
            {
                if (!previous.Shared || !current.Shared)
                {
                    throw new Pes2021PlayerProfileException(
                        InvalidProfileCode,
                        $"field '{current.Name}' [{current.Start}, {current.Start + 1}) overlaps field '{previous.Name}' without a sharedBitfield declaration.");
                }
            }
        }
    }

    private static void ValidateTypeMatches(
        string fieldName,
        Pes2021PlayerFieldType fieldType,
        int width,
        int stride,
        int offset,
        string signedness,
        string endianness,
        Pes2021PlayerTransform transform)
    {
        switch (fieldType)
        {
            case Pes2021PlayerFieldType.U8:
                RequireWidth(fieldName, width, 1);
                RequireSignedness(fieldName, signedness, "unsigned");
                RequireEndianness(fieldName, endianness, "n/a");
                break;
            case Pes2021PlayerFieldType.I8:
                RequireWidth(fieldName, width, 1);
                RequireSignedness(fieldName, signedness, "signed");
                RequireEndianness(fieldName, endianness, "n/a");
                break;
            case Pes2021PlayerFieldType.U16Le:
                RequireWidth(fieldName, width, 2);
                RequireSignedness(fieldName, signedness, "unsigned");
                RequireEndianness(fieldName, endianness, "little");
                break;
            case Pes2021PlayerFieldType.U32Le:
                RequireWidth(fieldName, width, 4);
                RequireSignedness(fieldName, signedness, "unsigned");
                RequireEndianness(fieldName, endianness, "little");
                break;
            case Pes2021PlayerFieldType.I32Le:
                RequireWidth(fieldName, width, 4);
                RequireSignedness(fieldName, signedness, "signed");
                RequireEndianness(fieldName, endianness, "little");
                break;
            case Pes2021PlayerFieldType.FixedAscii:
                RequireSignedness(fieldName, signedness, "n/a");
                RequireEndianness(fieldName, endianness, "n/a");
                if (transform != Pes2021PlayerTransform.TrimAsciiZ)
                {
                    throw new Pes2021PlayerProfileException(
                        InvalidProfileCode,
                        $"field '{fieldName}' of type fixedAscii must declare transform 'trimAsciiZ'.");
                }

                break;
            case Pes2021PlayerFieldType.I8X4:
                RequireWidth(fieldName, width, 4);
                RequireSignedness(fieldName, signedness, "signed");
                RequireEndianness(fieldName, endianness, "n/a");
                break;
            default:
                throw new Pes2021PlayerProfileException(
                    InvalidProfileCode,
                    $"field '{fieldName}' has unsupported type '{fieldType}'.");
        }

        if (offset + width > stride)
        {
            throw new Pes2021PlayerProfileException(
                InvalidProfileCode,
                $"field '{fieldName}' byte range [{offset}, {offset + width}) exceeds stride {stride}.");
        }
    }

    private static void RequireWidth(string fieldName, int width, int expected)
    {
        if (width != expected)
        {
            throw new Pes2021PlayerProfileException(
                InvalidProfileCode,
                $"field '{fieldName}' width must be {expected} for its declared type. Got {width}.");
        }
    }

    private static void RequireSignedness(string fieldName, string actual, string expected)
    {
        if (!string.Equals(actual, expected, StringComparison.Ordinal))
        {
            throw new Pes2021PlayerProfileException(
                InvalidProfileCode,
                $"field '{fieldName}' signedness must be '{expected}'. Got '{actual}'.");
        }
    }

    private static void RequireEndianness(string fieldName, string actual, string expected)
    {
        if (!string.Equals(actual, expected, StringComparison.Ordinal))
        {
            throw new Pes2021PlayerProfileException(
                InvalidProfileCode,
                $"field '{fieldName}' endianness must be '{expected}'. Got '{actual}'.");
        }
    }

    private static bool TryParseFieldType(string text, out Pes2021PlayerFieldType fieldType)
    {
        switch (text.ToLowerInvariant())
        {
            case "u8":
                fieldType = Pes2021PlayerFieldType.U8;
                return true;
            case "i8":
                fieldType = Pes2021PlayerFieldType.I8;
                return true;
            case "u16le":
                fieldType = Pes2021PlayerFieldType.U16Le;
                return true;
            case "u32le":
                fieldType = Pes2021PlayerFieldType.U32Le;
                return true;
            case "i32le":
                fieldType = Pes2021PlayerFieldType.I32Le;
                return true;
            case "fixedascii":
                fieldType = Pes2021PlayerFieldType.FixedAscii;
                return true;
            case "i8x4":
                fieldType = Pes2021PlayerFieldType.I8X4;
                return true;
            default:
                fieldType = default;
                return false;
        }
    }

    private static bool TryParseTransform(string text, out Pes2021PlayerTransform transform)
    {
        switch (text.ToLowerInvariant())
        {
            case "none":
                transform = Pes2021PlayerTransform.None;
                return true;
            case "rawmul100eur":
                transform = Pes2021PlayerTransform.RawMul100Eur;
                return true;
            case "trimasciiz":
                transform = Pes2021PlayerTransform.TrimAsciiZ;
                return true;
            case "bitfield":
                transform = Pes2021PlayerTransform.Bitfield;
                return true;
            default:
                transform = default;
                return false;
        }
    }

    private static bool TryParseSignedness(string text, out string signedness)
    {
        if (string.Equals(text, "unsigned", StringComparison.OrdinalIgnoreCase)
            || string.Equals(text, "signed", StringComparison.OrdinalIgnoreCase)
            || string.Equals(text, "n/a", StringComparison.OrdinalIgnoreCase))
        {
            signedness = text.ToLowerInvariant();
            return true;
        }

        signedness = string.Empty;
        return false;
    }

    private static bool TryParseEndianness(string text, out string endianness)
    {
        if (string.Equals(text, "little", StringComparison.OrdinalIgnoreCase)
            || string.Equals(text, "n/a", StringComparison.OrdinalIgnoreCase))
        {
            endianness = text.ToLowerInvariant();
            return true;
        }

        endianness = string.Empty;
        return false;
    }

    private static Pes2021PlayerEvidenceStatus ParseEvidenceStatus(string? text)
    {
        var value = text ?? string.Empty;
        switch (value.ToLowerInvariant())
        {
            case "confirmed":
                return Pes2021PlayerEvidenceStatus.Confirmed;
            case "candidate":
                return Pes2021PlayerEvidenceStatus.Candidate;
            case "unknown":
                return Pes2021PlayerEvidenceStatus.Unknown;
            case "refuted":
                return Pes2021PlayerEvidenceStatus.Refuted;
            default:
                throw new Pes2021PlayerProfileException(
                    InvalidProfileCode,
                    $"Unsupported evidence status '{text}'.");
        }
    }

    private static bool TryParseContext(string text, out Pes2021PlayerContext context)
    {
        switch (text.ToUpperInvariant())
        {
            case "EDIT_BASE_CANDIDATE":
                context = Pes2021PlayerContext.EditBaseCandidate;
                return true;
            case "EDIT_BASE_CONFIRMED":
                context = Pes2021PlayerContext.EditBaseConfirmed;
                return true;
            case "MASTER_LEAGUE_CANDIDATE":
                context = Pes2021PlayerContext.MasterLeagueCandidate;
                return true;
            case "MASTER_LEAGUE_CONFIRMED":
                context = Pes2021PlayerContext.MasterLeagueConfirmed;
                return true;
            case "UI_OR_RUNTIME_CACHE":
                context = Pes2021PlayerContext.UiOrRuntimeCache;
                return true;
            case "UNKNOWN_CONTEXT":
                context = Pes2021PlayerContext.UnknownContext;
                return true;
            default:
                context = default;
                return false;
        }
    }

    private static string? ReadString(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property))
        {
            return null;
        }

        return property.ValueKind == JsonValueKind.String ? property.GetString() : property.GetRawText();
    }

    private static string RequireString(JsonElement element, string propertyName)
    {
        var value = ReadString(element, propertyName);
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new Pes2021PlayerProfileException(
                InvalidProfileCode,
                $"'{propertyName}' is required and must be a non-empty string.");
        }

        return value;
    }

    private static JsonElement RequireObject(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property) || property.ValueKind != JsonValueKind.Object)
        {
            throw new Pes2021PlayerProfileException(
                InvalidProfileCode,
                $"'{propertyName}' is required and must be an object.");
        }

        return property;
    }

    private static JsonElement? ReadObject(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property) || property.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        return property;
    }

    private static IReadOnlyList<string>? ReadStringArray(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property) || property.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        var list = new List<string>();
        foreach (var item in property.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.String)
            {
                list.Add(item.GetString() ?? string.Empty);
            }
        }

        return list;
    }

    private static uint? ReadUInt32(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property) || property.ValueKind != JsonValueKind.Number)
        {
            return null;
        }

        return property.TryGetUInt32(out var value) ? value : (uint?)null;
    }

    private static byte? ReadByte(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property) || property.ValueKind != JsonValueKind.Number)
        {
            return null;
        }

        return property.TryGetByte(out var value) ? value : (byte?)null;
    }

    private static int? ReadInt32(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property) || property.ValueKind != JsonValueKind.Number)
        {
            return null;
        }

        return property.TryGetInt32(out var value) ? value : (int?)null;
    }

    private static bool? ReadBool(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property))
        {
            return null;
        }

        return property.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            _ => null,
        };
    }
}
