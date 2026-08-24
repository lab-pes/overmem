using System.Globalization;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace Overmem.Extensions.Pes2021;

internal static class Pes2021AgendaProfile
{
    internal const int RecordStride = 0x254;
    internal const int SecondaryDayStride = 0x2C4;
    internal const int SecondaryDayCount = 365;
    internal const int SecondaryHeaderMaxEvents = 17;
    internal const int SecondaryHeaderEventSize = 8;
    internal const int SecondaryItemsStart = 0x8C;
    internal const int SecondaryItemsEnd = 0x2B8;
    internal const int SecondaryCountOffset = 0x2BC;
    internal const int SecondaryScoreThreshold = 20;

    internal static readonly int[] SeasonAnchorYears = [2026, 2025, 2027, 2024, 2028, 2023, 2029];

    internal static readonly int[] SecondarySampleDays = [0, 1, 31, 90, 181, 270, 363, 364];

    internal static readonly Pes2021CalendarSearchPriority[] SearchPriorities =
    [
        new("primary", "match_array", "array principal de partidas; stride 0x254; melhor ancora para semantica bruta"),
        new("secondary", "secondary_calendar", "DayEntry de 365 dias; stride 0x2C4; header+items+count; melhor alvo para calendario visivel"),
        new("third", "player_event_table", "regiao em torno de 0xACCC00 no save; ainda semantica parcial; manter em observacao"),
        new("fallback", "cache_copa_brasil", "regiao 0xBEB3A8 no save; cache especializado; util como apoio, nao como modelo geral"),
        new("fallback", "region_22xxxx", "fixure/index estatico sem Y/M/D confirmado; manter anotada, sem prioridade atual"),
        new("fallback", "schedule_ai_168", "regiao 0xC016A8 no save; schedule AI de comp 168; manter 0xC016B0 como alias legada nos materiais antigos"),
    ];

    internal static readonly string[] DefaultCheatTablePaths =
    [
        Path.Combine(AppContext.BaseDirectory, "files", "PES 2021 - v21.1.0.CT"),
        Path.Combine(Environment.CurrentDirectory, "files", "PES 2021 - v21.1.0.CT"),
    ];

    internal static readonly string[] DefaultCompetitionMapPaths =
    [
        Path.Combine(AppContext.BaseDirectory, "files", "map_competitions-from-edit.txt"),
        Path.Combine(Environment.CurrentDirectory, "files", "map_competitions-from-edit.txt"),
    ];

    internal static Pes2021AgendaGuide LoadGuide(string? cheatTablePath = null)
    {
        var resolvedCheatTablePath = ResolveFirstExistingPath(cheatTablePath, DefaultCheatTablePaths);
        if (resolvedCheatTablePath is null)
        {
            return new Pes2021AgendaGuide(
                CheatTablePath: cheatTablePath ?? DefaultCheatTablePaths[0],
                CheatTableFound: false,
                InspectorScriptPath: null,
                InspectorScriptFound: false,
                CompetitionMapPath: null,
                CompetitionMapFound: false,
                RecordStride,
                SecondaryDayStride,
                SecondaryCountOffset,
                SecondaryItemsStart,
                SecondaryItemsEnd,
                SecondaryHeaderMaxEvents,
                SecondaryHeaderEventSize,
                SecondaryScoreThreshold,
                SeasonAnchorYears,
                SecondarySampleDays,
                SearchPriorities,
                [],
                BuildRecommendedCommands());
        }

        var ctText = File.ReadAllText(resolvedCheatTablePath);
        var cheatTable = XDocument.Parse(ctText, LoadOptions.PreserveWhitespace | LoadOptions.SetLineInfo);
        var bootstrapScript = cheatTable.Root?.Element("LuaScript")?.Value ?? string.Empty;
        var inspectorScriptPath = ExtractFirstMatch(bootstrapScript, @"local\s+inspectorFile\s*=\s*'(?<path>[^']+)'");
        var inspectorScriptFound = inspectorScriptPath is not null && File.Exists(inspectorScriptPath);
        var inspectorScriptText = inspectorScriptFound && inspectorScriptPath is not null
            ? File.ReadAllText(inspectorScriptPath)
            : bootstrapScript;

        var competitionMapPath = TryResolveCompetitionMapPath(inspectorScriptPath);
        var competitionMapFound = competitionMapPath is not null && File.Exists(competitionMapPath);

        return new Pes2021AgendaGuide(
            CheatTablePath: resolvedCheatTablePath,
            CheatTableFound: true,
            InspectorScriptPath: inspectorScriptPath,
            InspectorScriptFound: inspectorScriptFound,
            CompetitionMapPath: competitionMapPath,
            CompetitionMapFound: competitionMapFound,
            RecordStride: ExtractInt(inspectorScriptText, @"local\s+RECORD_SZ\s*=\s*0x(?<value>[0-9A-Fa-f]+)", RecordStride, hex: true),
            SecondaryDayStride: ExtractInt(inspectorScriptText, @"local\s+SECONDARY_DAY_STRIDE\s*=\s*0x(?<value>[0-9A-Fa-f]+)", SecondaryDayStride, hex: true),
            SecondaryCountOffset: ExtractInt(inspectorScriptText, @"local\s+SECONDARY_COUNT_OFFSET\s*=\s*0x(?<value>[0-9A-Fa-f]+)", SecondaryCountOffset, hex: true),
            SecondaryItemsStart: ExtractInt(inspectorScriptText, @"local\s+SECONDARY_ITEMS_START\s*=\s*0x(?<value>[0-9A-Fa-f]+)", SecondaryItemsStart, hex: true),
            SecondaryItemsEnd: ExtractInt(inspectorScriptText, @"local\s+SECONDARY_ITEMS_END\s*=\s*0x(?<value>[0-9A-Fa-f]+)", SecondaryItemsEnd, hex: true),
            SecondaryHeaderMaxEvents: ExtractInt(inspectorScriptText, @"local\s+SECONDARY_HEADER_MAX_EVENTS\s*=\s*(?<value>\d+)", SecondaryHeaderMaxEvents),
            SecondaryHeaderEventSize: ExtractInt(inspectorScriptText, @"local\s+SECONDARY_HEADER_EVENT_SIZE\s*=\s*(?<value>\d+)", SecondaryHeaderEventSize),
            SecondaryScoreThreshold: ExtractInt(inspectorScriptText, @"local\s+SECONDARY_SCORE_THRESHOLD\s*=\s*(?<value>\d+)", SecondaryScoreThreshold),
            SeasonAnchorYears: ExtractIntList(inspectorScriptText, @"local\s+seasonAnchorYears\s*=\s*\{(?<values>[^}]*)\}", SeasonAnchorYears),
            SecondarySampleDays: ExtractIntList(inspectorScriptText, @"local\s+SECONDARY_SAMPLE_DAYS\s*=\s*\{(?<values>[^}]*)\}", SecondarySampleDays),
            SearchPriorities: ExtractSearchPriorities(inspectorScriptText),
            References: ExtractReferences(cheatTable),
            RecommendedCommands: BuildRecommendedCommands());
    }

    internal static string ResolveCompetitionMapPath(string? inspectorScriptPath)
    {
        var path = TryResolveCompetitionMapPath(inspectorScriptPath);
        if (path is not null)
        {
            return path;
        }

        return DefaultCompetitionMapPaths[0];
    }

    internal static string? TryResolveCompetitionMapPath(string? inspectorScriptPath)
    {
        if (string.IsNullOrWhiteSpace(inspectorScriptPath) || !File.Exists(inspectorScriptPath))
        {
            return null;
        }

        var script = File.ReadAllText(inspectorScriptPath);
        var repoDir = ExtractFirstMatch(script, @"local\s+REPO_DIR\s*=\s*""(?<path>[^""]+)""");
        if (!string.IsNullOrWhiteSpace(repoDir))
        {
            return Path.Combine(repoDir, "input", "map_competitions-from-edit.txt");
        }

        var directPath = ExtractFirstMatch(script, @"local\s+COMP_MAP_FILE\s*=\s*""(?<path>[^""]+)""");
        if (!string.IsNullOrWhiteSpace(directPath))
        {
            return directPath;
        }

        return null;
    }

    internal static IReadOnlyDictionary<int, string> LoadCompetitionMap(string? mapPath = null)
    {
        var map = new Dictionary<int, string>();

        var resolvedPath = ResolveFirstExistingPath(mapPath, DefaultCompetitionMapPaths);
        if (resolvedPath is not null)
        {
            MergeCsvCompetitionMap(map, resolvedPath);
        }

        foreach (var installedRoot in EnumerateInstalledGameRoots())
        {
            var gogoszMenuMapPath = Path.Combine(installedRoot, "GOGOSZ", "Menu Server", "map_competitions.txt");
            if (File.Exists(gogoszMenuMapPath))
            {
                MergeCsvCompetitionMap(map, gogoszMenuMapPath, stopAtCommentOnlyLines: true);
            }

            var bmpesMenuMapPath = Path.Combine(installedRoot, "BMPES", "Servidor de Menus", "map_competitions.txt");
            if (File.Exists(bmpesMenuMapPath))
            {
                MergeCsvCompetitionMap(map, bmpesMenuMapPath, stopAtCommentOnlyLines: true);
            }

            var tournamentsPath = Path.Combine(installedRoot, "doc", "tournaments.txt");
            if (File.Exists(tournamentsPath))
            {
                MergeTournamentCompetitionMap(map, tournamentsPath);
            }
        }

        return map;
    }

    private static void MergeCsvCompetitionMap(Dictionary<int, string> map, string path, bool stopAtCommentOnlyLines = false)
    {
        foreach (var rawLine in File.ReadLines(path))
        {
            var line = rawLine.Trim();
            if (line.Length == 0)
            {
                continue;
            }

            if (line.StartsWith("#", StringComparison.Ordinal) || line.StartsWith(";", StringComparison.Ordinal))
            {
                if (stopAtCommentOnlyLines)
                {
                    continue;
                }

                continue;
            }

            var match = Regex.Match(line, @"^\s*(?<code>-?\d+)\s*,\s*(?<label>[^#]+?)\s*(?:#.*)?$");
            if (!match.Success)
            {
                continue;
            }

            if (!int.TryParse(match.Groups["code"].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var code))
            {
                continue;
            }

            var label = match.Groups["label"].Value.Trim();
            if (label.Length == 0 || map.ContainsKey(code))
            {
                continue;
            }

            map[code] = label;
        }
    }

    private static void MergeTournamentCompetitionMap(Dictionary<int, string> map, string path)
    {
        foreach (var rawLine in File.ReadLines(path))
        {
            var line = rawLine.Trim();
            if (line.Length == 0)
            {
                continue;
            }

            var match = Regex.Match(line, @"^\s*(?<label>.+?)\s*-\s*(?<code>-?\d+)\s*$");
            if (!match.Success)
            {
                continue;
            }

            if (!int.TryParse(match.Groups["code"].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var code))
            {
                continue;
            }

            var label = match.Groups["label"].Value.Trim();
            if (label.Length == 0 || map.ContainsKey(code))
            {
                continue;
            }

            map[code] = label;
        }
    }

    private static IEnumerable<string> EnumerateInstalledGameRoots()
    {
        var programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
        if (string.IsNullOrWhiteSpace(programFilesX86))
        {
            yield break;
        }

        var commonRoot = Path.Combine(programFilesX86, "Steam", "steamapps", "common");
        if (!Directory.Exists(commonRoot))
        {
            yield break;
        }

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var root in Directory.EnumerateDirectories(commonRoot, "eFootball PES 2021*"))
        {
            if (seen.Add(root))
            {
                yield return root;
            }
        }
    }

    private static IReadOnlyList<Pes2021CalendarSearchPriority> ExtractSearchPriorities(string luaScript)
    {
        var matches = Regex.Matches(
            luaScript,
            @"\{\s*tier\s*=\s*""(?<tier>[^""]+)""\s*,\s*label\s*=\s*""(?<label>[^""]+)""\s*,\s*note\s*=\s*""(?<note>[^""]+)""\s*\}");

        if (matches.Count == 0)
        {
            return SearchPriorities;
        }

        var items = new List<Pes2021CalendarSearchPriority>(matches.Count);
        foreach (Match match in matches)
        {
            items.Add(new Pes2021CalendarSearchPriority(
                match.Groups["tier"].Value,
                match.Groups["label"].Value,
                match.Groups["note"].Value));
        }

        return items;
    }

    private static IReadOnlyList<Pes2021CalendarReference> ExtractReferences(XDocument cheatTable)
    {
        var rootEntries = cheatTable.Root?.Element("CheatEntries");
        if (rootEntries is null)
        {
            return [];
        }

        var references = new List<Pes2021CalendarReference>();
        foreach (var group in rootEntries.Elements("CheatEntry"))
        {
            var description = ReadText(group, "Description");
            CollectReferences(group, description ?? string.Empty, references);
        }

        return references;
    }

    private static void CollectReferences(XElement entry, string scope, List<Pes2021CalendarReference> references)
    {
        var description = ReadText(entry, "Description")?.Trim('"');
        var address = ReadText(entry, "Address");
        var variableType = ReadText(entry, "VariableType");
        var offsets = ReadOffsets(entry);

        if (description is not null && IsRelevantReference(description))
        {
            references.Add(new Pes2021CalendarReference(scope, description, address, variableType, offsets));
        }

        var childEntries = entry.Element("CheatEntries");
        if (childEntries is null)
        {
            return;
        }

        var nextScope = description is not null && description.Length > 0 ? description : scope;
        foreach (var child in childEntries.Elements("CheatEntry"))
        {
            CollectReferences(child, nextScope, references);
        }
    }

    private static bool IsRelevantGroup(string? description)
    {
        if (string.IsNullOrWhiteSpace(description))
        {
            return false;
        }

        var value = description.Trim('"').ToLowerInvariant();
        return value.Contains("calendar")
            || value.Contains("agenda")
            || value.Contains("schedule")
            || value.Contains("fixture")
            || value.Contains("ml calendar")
            || value.Contains("r3-checks");
    }

    private static bool IsRelevantReference(string description)
    {
        var value = description.ToLowerInvariant();
        return value.Contains("calendar")
            || value.Contains("agenda")
            || value.Contains("schedule")
            || value.Contains("fixture")
            || value.Contains("competition code")
            || value.Contains("round")
            || value.Contains("year")
            || value.Contains("month")
            || value.Contains("day")
            || value.Contains("home id")
            || value.Contains("away id")
            || value.Contains("score")
            || value.Contains("record index")
            || value.Contains("record addr")
            || value.Contains("next record")
            || value.Contains("sig-");
    }

    private static IReadOnlyList<int> ReadOffsets(XElement entry)
    {
        var offsets = new List<int>();
        var offsetsElement = entry.Element("Offsets");
        if (offsetsElement is null)
        {
            return offsets;
        }

        foreach (var offsetElement in offsetsElement.Elements("Offset"))
        {
            var text = offsetElement.Value.Trim();
            if (text.Length == 0)
            {
                continue;
            }

            if (int.TryParse(text, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var offset))
            {
                offsets.Add(offset);
            }
        }

        return offsets;
    }

    private static string? ReadText(XElement element, string childName)
        => element.Element(childName)?.Value;

    private static string NormalizeLuaPath(string path)
        => path.Replace(@"\\", @"\");

    private static string? ResolveFirstExistingPath(string? preferredPath, IReadOnlyList<string> fallbackPaths)
    {
        if (!string.IsNullOrWhiteSpace(preferredPath) && File.Exists(preferredPath))
        {
            return preferredPath;
        }

        foreach (var fallback in fallbackPaths)
        {
            if (File.Exists(fallback))
            {
                return fallback;
            }
        }

        return null;
    }

    private static string? ExtractFirstMatch(string input, string pattern)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return null;
        }

        var match = Regex.Match(input, pattern);
        if (!match.Success)
        {
            return null;
        }

        return NormalizeLuaPath(match.Groups["path"].Value);
    }

    private static int ExtractInt(string input, string pattern, int fallback, bool hex = false)
    {
        var match = Regex.Match(input, pattern);
        if (!match.Success)
        {
            return fallback;
        }

        var valueText = match.Groups["value"].Value;
        return int.TryParse(
            valueText,
            hex ? NumberStyles.HexNumber : NumberStyles.Integer,
            CultureInfo.InvariantCulture,
            out var value)
            ? value
            : fallback;
    }

    private static IReadOnlyList<int> ExtractIntList(string input, string pattern, IReadOnlyList<int> fallback)
    {
        var match = Regex.Match(input, pattern, RegexOptions.Singleline);
        if (!match.Success)
        {
            return fallback;
        }

        var valueText = match.Groups["values"].Value;
        var values = new List<int>();
        foreach (Match numberMatch in Regex.Matches(valueText, @"-?\d+"))
        {
            if (int.TryParse(numberMatch.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
            {
                values.Add(value);
            }
        }

        return values.Count > 0 ? values : fallback;
    }

    private static IReadOnlyList<string> BuildRecommendedCommands()
    {
        return
        [
            "pes2021_calendar_guide",
            "pes2021_find_calendar_base",
            "pes2021_dump_calendar_date",
            "pes2021_calendar_summary",
            "pes2021_calendar_search_priorities",
            "pes2021_inspect_secondary_calendar_candidate",
            "pes2021_scan_secondary_calendar_candidates",
        ];
    }
}
