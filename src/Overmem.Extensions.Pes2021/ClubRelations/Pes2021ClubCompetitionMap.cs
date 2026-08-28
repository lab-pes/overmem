using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;

namespace Overmem.Extensions.Pes2021.ClubRelations;

public static class Pes2021ClubCompetitionMap
{
    public static IReadOnlyDictionary<int, string> LoadFromFile(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("Competition map path is required.", nameof(path));
        }

        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Competition map not found at '{path}'.", path);
        }

        var map = new Dictionary<int, string>();
        var lineNumber = 0;
        foreach (var rawLine in File.ReadLines(path))
        {
            lineNumber++;
            var line = rawLine.Trim();
            if (line.Length == 0 || line.StartsWith("#", StringComparison.Ordinal))
            {
                continue;
            }

            var match = Regex.Match(line, @"^(?<code>-?\d+)\s*=\s*(?<label>.+?)\s*$");
            if (!match.Success)
            {
                continue;
            }

            if (!int.TryParse(match.Groups["code"].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var code))
            {
                continue;
            }

            var label = match.Groups["label"].Value.Trim();
            if (label.Length == 0)
            {
                continue;
            }

            map[code] = label;
        }

        return map;
    }
}
