using Overmem.Abstractions.Memory;

namespace Overmem.Windows.Memory;

public static class PatternScanner
{
    public static PatternDefinition Parse(string pattern)
    {
        if (string.IsNullOrWhiteSpace(pattern))
        {
            throw new ArgumentException("A pattern is required.", nameof(pattern));
        }

        var tokens = pattern.Split([' ', '\t', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
        if (tokens.Length == 0)
        {
            throw new ArgumentException("A pattern is required.", nameof(pattern));
        }

        var bytes = new byte[tokens.Length];
        var mask = new bool[tokens.Length];

        for (var index = 0; index < tokens.Length; index++)
        {
            var token = tokens[index];
            if (token is "?" or "??")
            {
                mask[index] = false;
                continue;
            }

            if (token.Length != 2 || !byte.TryParse(token, System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture, out var value))
            {
                throw new FormatException($"Invalid pattern token '{token}'. Use hex byte pairs or ?? wildcards.");
            }

            bytes[index] = value;
            mask[index] = true;
        }

        return new PatternDefinition(bytes, mask);
    }

    public static IReadOnlyList<ulong> FindMatches(byte[] buffer, ulong baseAddress, PatternDefinition pattern, int maxResults)
    {
        if (pattern.Bytes.Length == 0 || buffer.Length < pattern.Bytes.Length)
        {
            return [];
        }

        var matches = new List<ulong>();
        var lastStart = buffer.Length - pattern.Bytes.Length;

        for (var start = 0; start <= lastStart; start++)
        {
            var matched = true;
            for (var offset = 0; offset < pattern.Bytes.Length; offset++)
            {
                if (!pattern.Mask[offset])
                {
                    continue;
                }

                if (buffer[start + offset] != pattern.Bytes[offset])
                {
                    matched = false;
                    break;
                }
            }

            if (!matched)
            {
                continue;
            }

            matches.Add(baseAddress + (ulong)start);
            if (matches.Count >= maxResults)
            {
                break;
            }
        }

        return matches;
    }
}

public sealed record PatternDefinition(byte[] Bytes, bool[] Mask);