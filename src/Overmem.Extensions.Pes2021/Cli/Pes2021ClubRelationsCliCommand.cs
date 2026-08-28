using Overmem.Abstractions.Cli;
using Overmem.Abstractions.Processes;

namespace Overmem.Extensions.Pes2021.Cli;

public sealed record Pes2021ScanClubRelationsCliCommand(
    ProcessSelector Selector,
    string TeamCatalogPath,
    string CompetitionMapPath,
    string OutputDirectory,
    int BlockBytes,
    int RestartTimeoutSeconds) : CliCommand;
