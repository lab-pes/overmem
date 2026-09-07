using System;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Overmem.McpServer;

var transport = args.Length > 0 ? args[0] : "stdio";
var builder = WebApplication.CreateBuilder(args);

builder.Logging.AddConsole(options =>
{
	options.LogToStandardErrorThreshold = LogLevel.Trace;
});

builder.Services.AddOvermemServices(transport);

var app = builder.Build();

if (transport == "sse")
{
    app.MapMcp("/sse");
}

await app.RunAsync();
