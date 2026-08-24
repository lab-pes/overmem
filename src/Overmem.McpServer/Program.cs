using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Overmem.McpServer;

var builder = Host.CreateApplicationBuilder(args);

builder.Logging.AddConsole(options =>
{
	options.LogToStandardErrorThreshold = LogLevel.Trace;
});

builder.Services.AddOvermemServices();

await builder.Build().RunAsync();
