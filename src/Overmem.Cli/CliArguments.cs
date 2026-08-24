using Overmem.Abstractions.Memory;
using Overmem.Abstractions.Processes;
using Overmem.Abstractions.Cli;
using System.Collections.Generic;
using System;

namespace Overmem.Cli;

public sealed record HelpCliCommand : CliCommand;
public sealed record ListModulesCliCommand(ProcessSelector Selector) : CliCommand;
public sealed record ListRegionsCliCommand(ProcessSelector Selector) : CliCommand;
public sealed record ReadCliCommand(ProcessSelector Selector, ulong Address, MemoryValueKind ValueKind, int Size) : CliCommand;
public sealed record WriteCliCommand(ProcessSelector Selector, ulong Address, MemoryValueKind ValueKind, string Value, int Size) : CliCommand;
public sealed record ScanPatternCliCommand(ProcessSelector Selector, string Pattern, string? ModuleName, int MaxResults) : CliCommand;
public sealed record ResolvePointerCliCommand(ProcessSelector Selector, ulong BaseAddress, long[] Offsets) : CliCommand;
public sealed record ResolveModulePointerCliCommand(ProcessSelector Selector, string ModuleName, long BaseOffset, long[] Offsets) : CliCommand;
public sealed record LoadTableCliCommand(string FilePath) : CliCommand;
public sealed record SaveTableCliCommand(string SourceFilePath, string DestinationFilePath) : CliCommand;
public sealed record RefreshTableCliCommand(ProcessSelector Selector, string FilePath) : CliCommand;
public sealed record ScanValueCliCommand(ProcessSelector Selector, MemoryValueKind ValueKind, string Value, int Size, int Alignment, int MaxResults) : CliCommand;
public sealed record DiscoverPointersCliCommand(ProcessSelector Selector, ulong TargetAddress, int MaxDepth, long MaxOffset, int Alignment, int MaxResults, string? BaseModuleName, bool RevalidateCandidates) : CliCommand;

public static class CliArgumentParser
{
    public static CliCommand Parse(string[] args, IReadOnlyList<ICliCommandExtension>? extensions = null)
    {
        if (args.Length == 0 || args[0] is "help" or "--help" or "-h")
        {
            return new HelpCliCommand();
        }

        var commandName = args[0];
        var options = CliOptionParser.ParseOptions(args[1..]);

        return commandName switch
        {
            "modules" => new ListModulesCliCommand(CliOptionParser.ParseSelector(options)),
            "regions" => new ListRegionsCliCommand(CliOptionParser.ParseSelector(options)),
            "read" => new ReadCliCommand(
                CliOptionParser.ParseSelector(options),
                CliOptionParser.ParseUnsignedLong(CliOptionParser.GetRequiredOption(options, "address")),
                Enum.Parse<MemoryValueKind>(CliOptionParser.GetRequiredOption(options, "value-kind"), ignoreCase: true),
                CliOptionParser.ParseInt32(CliOptionParser.GetOptionalOption(options, "size") ?? "0")),
            "write" => new WriteCliCommand(
                CliOptionParser.ParseSelector(options),
                CliOptionParser.ParseUnsignedLong(CliOptionParser.GetRequiredOption(options, "address")),
                Enum.Parse<MemoryValueKind>(CliOptionParser.GetRequiredOption(options, "value-kind"), ignoreCase: true),
                CliOptionParser.GetRequiredOption(options, "value"),
                CliOptionParser.ParseInt32(CliOptionParser.GetOptionalOption(options, "size") ?? "0")),
            "scan-pattern" => new ScanPatternCliCommand(
                CliOptionParser.ParseSelector(options),
                CliOptionParser.GetRequiredOption(options, "pattern"),
                CliOptionParser.GetOptionalOption(options, "module-name"),
                CliOptionParser.ParseInt32(CliOptionParser.GetOptionalOption(options, "max-results") ?? "100")),
            "resolve-pointer" => new ResolvePointerCliCommand(
                CliOptionParser.ParseSelector(options),
                CliOptionParser.ParseUnsignedLong(CliOptionParser.GetRequiredOption(options, "base-address")),
                CliOptionParser.ParseOffsets(CliOptionParser.GetOptionalOption(options, "offsets"))),
            "resolve-module-pointer" => new ResolveModulePointerCliCommand(
                CliOptionParser.ParseSelector(options),
                CliOptionParser.GetRequiredOption(options, "module-name"),
                CliOptionParser.ParseInt64(CliOptionParser.GetOptionalOption(options, "base-offset") ?? "0"),
                CliOptionParser.ParseOffsets(CliOptionParser.GetOptionalOption(options, "offsets"))),
            "table-load" => new LoadTableCliCommand(CliOptionParser.GetRequiredOption(options, "file")),
            "table-save" => new SaveTableCliCommand(
                CliOptionParser.GetRequiredOption(options, "source-file"),
                CliOptionParser.GetRequiredOption(options, "destination-file")),
            "table-refresh" => new RefreshTableCliCommand(CliOptionParser.ParseSelector(options), CliOptionParser.GetRequiredOption(options, "file")),
            "scan-value" => new ScanValueCliCommand(
                CliOptionParser.ParseSelector(options),
                Enum.Parse<MemoryValueKind>(CliOptionParser.GetRequiredOption(options, "value-kind"), ignoreCase: true),
                CliOptionParser.GetRequiredOption(options, "value"),
                CliOptionParser.ParseInt32(CliOptionParser.GetOptionalOption(options, "size") ?? "0"),
                CliOptionParser.ParseInt32(CliOptionParser.GetOptionalOption(options, "alignment") ?? "1"),
                CliOptionParser.ParseInt32(CliOptionParser.GetOptionalOption(options, "max-results") ?? "1000")),
            "discover-pointers" => new DiscoverPointersCliCommand(
                CliOptionParser.ParseSelector(options),
                CliOptionParser.ParseUnsignedLong(CliOptionParser.GetRequiredOption(options, "target-address")),
                CliOptionParser.ParseInt32(CliOptionParser.GetOptionalOption(options, "max-depth") ?? "2"),
                CliOptionParser.ParseInt64(CliOptionParser.GetOptionalOption(options, "max-offset") ?? "0"),
                CliOptionParser.ParseInt32(CliOptionParser.GetOptionalOption(options, "alignment") ?? "0"),
                CliOptionParser.ParseInt32(CliOptionParser.GetOptionalOption(options, "max-results") ?? "100"),
                CliOptionParser.GetOptionalOption(options, "base-module-name"),
                !options.ContainsKey("skip-revalidation")),
            _ => TryParseExtension(commandName, options, extensions)
                 ?? throw new ArgumentException($"Unknown command '{commandName}'.")
        };
    }

    private static CliCommand? TryParseExtension(string commandName, IReadOnlyDictionary<string, string?> options, IReadOnlyList<ICliCommandExtension>? extensions)
    {
        if (extensions is null) return null;

        foreach (var extension in extensions)
        {
            var result = extension.TryParse(commandName, options);
            if (result is not null) return result;
        }

        return null;
    }
}
