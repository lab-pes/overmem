using System;
using System.Collections.Generic;

namespace Overmem.Extensions.Pes2021.Players.FamilyDiscovery;

public sealed record SessionProvenance(
    int ProcessId,
    DateTimeOffset StartTime,
    string ExecutableSha256,
    string ExecutableVersion,
    IReadOnlyList<string> LoadedModules,
    string ProfileId,
    string ProfileSha256,
    string DeclaredState, // Menu, Editor, ML
    string OptionsHash, // Hash of scanner options (policy, etc)
    DateTimeOffset ScanStarted,
    DateTimeOffset ScanFinished,
    int TotalRegionsRead,
    ulong TotalBytesRead,
    int WriteOperationsCount // Proof of zero-write
);
