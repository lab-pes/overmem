using System.Diagnostics;
using System.IO.Pipes;
using System.Text.Json;
using Overmem.Tests.Support;

namespace Overmem.Tests;

public sealed class PipeRangeIntegrationTests
{
    [Fact]
    public async Task PipeTransportCanSnapshotAndRefineTheControlledTestTarget()
    {
        await using var target = await TestTargetHost.StartAsync();
        var name = "overmem-test-" + Guid.NewGuid().ToString("N");
        using var server = Process.Start(new ProcessStartInfo
        {
            FileName = "dotnet",
            ArgumentList = { TestTargetHost.ResolveBuiltAssemblyPath("Overmem.McpServer", "src"), "--pipe", name },
            RedirectStandardOutput = true, RedirectStandardError = true,
            UseShellExecute = false, CreateNoWindow = true,
        })!;
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        var stderr = server.StandardError.ReadToEndAsync();
        var stdout = server.StandardOutput.ReadToEndAsync();
        try
        {
            await using var input = new NamedPipeClientStream(".", name + ".in", PipeDirection.Out, PipeOptions.Asynchronous);
            await using var output = new NamedPipeClientStream(".", name + ".out", PipeDirection.In, PipeOptions.Asynchronous);
            await input.ConnectAsync(timeout.Token);
            await output.ConnectAsync(timeout.Token);
            using var writer = new StreamWriter(input) { AutoFlush = true };
            using var reader = new StreamReader(output);
            var sequence = 0;
            async Task<JsonElement> Call(string method, object parameters)
            {
                var id = ++sequence;
                await writer.WriteLineAsync(JsonSerializer.Serialize(new { id, method, @params = parameters }));
                using var document = JsonDocument.Parse((await reader.ReadLineAsync(timeout.Token))!);
                Assert.Equal(id, document.RootElement.GetProperty("id").GetInt32());
                Assert.False(document.RootElement.TryGetProperty("error", out _), document.RootElement.ToString());
                return document.RootElement.GetProperty("result").Clone();
            }
            var attached = await Call("attach_process", new { processId = target.Info.Pid });
            var attachmentId = attached.GetProperty("attachmentId").GetString();
            var address = target.Info.Values.Int32.Address;
            var started = await Call("start_int32_range", new {
                attachmentId, startAddress = $"0x{address:X}", endAddress = $"0x{address + 4:X}" });
            Assert.Equal(1, started.GetProperty("capturedSlots").GetInt32());
            Assert.Equal(target.Info.Values.Int32.Value.GetInt32(), started.GetProperty("firstInt32").GetInt32());
            var sessionId = started.GetProperty("sessionId").GetString();
            var refined = await Call("refine_int32_range", new { sessionId, comparison = "Unchanged" });
            Assert.Equal(1, refined.GetProperty("retainedCount").GetInt32());
            await Call("close_int32_range", new { sessionId });
            await Call("detach_process", new { attachmentId });
        }
        finally
        {
            if (!server.HasExited) server.Kill(entireProcessTree: true);
            await server.WaitForExitAsync();
            await Task.WhenAll(stderr, stdout);
        }
    }
}
