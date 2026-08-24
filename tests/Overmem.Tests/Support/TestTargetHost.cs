using System.Diagnostics;
using System.Text.Json;

namespace Overmem.Tests.Support;

internal sealed class TestTargetHost : IAsyncDisposable
{
    private readonly Process _process;

    public TestTargetHost(Process process, TestTargetInfo info)
    {
        _process = process;
        Info = info;
    }

    public TestTargetInfo Info { get; }

    public static async Task<TestTargetHost> StartAsync()
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            ArgumentList = { ResolveBuiltAssemblyPath("Overmem.TestTarget", "tests") },
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Failed to start the test target process.");

        var line = await process.StandardOutput.ReadLineAsync();
        if (string.IsNullOrWhiteSpace(line))
        {
            var stderr = await process.StandardError.ReadToEndAsync();
            throw new InvalidOperationException($"Test target did not emit startup metadata. STDERR: {stderr}");
        }

        var info = JsonSerializer.Deserialize<TestTargetInfo>(line, JsonOptions)
            ?? throw new InvalidOperationException("Failed to deserialize test target metadata.");

        return new TestTargetHost(process, info);
    }

    public async ValueTask DisposeAsync()
    {
        if (!_process.HasExited)
        {
            _process.Kill(entireProcessTree: true);
            await _process.WaitForExitAsync();
        }

        _process.Dispose();
    }

    public static string ResolveBuiltAssemblyPath(string projectName, string rootFolder)
    {
        var root = FindRepositoryRoot();
        var path = Path.Combine(root, rootFolder, projectName, "bin", "Debug", "net8.0", $"{projectName}.dll");
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Built assembly not found at '{path}'. Build the solution before running tests.", path);
        }

        return path;
    }

    private static string FindRepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "Overmem.slnx")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate the repository root from the test output directory.");
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };
}

internal sealed record TestTargetInfo(int Pid, TestTargetValues Values);

internal sealed record TestTargetValues(
    TestTargetValueInfo Int32,
    TestTargetMutableIntInfo MutableInt,
    TestTargetValueInfo Double,
    TestTargetStringValueInfo Utf8,
    TestTargetPointerChainInfo PointerChain,
    TestTargetModulePointerChainInfo ModulePointerChain,
    TestTargetPatternInfo Pattern);

internal record TestTargetValueInfo(ulong Address, JsonElement Value);

internal sealed record TestTargetMutableIntInfo(ulong Address, int FrozenValue, int MutationIntervalMs);

internal sealed record TestTargetStringValueInfo(ulong Address, int Size, string Value);

internal sealed record TestTargetPointerChainInfo(ulong BaseAddress, long[] Offsets, ulong ResolvedAddress);

internal sealed record TestTargetModulePointerChainInfo(string ModuleName, long BaseOffset, long[] Offsets, ulong ResolvedAddress);

internal sealed record TestTargetPatternInfo(ulong Address, string Pattern, string WildcardPattern);