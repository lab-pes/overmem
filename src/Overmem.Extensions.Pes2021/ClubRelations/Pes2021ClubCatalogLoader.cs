using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace Overmem.Extensions.Pes2021.ClubRelations;

public sealed class Pes2021ClubCatalogLoadResult
{
    public Pes2021ClubCatalogLoadResult(
        IReadOnlyList<Pes2021ClubCatalogRow> rows,
        IReadOnlyList<string> warnings,
        string sourcePath,
        string sourceSha256)
    {
        Rows = rows;
        Warnings = warnings;
        SourcePath = sourcePath;
        SourceSha256 = sourceSha256;
    }

    public IReadOnlyList<Pes2021ClubCatalogRow> Rows { get; }
    public IReadOnlyList<string> Warnings { get; }
    public string SourcePath { get; }
    public string SourceSha256 { get; }
}

public static class Pes2021ClubCatalogLoader
{
    private const int MaxNameBytes = 63;

    public static Pes2021ClubCatalogLoadResult LoadFromFile(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("Catalog path is required.", nameof(path));
        }

        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Catalog CSV not found at '{path}'.", path);
        }

        var sha256 = ComputeSha256(path);
        var rows = new List<Pes2021ClubCatalogRow>();
        var warnings = new List<string>();

        using var reader = new StreamReader(path, Encoding.UTF8);
        var headerLine = reader.ReadLine();
        if (headerLine is null)
        {
            return new Pes2021ClubCatalogLoadResult(rows, warnings, path, sha256);
        }

        var header = SplitCsvLine(headerLine);
        var teamIdIndex = IndexOf(header, "team_id");
        var secondaryIndex = IndexOf(header, "secondary_id");
        var nameIndex = IndexOf(header, "name");
        var shortNameIndex = IndexOf(header, "short_name");
        var cityIndex = IndexOf(header, "city_or_stadium");
        var addressIndex = IndexOf(header, "address");
        var regionBaseIndex = IndexOf(header, "region_base");
        var regionOffsetIndex = IndexOf(header, "region_offset");

        if (teamIdIndex < 0 || secondaryIndex < 0 || nameIndex < 0)
        {
            throw new InvalidDataException(
                "Catalog CSV header must contain 'team_id', 'secondary_id' and 'name' columns.");
        }

        var lineNumber = 1;
        while (!reader.EndOfStream)
        {
            lineNumber++;
            var rawLine = reader.ReadLine();
            if (rawLine is null)
            {
                break;
            }

            if (string.IsNullOrWhiteSpace(rawLine))
            {
                continue;
            }

            var cells = SplitCsvLine(rawLine);
            if (cells.Count <= teamIdIndex || cells.Count <= secondaryIndex || cells.Count <= nameIndex)
            {
                warnings.Add($"Line {lineNumber}: column count smaller than header. Skipped.");
                continue;
            }

            if (!TryParseInt32(cells[teamIdIndex], out var teamId))
            {
                warnings.Add($"Line {lineNumber}: team_id '{cells[teamIdIndex]}' is not an integer. Skipped.");
                continue;
            }

            if (!TryParseInt32(cells[secondaryIndex], out var secondaryId))
            {
                warnings.Add($"Line {lineNumber}: secondary_id '{cells[secondaryIndex]}' is not an integer. Skipped.");
                continue;
            }

            var name = cells[nameIndex];
            if (name.Length > MaxNameBytes)
            {
                name = name.Substring(0, MaxNameBytes);
            }

            var shortName = shortNameIndex >= 0 && shortNameIndex < cells.Count ? cells[shortNameIndex] : string.Empty;
            var city = cityIndex >= 0 && cityIndex < cells.Count ? cells[cityIndex] : string.Empty;
            var address = addressIndex >= 0 && addressIndex < cells.Count && TryParseUInt64(cells[addressIndex], out var addrValue)
                ? addrValue
                : 0UL;
            var regionBase = regionBaseIndex >= 0 && regionBaseIndex < cells.Count && TryParseUInt64(cells[regionBaseIndex], out var regionBaseValue)
                ? regionBaseValue
                : 0UL;
            var regionOffset = regionOffsetIndex >= 0 && regionOffsetIndex < cells.Count && TryParseUInt64(cells[regionOffsetIndex], out var regionOffsetValue)
                ? regionOffsetValue
                : 0UL;

            rows.Add(new Pes2021ClubCatalogRow(
                teamId,
                secondaryId,
                name,
                shortName,
                string.IsNullOrEmpty(city) ? null : city,
                address,
                regionBase,
                regionOffset,
                path,
                sha256));
        }

        return new Pes2021ClubCatalogLoadResult(rows, warnings, path, sha256);
    }

    private static string ComputeSha256(string path)
    {
        using var stream = File.OpenRead(path);
        using var sha = SHA256.Create();
        var hash = sha.ComputeHash(stream);
        var builder = new StringBuilder(hash.Length * 2);
        foreach (var b in hash)
        {
            builder.Append(b.ToString("x2", CultureInfo.InvariantCulture));
        }

        return builder.ToString();
    }

    private static int IndexOf(IReadOnlyList<string> header, string name)
    {
        for (var i = 0; i < header.Count; i++)
        {
            if (string.Equals(header[i], name, StringComparison.OrdinalIgnoreCase))
            {
                return i;
            }
        }

        return -1;
    }

    private static bool TryParseInt32(string text, out int value)
        => int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);

    private static bool TryParseUInt64(string text, out ulong value)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            value = 0UL;
            return false;
        }

        var span = text.AsSpan().Trim();
        if (span.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            return ulong.TryParse(span[2..], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out value);
        }

        return ulong.TryParse(span, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
    }

    private static List<string> SplitCsvLine(string line)
    {
        var cells = new List<string>();
        var current = new StringBuilder();
        var insideQuotes = false;
        for (var i = 0; i < line.Length; i++)
        {
            var ch = line[i];
            if (ch == '"')
            {
                if (insideQuotes && i + 1 < line.Length && line[i + 1] == '"')
                {
                    current.Append('"');
                    i++;
                }
                else
                {
                    insideQuotes = !insideQuotes;
                }
                continue;
            }

            if (ch == ',' && !insideQuotes)
            {
                cells.Add(current.ToString());
                current.Clear();
                continue;
            }

            current.Append(ch);
        }

        cells.Add(current.ToString());
        return cells;
    }
}
