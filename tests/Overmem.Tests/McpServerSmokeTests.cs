using Overmem.Tests.Support;
using System.Diagnostics;

namespace Overmem.Tests;

public sealed class McpServerSmokeTests
{
    [Fact]
    public async Task StdioServerStartsAndStaysAlive()
    {
        var serverAssembly = TestTargetHost.ResolveBuiltAssemblyPath("Overmem.McpServer", "src");
        using var process = Process.Start(new ProcessStartInfo
        {
            FileName = "dotnet",
            ArgumentList = { serverAssembly },
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        });

        Assert.NotNull(process);

        await Task.Delay(500);

        var stderrLine = process!.StandardError.Peek() >= 0
            ? await process.StandardError.ReadLineAsync()
            : null;

        Assert.False(process.HasExited, stderrLine);

        process.Kill(entireProcessTree: true);
        await process.WaitForExitAsync();
        Assert.True(process.HasExited);
    }
}