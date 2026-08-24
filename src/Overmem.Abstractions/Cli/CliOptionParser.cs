using System;
using System.Collections.Generic;
using System.Linq;
using Overmem.Abstractions.Processes;

namespace Overmem.Abstractions.Cli;

public static class CliOptionParser
{
    public static Dictionary<string, string?> ParseOptions(string[] args)
    {
        var options = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        for (var index = 0; index < args.Length; index++)
        {
            var token = args[index];
            if (!token.StartsWith("--", StringComparison.Ordinal))
            {
                throw new ArgumentException($"Unexpected token '{token}'. Options must start with '--'.");
            }

            var optionName = token[2..];
            if (string.IsNullOrWhiteSpace(optionName))
            {
                throw new ArgumentException("Option names cannot be empty.");
            }

            if (index == args.Length - 1 || args[index + 1].StartsWith("--", StringComparison.Ordinal))
            {
                options[optionName] = null;
                continue;
            }

            options[optionName] = args[++index];
        }

        return options;
    }

    public static ProcessSelector ParseSelector(IReadOnlyDictionary<string, string?> options)
    {
        var pidText = GetOptionalOption(options, "pid");
        var processName = GetOptionalOption(options, "name");
        int? pid = string.IsNullOrWhiteSpace(pidText) ? null : ParseInt32(pidText);

        var selector = new ProcessSelector(pid, processName);
        if (!selector.IsValid())
        {
            throw new ArgumentException("Specify either --pid or --name.");
        }

        return selector;
    }

    public static string GetRequiredOption(IReadOnlyDictionary<string, string?> options, string name)
        => GetOptionalOption(options, name) ?? throw new ArgumentException($"Missing required option '--{name}'.");

    public static string? GetOptionalOption(IReadOnlyDictionary<string, string?> options, string name, string? defaultValue = null)
        => options.TryGetValue(name, out var value) && value is not null ? value : defaultValue;

    public static int ParseInt32(string value)
        => value.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
            ? Convert.ToInt32(value[2..], 16)
            : int.Parse(value, System.Globalization.CultureInfo.InvariantCulture);

    public static int? ParseOptionalInt32(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : ParseInt32(value);

    public static long ParseInt64(string value)
        => value.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
            ? Convert.ToInt64(value[2..], 16)
            : long.Parse(value, System.Globalization.CultureInfo.InvariantCulture);

    public static ulong ParseUnsignedLong(string value)
        => value.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
            ? Convert.ToUInt64(value[2..], 16)
            : ulong.Parse(value, System.Globalization.CultureInfo.InvariantCulture);

    public static ulong? ParseOptionalUnsignedLong(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : ParseUnsignedLong(value);

    public static long[] ParseOffsets(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return [];
        }

        return value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Select(ParseInt64)
            .ToArray();
    }

    public static int[] ParseInt32List(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return [];
        }

        return value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Select(ParseInt32)
            .ToArray();
    }
}
