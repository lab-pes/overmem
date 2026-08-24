using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System;

namespace Overmem.Abstractions.Cli;

/// <summary>
/// Contract for CLI extensions that add game-specific or domain-specific commands.
/// </summary>
public interface ICliCommandExtension
{
    /// <summary>
    /// Try to parse game-specific command arguments. Returns null if the command name is not recognized by this extension.
    /// </summary>
    CliCommand? TryParse(string commandName, IReadOnlyDictionary<string, string?> options);

    /// <summary>
    /// Try to execute a game-specific CLI command. Returns null if the command type is not handled by this extension.
    /// </summary>
    Task<int>? TryExecute(CliCommand command, IServiceProvider services, TextWriter stdout, CancellationToken cancellationToken);

    /// <summary>
    /// Returns help text lines for extension-specific commands.
    /// </summary>
    IReadOnlyList<string> GetHelpLines();
}
