using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace Overmem.Extensions.Pes2021.Fixtures;

/// <summary>
/// Result of loading one competition and one team catalog. The catalog exposes the loaded
/// entries together with the file hash, the rejection counts and the list of conflicts so
/// the extraction result can report exactly what was used.
/// </summary>
public sealed record Pes2021FixtureCatalog(
    string? CompetitionMapPath,
    string? CompetitionMapSha256,
    IReadOnlyList<CompetitionMapEntry> CompetitionEntries,
    IReadOnlyList<string> CompetitionWarnings,
    string? TeamMapPath,
    string? TeamMapSha256,
    IReadOnlyList<TeamMapEntry> TeamEntries,
    IReadOnlyList<CatalogConflict> TeamConflicts,
    IReadOnlyList<string> TeamWarnings);

/// <summary>
/// Loads PES 2021 competition/team catalogs from CSV files and builds the indices used by
/// <see cref="Pes2021FixtureNameResolver"/>. The loader is profile-independent: only the
/// caller decides whether the path comes from the profile, the host configuration or the
/// CLI arguments.
///
/// The team CSV accepts both the canonical column names (<c>team_id</c>, <c>team_liga</c>,
/// <c>name</c>) and the legacy aliases (<c>secondary_id</c>, <c>league_id</c>) for
/// compatibility with historical catalogs. When an alias is used the loader records a
/// warning so the caller can document that the value was mapped and not validated as a
/// semantic league id.
/// </summary>
public static class Pes2021FixtureCatalogLoader
{
    public static Pes2021FixtureCatalog Load(
        string? competitionMapPath,
        string? teamMapPath,
        string? competitionMapSearchDirectory = null,
        string? teamMapSearchDirectory = null)
    {
        var competitionEntries = new List<CompetitionMapEntry>();
        var competitionWarnings = new List<string>();
        string? competitionSha = null;
        if (!string.IsNullOrWhiteSpace(competitionMapPath))
        {
            var resolved = ResolveCatalogPath(competitionMapPath, competitionMapSearchDirectory);
            if (resolved is null)
            {
                competitionWarnings.Add($"competition_map_not_found:{competitionMapPath}");
            }
            else
            {
                competitionSha = LoadCompetitionEntries(resolved, competitionEntries, competitionWarnings);
            }
        }

        var teamEntries = new List<TeamMapEntry>();
        var teamConflicts = new List<CatalogConflict>();
        var teamWarnings = new List<string>();
        string? teamSha = null;
        if (!string.IsNullOrWhiteSpace(teamMapPath))
        {
            var resolved = ResolveCatalogPath(teamMapPath, teamMapSearchDirectory);
            if (resolved is null)
            {
                teamWarnings.Add($"team_map_not_found:{teamMapPath}");
            }
            else
            {
                teamSha = LoadTeamEntries(resolved, teamEntries, teamWarnings, teamConflicts);
            }
        }

        return new Pes2021FixtureCatalog(
            CompetitionMapPath: competitionMapPath,
            CompetitionMapSha256: competitionSha,
            CompetitionEntries: competitionEntries,
            CompetitionWarnings: competitionWarnings,
            TeamMapPath: teamMapPath,
            TeamMapSha256: teamSha,
            TeamEntries: teamEntries,
            TeamConflicts: teamConflicts,
            TeamWarnings: teamWarnings);
    }

    private static string? ResolveCatalogPath(string path, string? searchDirectory)
    {
        if (Path.IsPathRooted(path) && File.Exists(path))
        {
            return Path.GetFullPath(path);
        }

        if (File.Exists(path))
        {
            return Path.GetFullPath(path);
        }

        if (!string.IsNullOrWhiteSpace(searchDirectory))
        {
            var candidate = Path.Combine(searchDirectory, path);
            if (File.Exists(candidate))
            {
                return Path.GetFullPath(candidate);
            }
        }

        return null;
    }

    private static string LoadCompetitionEntries(
        string path,
        List<CompetitionMapEntry> entries,
        List<string> warnings)
    {
        var bytes = File.ReadAllBytes(path);
        var sha = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        var lines = ParseCsvLines(bytes);
        if (lines.Count == 0)
        {
            warnings.Add($"competition_map_empty:{path}");
            return sha;
        }

        var firstRow = lines[0];
        var hasHeader = firstRow.Count >= 2
            && firstRow[0].Trim().Equals("competition_id", System.StringComparison.OrdinalIgnoreCase)
            && !IsInteger(firstRow[1]);
        var startIndex = hasHeader ? 1 : 0;

        for (var index = startIndex; index < lines.Count; index++)
        {
            var row = lines[index];
            if (row.Count < 2)
            {
                warnings.Add($"competition_map_invalid_line:{index + 1}");
                continue;
            }

            if (!ushort.TryParse(row[0].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var code))
            {
                warnings.Add($"competition_map_invalid_id:{row[0]}");
                continue;
            }

            var name = row[1].Trim();
            if (name.Length == 0)
            {
                warnings.Add($"competition_map_empty_name:{code}");
                continue;
            }

            entries.Add(new CompetitionMapEntry(new CompetitionId(code), name, path, sha));
        }

        return sha;
    }

    private static string LoadTeamEntries(
        string path,
        List<TeamMapEntry> entries,
        List<string> warnings,
        List<CatalogConflict> conflicts)
    {
        var bytes = File.ReadAllBytes(path);
        var sha = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        var lines = ParseCsvLines(bytes);
        if (lines.Count == 0)
        {
            warnings.Add($"team_map_empty:{path}");
            return sha;
        }

        var firstRow = lines[0];
        var hasHeader = firstRow.Count >= 3
            && firstRow[0].Trim().Equals("team_id", System.StringComparison.OrdinalIgnoreCase)
            && !IsInteger(firstRow[1]);
        var columnMap = hasHeader ? MapColumns(firstRow) : new Dictionary<string, int>(System.StringComparer.OrdinalIgnoreCase);
        if (!hasHeader)
        {
            columnMap["team_id"] = 0;
            columnMap["team_liga"] = 1;
            columnMap["name"] = 2;
        }

        var ligaColumn = columnMap.TryGetValue("team_liga", out var ligaIndex)
            ? ligaIndex
            : columnMap.TryGetValue("secondary_id", out var ligaSecondary)
                ? ligaSecondary
                : columnMap.TryGetValue("league_id", out var ligaLeague)
                    ? ligaLeague
                    : -1;
        var ligaSource = columnMap.TryGetValue("team_liga", out _)
            ? "team_liga"
            : columnMap.TryGetValue("secondary_id", out _)
                ? "secondary_id"
                : columnMap.TryGetValue("league_id", out _)
                    ? "league_id"
                    : string.Empty;
        if (ligaColumn < 0)
        {
            warnings.Add($"team_map_missing_liga_column:{path}");
            return sha;
        }

        if (ligaSource is "secondary_id" or "league_id")
        {
            warnings.Add($"team_map_liga_alias_used:{ligaSource}");
        }

        var byKey = new Dictionary<TeamKey, List<TeamMapEntry>>();

        var startIndex = hasHeader ? 1 : 0;
        for (var index = startIndex; index < lines.Count; index++)
        {
            var row = lines[index];
            if (row.Count <= ligaColumn)
            {
                warnings.Add($"team_map_invalid_line:{index + 1}");
                continue;
            }

            if (!ushort.TryParse(row[columnMap["team_id"]].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var teamId))
            {
                warnings.Add($"team_map_invalid_team_id:{row[columnMap["team_id"]]}");
                continue;
            }

            if (!ushort.TryParse(row[ligaColumn].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var teamLiga))
            {
                warnings.Add($"team_map_invalid_team_liga:{teamId}");
                continue;
            }

            var nameIndex = columnMap.TryGetValue("name", out var nameColumn) ? nameColumn : 2;
            if (row.Count <= nameIndex)
            {
                warnings.Add($"team_map_missing_name:{teamId}");
                continue;
            }

            var name = row[nameIndex].Trim();
            if (name.Length == 0)
            {
                warnings.Add($"team_map_empty_name:{teamId}");
                continue;
            }

            var shortName = columnMap.TryGetValue("short_name", out var shortColumn) && row.Count > shortColumn
                ? row[shortColumn].Trim()
                : null;
            var source = columnMap.TryGetValue("source", out var sourceColumn) && row.Count > sourceColumn
                ? row[sourceColumn].Trim()
                : null;
            var evidenceStatus = columnMap.TryGetValue("evidence_status", out var statusColumn) && row.Count > statusColumn
                ? row[statusColumn].Trim()
                : null;

            var entry = new TeamMapEntry(
                Key: new TeamKey(teamId, teamLiga),
                Name: name,
                ShortName: string.IsNullOrEmpty(shortName) ? null : shortName,
                Source: string.IsNullOrEmpty(source) ? null : source,
                EvidenceStatus: string.IsNullOrEmpty(evidenceStatus) ? null : evidenceStatus,
                SourcePath: path,
                SourceSha256: sha);

            entries.Add(entry);
            if (!byKey.TryGetValue(entry.Key, out var list))
            {
                list = new List<TeamMapEntry>();
                byKey[entry.Key] = list;
            }

            list.Add(entry);
        }

        foreach (var (key, list) in byKey)
        {
            var distinctNames = list.Select(static e => e.Name).Distinct(StringComparer.Ordinal).ToArray();
            if (distinctNames.Length > 1)
            {
                conflicts.Add(new CatalogConflict(
                    Key: key,
                    ConflictingNames: distinctNames,
                    SourcePaths: list.Select(static e => e.SourcePath).Distinct(StringComparer.OrdinalIgnoreCase).ToArray()));
            }
        }

        return sha;
    }

    private static IReadOnlyList<IReadOnlyList<string>> ParseCsvLines(byte[] bytes)
    {
        var text = Encoding.UTF8.GetString(bytes);
        var rows = new List<IReadOnlyList<string>>();
        foreach (var rawLine in text.Split('\n'))
        {
            var line = rawLine.TrimEnd('\r').Trim();
            if (line.Length == 0)
            {
                continue;
            }

            rows.Add(line.Split(','));
        }

        return rows;
    }

    private static Dictionary<string, int> MapColumns(IReadOnlyList<string> header)
    {
        var map = new Dictionary<string, int>(System.StringComparer.OrdinalIgnoreCase);
        for (var index = 0; index < header.Count; index++)
        {
            map[header[index].Trim()] = index;
        }

        return map;
    }

    private static bool IsInteger(string value)
        => int.TryParse(value.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out _);
}
