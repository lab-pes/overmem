using Microsoft.Extensions.DependencyInjection;
using Overmem.Abstractions.Memory;
using Overmem.Abstractions.Processes;
using Overmem.Abstractions.Search;
using Overmem.Application;
using Overmem.Application.Pointers;
using Overmem.Application.Tables;
using Overmem.Search;
using System.Text.Json;
using System.Text.Json.Serialization;
using Overmem.Abstractions.Cli;

namespace Overmem.Cli;

public static class CliApplication
{
    internal static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public static async Task<int> RunAsync(
        string[] args,
        IServiceProvider services,
        TextWriter stdout,
        TextWriter stderr,
        IReadOnlyList<ICliCommandExtension>? extensions = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var command = CliArgumentParser.Parse(args, extensions);
            if (command is HelpCliCommand)
            {
                await stdout.WriteLineAsync(GetHelpText(extensions));
                return 0;
            }

            var memoryTableService = services.GetRequiredService<MemoryTableService>();
            return await (command switch
            {
                ListModulesCliCommand modules => ExecuteWithAttachmentAsync(modules.Selector, services.GetRequiredService<ProcessMemoryApplicationService>(), attachment => services.GetRequiredService<ProcessMemoryApplicationService>().ListModulesAsync(attachment.AttachmentId, cancellationToken), stdout, cancellationToken),
                ListRegionsCliCommand regions => ExecuteWithAttachmentAsync(regions.Selector, services.GetRequiredService<ProcessMemoryApplicationService>(), attachment => services.GetRequiredService<ProcessMemoryApplicationService>().ListRegionsAsync(attachment.AttachmentId, cancellationToken), stdout, cancellationToken),
                ReadCliCommand read => ExecuteWithAttachmentAsync(read.Selector, services.GetRequiredService<ProcessMemoryApplicationService>(), attachment => services.GetRequiredService<ProcessMemoryApplicationService>().ReadAsync(new ReadMemoryRequest(attachment.AttachmentId, read.Address, read.ValueKind, read.Size), cancellationToken), stdout, cancellationToken),
                WriteCliCommand write => ExecuteWithAttachmentAsync(write.Selector, services.GetRequiredService<ProcessMemoryApplicationService>(), attachment => services.GetRequiredService<ProcessMemoryApplicationService>().WriteAsync(new WriteMemoryRequest(attachment.AttachmentId, write.Address, write.ValueKind, write.Value, write.Size), cancellationToken), stdout, cancellationToken),
                ScanPatternCliCommand scan => ExecuteWithAttachmentAsync(scan.Selector, services.GetRequiredService<ProcessMemoryApplicationService>(), attachment => services.GetRequiredService<ProcessMemoryApplicationService>().ScanPatternAsync(new PatternScanRequest(attachment.AttachmentId, scan.Pattern, scan.ModuleName, scan.MaxResults), cancellationToken), stdout, cancellationToken),
                ResolvePointerCliCommand resolve => ExecuteWithAttachmentAsync(resolve.Selector, services.GetRequiredService<ProcessMemoryApplicationService>(), attachment => services.GetRequiredService<ProcessMemoryApplicationService>().ResolvePointerAsync(new ResolvePointerRequest(attachment.AttachmentId, resolve.BaseAddress, resolve.Offsets), cancellationToken), stdout, cancellationToken),
                ResolveModulePointerCliCommand resolveModule => ExecuteWithAttachmentAsync(resolveModule.Selector, services.GetRequiredService<ProcessMemoryApplicationService>(), attachment => services.GetRequiredService<ProcessMemoryApplicationService>().ResolveModulePointerAsync(new ResolveModulePointerRequest(attachment.AttachmentId, resolveModule.ModuleName, resolveModule.BaseOffset, resolveModule.Offsets), cancellationToken), stdout, cancellationToken),
                LoadTableCliCommand loadTable => ExecuteAsync(async () => await memoryTableService.LoadAsync(loadTable.FilePath, cancellationToken), stdout),
                SaveTableCliCommand saveTable => ExecuteAsync(async () =>
                {
                    var document = await memoryTableService.LoadAsync(saveTable.SourceFilePath, cancellationToken);
                    await memoryTableService.SaveAsync(saveTable.DestinationFilePath, document, cancellationToken);
                    return new
                    {
                        SourceFilePath = saveTable.SourceFilePath,
                        DestinationFilePath = saveTable.DestinationFilePath,
                        document.Name,
                        EntryCount = document.Entries.Count
                    };
                }, stdout),
                RefreshTableCliCommand refreshTable => ExecuteWithAttachmentAsync(refreshTable.Selector, services.GetRequiredService<ProcessMemoryApplicationService>(), async attachment =>
                {
                    var document = await memoryTableService.LoadAsync(refreshTable.FilePath, cancellationToken);
                    return await memoryTableService.RefreshAsync(attachment.AttachmentId, document, cancellationToken);
                }, stdout, cancellationToken),
                ScanValueCliCommand scanValue => ExecuteWithAttachmentAsync(scanValue.Selector, services.GetRequiredService<ProcessMemoryApplicationService>(), async attachment =>
                {
                    var valueSearchService = services.GetRequiredService<IValueSearchService>();
                    var result = await valueSearchService.StartExactSearchAsync(new StartValueSearchRequest(
                        attachment.AttachmentId,
                        scanValue.ValueKind,
                        scanValue.Value,
                        scanValue.Size,
                        scanValue.Alignment,
                        scanValue.MaxResults), cancellationToken);

                    await valueSearchService.CloseSessionAsync(result.SessionId, cancellationToken);
                    return result;
                }, stdout, cancellationToken),
                DiscoverPointersCliCommand discoverPointers => ExecuteWithAttachmentAsync(discoverPointers.Selector, services.GetRequiredService<ProcessMemoryApplicationService>(), async attachment =>
                {
                    var pointerDiscoveryService = services.GetRequiredService<IPointerDiscoveryService>();
                    return await pointerDiscoveryService.DiscoverAsync(new DiscoverPointersRequest(
                        attachment.AttachmentId,
                        discoverPointers.TargetAddress,
                        discoverPointers.MaxDepth,
                        discoverPointers.MaxOffset,
                        discoverPointers.Alignment,
                        discoverPointers.MaxResults,
                        discoverPointers.BaseModuleName,
                        discoverPointers.RevalidateCandidates), cancellationToken);
                }, stdout, cancellationToken),
                _ => TryExecuteExtension(command, extensions, services, stdout, cancellationToken)
                     ?? throw new ArgumentOutOfRangeException(nameof(command), $"Unsupported command type '{command.GetType().Name}'.")
            });
        }
        catch (Exception exception)
        {
            await stderr.WriteLineAsync(exception.Message);
            return 1;
        }
    }

    private static Task<int>? TryExecuteExtension(
        CliCommand command,
        IReadOnlyList<ICliCommandExtension>? extensions,
        IServiceProvider services,
        TextWriter stdout,
        CancellationToken cancellationToken)
    {
        if (extensions is null) return null;

        foreach (var extension in extensions)
        {
            var result = extension.TryExecute(command, services, stdout, cancellationToken);
            if (result is not null) return result;
        }

        return null;
    }

    public static async Task<int> ExecuteWithAttachmentAsync<T>(
        ProcessSelector selector,
        ProcessMemoryApplicationService applicationService,
        Func<AttachmentInfo, Task<T>> action,
        TextWriter stdout,
        CancellationToken cancellationToken)
    {
        var attachment = await applicationService.AttachAsync(selector, cancellationToken);
        try
        {
            var result = await action(attachment);
            await stdout.WriteLineAsync(JsonSerializer.Serialize(result, JsonOptions));
            return 0;
        }
        finally
        {
            await applicationService.DetachAsync(attachment.AttachmentId, cancellationToken);
        }
    }

    internal static async Task<int> ExecuteAsync<T>(Func<Task<T>> action, TextWriter stdout)
    {
        var result = await action();
        await stdout.WriteLineAsync(JsonSerializer.Serialize(result, JsonOptions));
        return 0;
    }

    private static string GetHelpText(IReadOnlyList<ICliCommandExtension>? extensions = null)
    {
        var lines = new List<string>
        {
            "Overmem CLI",
            "",
            "Commands:",
            "  modules --pid <id>|--name <process>",
            "  regions --pid <id>|--name <process>",
            "  read --pid <id>|--name <process> --address <value> --value-kind <kind> [--size <bytes>]",
            "  write --pid <id>|--name <process> --address <value> --value-kind <kind> --value <value> [--size <bytes>]",
            "  scan-pattern --pid <id>|--name <process> --pattern <pattern> [--module-name <module>] [--max-results <count>]",
            "  resolve-pointer --pid <id>|--name <process> --base-address <value> [--offsets <o1,o2,...>]",
            "  resolve-module-pointer --pid <id>|--name <process> --module-name <module> [--base-offset <value>] [--offsets <o1,o2,...>]",
            "  table-load --file <path>",
            "  table-save --source-file <path> --destination-file <path>",
            "  table-refresh --pid <id>|--name <process> --file <path>",
            "  scan-value --pid <id>|--name <process> --value-kind <kind> --value <value> [--size <bytes>] [--alignment <step>] [--max-results <count>]",
            "  discover-pointers --pid <id>|--name <process> --target-address <value> [--max-depth <levels>] [--max-offset <value>] [--alignment <step>] [--max-results <count>] [--base-module-name <module>] [--skip-revalidation]"
        };

        if (extensions is not null)
        {
            foreach (var extension in extensions)
            {
                var extensionLines = extension.GetHelpLines();
                if (extensionLines.Count > 0)
                {
                    lines.Add("");
                    lines.AddRange(extensionLines);
                }
            }
        }

        lines.Add("");
        lines.Add("Numeric options accept decimal or hexadecimal values prefixed with 0x.");
        lines.Add($"Supported value kinds: {string.Join(", ", Enum.GetNames<MemoryValueKind>())}");

        return string.Join(Environment.NewLine, lines);
    }
}
