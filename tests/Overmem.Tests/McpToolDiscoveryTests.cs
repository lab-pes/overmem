using System.Diagnostics;
using System.Text.Json;
using Overmem.Tests.Support;

namespace Overmem.Tests;

public sealed class McpToolDiscoveryTests
{
    [Fact]
    public async Task StdioHandshakeListsBothCalendarAndPlayerTools()
    {
        using var process = Process.Start(new ProcessStartInfo
        {
            FileName = "dotnet",
            ArgumentList = { TestTargetHost.ResolveBuiltAssemblyPath("Overmem.McpServer", "src") },
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        })!;
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        var stderr = process.StandardError.ReadToEndAsync();
        try
        {
            await process.StandardInput.WriteLineAsync("{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"initialize\",\"params\":{\"protocolVersion\":\"2025-03-26\",\"capabilities\":{},\"clientInfo\":{\"name\":\"overmem-tests\",\"version\":\"1.0\"}}}");
            await process.StandardInput.FlushAsync();
            using var initialized = await ReadResponse(process, 1, timeout.Token);
            Assert.True(initialized.RootElement.TryGetProperty("result", out _));
            await process.StandardInput.WriteLineAsync("{\"jsonrpc\":\"2.0\",\"method\":\"notifications/initialized\"}");
            await process.StandardInput.WriteLineAsync("{\"jsonrpc\":\"2.0\",\"id\":2,\"method\":\"tools/list\"}");
            await process.StandardInput.FlushAsync();
            using var listed = await ReadResponse(process, 2, timeout.Token);
            var names = listed.RootElement.GetProperty("result").GetProperty("tools")
                .EnumerateArray().Select(t => t.GetProperty("name").GetString()).ToArray();
            foreach (var name in new[] { "attach_process", "pes2021_find_fixture_anchor",
                "pes2021_extract_competition_fixtures", "pes2021_find_player_anchor", "pes2021_scan_players",
                "pes2021_query_player", "pes2021_discover_player_families", "pes2021_inventory_player_hits",
                "pes2021_compare_player_sessions", "pes2021_export_family_catalog",
                "pes2021_inspect_daily_calendar_candidate", "pes2021_scan_daily_calendar_candidates",
                "pes2021_find_daily_calendar_base_by_date", "pes2021_dump_daily_calendar_day" })
                Assert.Contains(name, names);
            Assert.Equal(names.Length, names.Distinct().Count());
        }
        finally
        {
            if (!process.HasExited) process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync();
            await stderr;
        }
    }

    private static async Task<JsonDocument> ReadResponse(Process process, int id, CancellationToken ct)
    {
        while (true)
        {
            var line = await process.StandardOutput.ReadLineAsync(ct)
                ?? throw new EndOfStreamException("MCP server exited before responding.");
            var document = JsonDocument.Parse(line);
            if (document.RootElement.TryGetProperty("id", out var value) && value.GetInt32() == id)
                return document;
            document.Dispose();
        }
    }
}
