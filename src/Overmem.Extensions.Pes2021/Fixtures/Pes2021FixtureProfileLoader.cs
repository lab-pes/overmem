using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text.Json;

namespace Overmem.Extensions.Pes2021.Fixtures;

/// <summary>
/// Loads and validates PES 2021 fixture profiles (schema
/// <c>pes2021.fixture-profile.v1</c>). The loader is strict: any offset outside the stride,
/// any unknown layout type, any non-positive limit or any unreadable path produces a
/// <see cref="Pes2021FixtureProfileException"/> carrying a stable error code. The caller
/// must fail before attaching to PES if the profile is invalid.
/// </summary>
public static class Pes2021FixtureProfileLoader
{
    public const string SupportedSchemaVersion = "pes2021.fixture-profile.v1";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public static Pes2021FixtureProfile LoadFromFile(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new Pes2021FixtureProfileException("PES2021_PROFILE_INVALID", "Profile path is empty.");
        }

        var absolutePath = Path.GetFullPath(path);
        if (!File.Exists(absolutePath))
        {
            throw new Pes2021FixtureProfileException("PES2021_PROFILE_INVALID", $"Profile not found at '{absolutePath}'.");
        }

        var bytes = File.ReadAllBytes(absolutePath);
        return LoadFromBytes(bytes, absolutePath);
    }

    public static Pes2021FixtureProfile LoadFromBytes(byte[] bytes, string sourcePath)
    {
        if (bytes is null || bytes.Length == 0)
        {
            throw new Pes2021FixtureProfileException("PES2021_PROFILE_INVALID", "Profile bytes are empty.");
        }

        var sha = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        using var document = JsonDocument.Parse(bytes);
        var root = document.RootElement;

        var schemaVersion = ReadString(root, "schemaVersion") ?? string.Empty;
        if (!string.Equals(schemaVersion, SupportedSchemaVersion, StringComparison.Ordinal))
        {
            throw new Pes2021FixtureProfileException(
                "PES2021_PROFILE_INVALID",
                $"Unsupported profile schemaVersion '{schemaVersion}'. Expected '{SupportedSchemaVersion}'.");
        }

        var profileId = RequireString(root, "profileId");
        var profileVersion = RequireString(root, "profileVersion");
        var evidenceStatus = ReadString(root, "evidenceStatus") ?? "unknown";
        var processNames = ReadStringArray(root, "processNames") ?? new[] { "PES2021" };

        if (!root.TryGetProperty("recordLayout", out var layoutElement))
        {
            throw new Pes2021FixtureProfileException("PES2021_PROFILE_INVALID", "recordLayout is required.");
        }

        var layout = ParseLayout(layoutElement);

        var calendar = ParseCalendarLimits(RequireObject(root, "calendar"));
        var recordValidation = ParseRecordValidation(RequireObject(root, "recordValidation"));
        var regionFilter = ParseRegionFilter(RequireObject(root, "regionFilter"));
        var anchorValidation = ParseAnchorValidation(RequireObject(root, "anchorValidation"));
        var normalization = ParseNormalization(RequireObject(root, "normalization"));

        Pes2021ProfileMaps maps = new(null, null);
        if (root.TryGetProperty("maps", out var mapsElement) && mapsElement.ValueKind == JsonValueKind.Object)
        {
            maps = new Pes2021ProfileMaps(
                ReadString(mapsElement, "competitionMapPath"),
                ReadString(mapsElement, "teamMapPath"));
        }

        return new Pes2021FixtureProfile(
            schemaVersion,
            profileId,
            profileVersion,
            evidenceStatus,
            processNames,
            layout,
            calendar,
            recordValidation,
            regionFilter,
            anchorValidation,
            normalization,
            maps,
            sha,
            sourcePath);
    }

    private static Pes2021RecordLayout ParseLayout(JsonElement layout)
    {
        var stride = ReadInt32(layout, "stride") ?? 0;
        if (stride <= 0)
        {
            throw new Pes2021FixtureProfileException("PES2021_PROFILE_INVALID", "recordLayout.stride must be positive.");
        }

        var competitionIdOffset = ReadFieldOffset(layout, "competitionId", stride);
        var roundOffset = ReadFieldOffset(layout, "round", stride);
        var yearOffset = ReadFieldOffset(layout, "year", stride);
        var monthOffset = ReadFieldOffset(layout, "month", stride);
        var dayOffset = ReadFieldOffset(layout, "day", stride);
        var homeIdOffset = ReadFieldOffset(layout, "homeTeamId", stride);
        var homeLigaOffset = ReadFieldOffset(layout, "homeTeamLiga", stride);
        var awayIdOffset = ReadFieldOffset(layout, "awayTeamId", stride);
        var awayLigaOffset = ReadFieldOffset(layout, "awayTeamLiga", stride);
        var homeScoreOffset = ReadFieldOffset(layout, "homeScoreRaw", stride);
        var awayScoreOffset = ReadFieldOffset(layout, "awayScoreRaw", stride);

        return new Pes2021RecordLayout(
            stride,
            competitionIdOffset,
            roundOffset,
            yearOffset,
            monthOffset,
            dayOffset,
            homeIdOffset,
            homeLigaOffset,
            awayIdOffset,
            awayLigaOffset,
            homeScoreOffset,
            awayScoreOffset);
    }

    private static int ReadFieldOffset(JsonElement parent, string name, int stride)
    {
        if (!parent.TryGetProperty(name, out var field))
        {
            throw new Pes2021FixtureProfileException("PES2021_PROFILE_INVALID", $"recordLayout.{name} is required.");
        }

        var offset = ReadInt32(field, "offset") ?? -1;
        var type = ReadString(field, "type");
        if (offset < 0 || offset >= stride)
        {
            throw new Pes2021FixtureProfileException(
                "PES2021_PROFILE_INVALID",
                $"recordLayout.{name}.offset {offset} is outside stride {stride}.");
        }

        if (string.Equals(type, "u8", StringComparison.Ordinal))
        {
            if (offset + 1 > stride)
            {
                throw new Pes2021FixtureProfileException(
                    "PES2021_PROFILE_INVALID",
                    $"recordLayout.{name} u8 byte {offset + 1} exceeds stride {stride}.");
            }
        }
        else if (string.Equals(type, "u16le", StringComparison.Ordinal))
        {
            if (offset + 2 > stride)
            {
                throw new Pes2021FixtureProfileException(
                    "PES2021_PROFILE_INVALID",
                    $"recordLayout.{name} u16le range {offset + 2} exceeds stride {stride}.");
            }
        }
        else
        {
            throw new Pes2021FixtureProfileException(
                "PES2021_PROFILE_INVALID",
                $"recordLayout.{name}.type '{type}' is not allowed. Use u8 or u16le.");
        }

        return offset;
    }

    private static Pes2021CalendarLimits ParseCalendarLimits(JsonElement calendar)
    {
        var defaultBlock = ReadInt32(calendar, "defaultBlockRecords") ?? 0;
        var maxBlock = ReadInt32(calendar, "maxBlockRecords") ?? 0;
        var recordLimit = ReadInt32(calendar, "recordLimit") ?? 0;
        var maxConsecutive = ReadInt32(calendar, "maxConsecutiveNonCompetitionRecords") ?? 0;

        if (defaultBlock <= 0)
        {
            throw new Pes2021FixtureProfileException("PES2021_PROFILE_INVALID", "calendar.defaultBlockRecords must be positive.");
        }

        if (maxBlock < defaultBlock)
        {
            throw new Pes2021FixtureProfileException(
                "PES2021_PROFILE_INVALID",
                "calendar.maxBlockRecords must be >= calendar.defaultBlockRecords.");
        }

        if (recordLimit <= 0)
        {
            throw new Pes2021FixtureProfileException("PES2021_PROFILE_INVALID", "calendar.recordLimit must be positive.");
        }

        if (maxConsecutive < 0)
        {
            throw new Pes2021FixtureProfileException(
                "PES2021_PROFILE_INVALID",
                "calendar.maxConsecutiveNonCompetitionRecords must be >= 0.");
        }

        return new Pes2021CalendarLimits(defaultBlock, maxBlock, recordLimit, maxConsecutive);
    }

    private static Pes2021RecordValidation ParseRecordValidation(JsonElement validation)
    {
        var minYear = ReadUInt16(validation, "minimumYear") ?? 0;
        var maxYear = ReadUInt16(validation, "maximumYear") ?? 0;
        if (minYear == 0 || maxYear == 0 || maxYear < minYear)
        {
            throw new Pes2021FixtureProfileException(
                "PES2021_PROFILE_INVALID",
                "recordValidation year range must satisfy minimumYear <= maximumYear with both > 0.");
        }

        var minRound = ReadByte(validation, "minimumRound") ?? 0;
        var maxRound = ReadByte(validation, "maximumRound") ?? 0;
        if (maxRound < minRound)
        {
            throw new Pes2021FixtureProfileException(
                "PES2021_PROFILE_INVALID",
                "recordValidation round range must satisfy minimumRound <= maximumRound.");
        }

        var sentinels = new List<ushort>();
        if (validation.TryGetProperty("teamIdSentinels", out var sentinelsElement)
            && sentinelsElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in sentinelsElement.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.Number && item.TryGetUInt16(out var value))
                {
                    sentinels.Add(value);
                }
            }
        }

        if (!sentinels.Contains(CompetitionId.SentinelValue))
        {
            sentinels.Add(CompetitionId.SentinelValue);
        }

        return new Pes2021RecordValidation(minYear, maxYear, minRound, maxRound, sentinels);
    }

    private static Pes2021RegionFilter ParseRegionFilter(JsonElement filter)
    {
        var states = ReadStringArray(filter, "states") ?? new[] { "Commit" };
        var types = ReadStringArray(filter, "types") ?? new[] { "Private" };
        var requireReadable = ReadBool(filter, "requireReadable") ?? true;
        var requireWritable = ReadBool(filter, "requireWritable") ?? true;
        var allowExecutable = ReadBool(filter, "allowExecutable") ?? false;
        var chunkBytes = ReadInt32(filter, "chunkBytes") ?? 1 << 20;
        if (chunkBytes <= 0)
        {
            throw new Pes2021FixtureProfileException("PES2021_PROFILE_INVALID", "regionFilter.chunkBytes must be positive.");
        }

        return new Pes2021RegionFilter(states, types, requireReadable, requireWritable, allowExecutable, chunkBytes);
    }

    private static Pes2021AnchorValidation ParseAnchorValidation(JsonElement anchor)
    {
        var before = ReadInt32(anchor, "recordsBefore") ?? 0;
        var after = ReadInt32(anchor, "recordsAfter") ?? 0;
        var minRun = ReadInt32(anchor, "minimumPlausibleRun") ?? 0;
        var minComp = ReadInt32(anchor, "minimumCompetitionRun") ?? 0;
        var medium = ReadInt32(anchor, "mediumScore") ?? 0;
        var high = ReadInt32(anchor, "highScore") ?? 0;

        if (before < 0 || after < 0)
        {
            throw new Pes2021FixtureProfileException("PES2021_PROFILE_INVALID", "anchorValidation.recordsBefore/recordsAfter must be >= 0.");
        }

        if (minRun <= 0)
        {
            throw new Pes2021FixtureProfileException("PES2021_PROFILE_INVALID", "anchorValidation.minimumPlausibleRun must be positive.");
        }

        if (minComp <= 0)
        {
            throw new Pes2021FixtureProfileException("PES2021_PROFILE_INVALID", "anchorValidation.minimumCompetitionRun must be positive.");
        }

        if (medium <= 0 || high <= medium)
        {
            throw new Pes2021FixtureProfileException(
                "PES2021_PROFILE_INVALID",
                "anchorValidation must satisfy 0 < mediumScore < highScore.");
        }

        return new Pes2021AnchorValidation(before, after, minRun, minComp, medium, high);
    }

    private static Pes2021Normalization ParseNormalization(JsonElement normalization)
    {
        var strategyText = ReadString(normalization, "strategy") ?? "competition-block-only";
        NormalizationStrategy strategy;
        switch (strategyText.ToLowerInvariant())
        {
            case "competition-block-only":
                strategy = NormalizationStrategy.CompetitionBlockOnly;
                break;
            case "known-season-start-index":
                strategy = NormalizationStrategy.KnownSeasonStartIndex;
                break;
            case "scan-array-boundary":
                strategy = NormalizationStrategy.ScanArrayBoundary;
                break;
            default:
                throw new Pes2021FixtureProfileException(
                    "PES2021_PROFILE_INVALID",
                    $"normalization.strategy '{strategyText}' is not supported.");
        }

        int? knownIndex = ReadInt32(normalization, "knownSeasonStartIndex");
        if (strategy == NormalizationStrategy.KnownSeasonStartIndex && (knownIndex is null || knownIndex < 0))
        {
            throw new Pes2021FixtureProfileException(
                "PES2021_PROFILE_INVALID",
                "normalization.knownSeasonStartIndex must be a non-negative integer when strategy = known-season-start-index.");
        }

        var validationIndices = new List<int>();
        if (normalization.TryGetProperty("validationSampleIndices", out var samples)
            && samples.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in samples.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.Number && item.TryGetInt32(out var index))
                {
                    validationIndices.Add(index);
                }
            }
        }

        return new Pes2021Normalization(strategy, knownIndex, validationIndices);
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
            throw new Pes2021FixtureProfileException(
                "PES2021_PROFILE_INVALID",
                $"'{propertyName}' is required and must be a non-empty string.");
        }

        return value;
    }

    private static JsonElement RequireObject(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property) || property.ValueKind != JsonValueKind.Object)
        {
            throw new Pes2021FixtureProfileException(
                "PES2021_PROFILE_INVALID",
                $"'{propertyName}' is required and must be an object.");
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

    private static ushort? ReadUInt16(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property) || property.ValueKind != JsonValueKind.Number)
        {
            return null;
        }

        return property.TryGetUInt16(out var value) ? value : (ushort?)null;
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

/// <summary>
/// Thrown when a fixture profile fails validation. The error code matches one of the stable
/// codes listed in <c>api.md</c> so CLI/MCP surfaces can render it without parsing the
/// message.
/// </summary>
public sealed class Pes2021FixtureProfileException : InvalidOperationException
{
    public Pes2021FixtureProfileException(string code, string message)
        : base($"[{code}] {message}")
    {
        Code = code;
    }

    public string Code { get; }
}
