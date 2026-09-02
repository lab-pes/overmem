# P5 - review request

Reviewer: Codex
Scope: CLI + MCP surface for read-only player-memory operations.

## Acceptance gates (from implementation-packages.md P5)

- [x] Player-memory services registered through `Pes2021Extension`.
- [x] CLI commands parse and dispatch through the existing `Pes2021CliExtension`.
- [x] MCP tools registered as `[McpServerTool]` methods.
- [x] Help lines added for every new command.
- [x] No write command exposed.

## Review questions

1. Does the CLI extension still detach and clear the catalog per command?
   Yes: `ExecutePlayerAttachmentAsync` calls `DetachAsync` and clears the
   catalog in `finally`.
2. Are MCP tools honest about identity? Yes: `FindPlayerAnchor` and `ScanPlayers`
   carry an `AttachmentId` and synthesize a placeholder process identity so the
   same discovery signature is used. A future package will thread the real
   `AttachmentInfo` through the gateway.
3. Does the export command always produce an atomic file? Yes: it delegates to
   `Pes2021AtomicFileWriter.WriteJson`, which writes to `.tmp`, flushes, and
   renames over the target.
4. Are the help lines exhaustive? Yes: `GetHelpLines` lists every new command
   with its options; the test asserts each line is present.

## Reproduce

```powershell
dotnet build Overmem.slnx
dotnet test Overmem.slnx --no-build
dotnet test tests\Overmem.Extensions.Pes2021.Tests\Overmem.Extensions.Pes2021.Tests.csproj --no-build --filter "FullyQualifiedName~Pes2021PlayerCliSurfaceTests"
```