using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Overmem.McpServer;

// Usage:
//   Overmem.McpServer              -> MCP-over-stdio (full JSON-RPC tool surface)
//   Overmem.McpServer --pipe <name> -> range-restricted pipe bridge for live A/B/A/B/C refinement

var pipeMode = false;
var pipeBase = "overmem";
for (var i = 0; i < args.Length; i++)
{
    if (args[i] is "--pipe" or "--pipe-search" && i + 1 < args.Length)
    {
        pipeMode = true;
        pipeBase = args[i + 1];
        i++;
    }
}

if (pipeMode)
{
    var inName = $"{pipeBase}.in";
    var outName = $"{pipeBase}.out";
    Console.Error.WriteLine($"[mcp] Pipe bridge (range-restricted). in=\\\\.\\pipe\\{inName} out=\\\\.\\pipe\\{outName}");
    await Overmem.McpServer.PipeRunner.RunAsync(inName, outName);
    return;
}

var builder = Host.CreateApplicationBuilder(args);
builder.Logging.AddConsole(options => { options.LogToStandardErrorThreshold = LogLevel.Trace; });
builder.Services.AddOvermemServices();
await builder.Build().RunAsync();
