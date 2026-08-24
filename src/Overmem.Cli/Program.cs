using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;
using Overmem.Cli;
using Overmem.Hosting;
using Overmem.Abstractions.Cli;
using Overmem.Extensions.Pes2021;
using Overmem.Extensions.Pes2021.Cli;

var builder = Host.CreateApplicationBuilder(args);

builder.Logging.AddSimpleConsole(options =>
{
    options.SingleLine = true;
    options.TimestampFormat = "HH:mm:ss ";
});

builder.Services.Configure<ConsoleLoggerOptions>(options =>
{
    options.LogToStandardErrorThreshold = LogLevel.Trace;
});

builder.Services.AddOvermemPlatformServices();
builder.Services.AddPes2021Extension();

using var host = builder.Build();
var extensions = new ICliCommandExtension[] { new Pes2021CliExtension() };
return await CliApplication.RunAsync(args, host.Services, Console.Out, Console.Error, extensions);