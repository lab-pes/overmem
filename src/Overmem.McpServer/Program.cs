using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Overmem.McpServer;

// Stdio remains the default. HTTP and the range-search pipe are explicit modes.
if (args.Length > 0 && args[0] is "--pipe" or "--pipe-search")
{
    if (args.Length != 2 || string.IsNullOrWhiteSpace(args[1]))
        throw new ArgumentException("Usage: Overmem.McpServer --pipe <name>");
    await PipeRunner.RunAsync($"{args[1]}.in", $"{args[1]}.out");
    return;
}

var transport = args.Length > 0 && args[0] is "stdio" or "sse" ? args[0] : "stdio";
var hostArgs = args.Length > 0 && args[0] == transport ? args[1..] : args;
if (transport == "sse")
{
    var builder = WebApplication.CreateBuilder(hostArgs);
    if (string.IsNullOrEmpty(builder.Configuration["urls"]))
        builder.WebHost.UseUrls("http://localhost:5000");
    builder.Logging.AddConsole(options => options.LogToStandardErrorThreshold = LogLevel.Trace);
    builder.Services.AddOvermemServices(transport);
    var app = builder.Build();
    app.MapMcp("/sse");
    await app.RunAsync();
}
else
{
    var builder = Host.CreateApplicationBuilder(hostArgs);
    builder.Logging.AddConsole(options => options.LogToStandardErrorThreshold = LogLevel.Trace);
    builder.Services.AddOvermemServices();
    await builder.Build().RunAsync();
}
