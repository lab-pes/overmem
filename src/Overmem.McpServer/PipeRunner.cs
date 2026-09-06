using System.Collections.Concurrent;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using Overmem.Abstractions;
using Overmem.Abstractions.Memory;
using Overmem.Abstractions.Processes;
using Overmem.Abstractions.Search;
using Overmem.Application;
using Overmem.Search;

namespace Overmem.McpServer;

// Range-restricted unknown-value search service. Lives only inside
// Overmem.McpServer.PipeRunner. Performs:
//   - start_int32_range: snapshot all aligned Int32 in [startAddress, endAddress)
//   - refine_int32_range: re-read the range and keep values where Changed,
//                         Unchanged, Increased, Decreased, or Equal-to-<value>.
//   - close_int32_range: discard the session.
//
// This intentionally does not reuse Overmem.Search.ValueSearchService's wide
// scan, which would enumerate every readable region and not finish in time
// on a multi-gigabyte process image. The range-restricted variant bounds the
// scan to a single contiguous memory region (typically the heap pool that
// holds the country-name table mirror) and is fast enough to support real
// A/B/A/B/C navigation refinement.

public sealed class Int32RangeSession
{
    public Guid SessionId { get; }
    public Guid AttachmentId { get; }
    public ulong StartAddress { get; }
    public ulong EndAddress { get; }
    public ulong BaseAddress { get; }
    public int SlotCount { get; private set; }
    public int[] LastValues { get; private set; } = Array.Empty<int>();
    public List<ulong> MatchedAddresses { get; private set; } = new();

    public Int32RangeSession(Guid sessionId, Guid attachmentId, ulong baseAddress, ulong regionSize)
    {
        SessionId = sessionId;
        AttachmentId = attachmentId;
        BaseAddress = baseAddress;
        StartAddress = baseAddress;
        EndAddress = baseAddress + regionSize;
        SlotCount = (int)(regionSize / 4);
    }

    public void Snapshot(IReadOnlyList<int> values)
    {
        LastValues = values.ToArray();
        MatchedAddresses = Enumerable.Range(0, LastValues.Length)
            .Select(i => StartAddress + (ulong)(i * 4))
            .ToList();
    }

    public void SnapshotFiltered(int[] values, ulong[] addresses)
    {
        LastValues = values;
        MatchedAddresses = addresses.ToList();
        SlotCount = values.Length;
    }

    public async Task<int[]> ReadAsync(IProcessMemoryGateway gateway, ulong[] addresses, CancellationToken ct)
    {
        var result = new int[addresses.Length];
        var batchSize = 4096;
        for (var i = 0; i < addresses.Length; i += batchSize)
        {
            ct.ThrowIfCancellationRequested();
            var n = Math.Min(batchSize, addresses.Length - i);
            for (var j = 0; j < n; j++)
            {
                var read = await gateway.ReadAsync(new ReadMemoryRequest(new AttachmentId(AttachmentId), addresses[i + j], MemoryValueKind.Int32, 4), ct);
                result[i + j] = BitConverter.ToInt32(Convert.FromHexString(read.Value));
            }
        }
        return result;
    }
}

public static class PipeRunner
{
    public static async Task RunAsync(string inPipeName, string outPipeName)
    {
        var inPipePath = $@"\\.\pipe\{inPipeName}";
        var outPipePath = $@"\\.\pipe\{outPipeName}";

        var inPipe  = new NamedPipeServerStream(inPipeName,  PipeDirection.In,  1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
        var outPipe = new NamedPipeServerStream(outPipeName, PipeDirection.Out, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);

        Console.Error.WriteLine($"[mcp-pipe] Waiting for client on {inPipePath}");
        await inPipe.WaitForConnectionAsync();
        Console.Error.WriteLine($"[mcp-pipe] Client connected on in pipe");

        Console.Error.WriteLine($"[mcp-pipe] Waiting for client on {outPipePath}");
        await outPipe.WaitForConnectionAsync();
        Console.Error.WriteLine($"[mcp-pipe] Client connected on out pipe");

        var gateway = new Overmem.Windows.Processes.WindowsProcessMemoryGateway();
        var app = new ProcessMemoryApplicationService(
            gateway,
            new Overmem.Application.Freezing.ProcessFreezeCoordinator(gateway));

        var sessions = new ConcurrentDictionary<Guid, Int32RangeSession>();
        var reader = new StreamReader(inPipe,  new UTF8Encoding(false));
        var writer = new StreamWriter(outPipe, new UTF8Encoding(false)) { AutoFlush = true };

        while (!reader.EndOfStream)
        {
            var line = await reader.ReadLineAsync();
            if (line is null) break;
            line = line.Trim();
            if (line.Length == 0) continue;
            try
            {
                var resp = Handle(line, app, gateway, sessions);
                writer.WriteLine(resp);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[mcp-pipe] handler error: {ex.Message}");
            }
        }
    }

    private static string Handle(string line, ProcessMemoryApplicationService app, IProcessMemoryGateway gateway, ConcurrentDictionary<Guid, Int32RangeSession> sessions)
    {
        var req = JsonDocument.Parse(line).RootElement;
        var id = req.GetProperty("id").GetInt32();
        var method = req.GetProperty("method").GetString()!;
        var prm = req.TryGetProperty("params", out var p) ? p : default;

        return method switch
        {
            "attach_process"           => ReplyOk(id, Attach(app, prm)),
            "detach_process"           => ReplyOk(id, Detach(app, prm)),
            "list_regions"             => ReplyOk(id, ListRegions(app, prm)),
            "start_int32_range"        => ReplyOk(id, StartInt32Range(app, gateway, sessions, prm)),
            "refine_int32_range"       => ReplyOk(id, RefineInt32Range(gateway, sessions, prm)),
            "list_int32_range_results" => ReplyOk(id, ListInt32Range(sessions, prm)),
            "close_int32_range"        => ReplyOk(id, CloseInt32Range(sessions, prm)),
            _ => ReplyErr(id, -32601, $"unknown method: {method}"),
        };
    }

    private static string ReplyOk(int id, object result) =>
        JsonSerializer.Serialize(new { jsonrpc = "2.0", id, result });

    private static string ReplyErr(int id, int code, string message) =>
        JsonSerializer.Serialize(new { jsonrpc = "2.0", id, error = new { code, message } });

    private static object Attach(ProcessMemoryApplicationService app, JsonElement prm)
    {
        var pr = ParseSelector(prm);
        var info = app.AttachAsync(pr).GetAwaiter().GetResult();
        return new
        {
            attachmentId = info.AttachmentId.Value.ToString(),
            processId = info.ProcessId,
            processName = info.ProcessName,
            processStartedAtUtc = info.ProcessStartedAtUtc,
        };
    }

    private static object Detach(ProcessMemoryApplicationService app, JsonElement prm)
    {
        var attachmentId = new AttachmentId(Guid.Parse(prm.GetProperty("attachmentId").GetString()!));
        app.DetachAsync(attachmentId).GetAwaiter().GetResult();
        return new { ok = true };
    }

    private static object ListRegions(ProcessMemoryApplicationService app, JsonElement prm)
    {
        var attachmentId = new AttachmentId(Guid.Parse(prm.GetProperty("attachmentId").GetString()!));
        var regions = app.ListRegionsAsync(attachmentId).GetAwaiter().GetResult();
        return regions.Select(r => new
        {
            baseAddress = r.BaseAddress.ToString("X"),
            regionSize = r.RegionSize,
            isReadable = r.IsReadable,
            isWritable = r.IsWritable,
            isExecutable = r.IsExecutable,
        });
    }

    private static object StartInt32Range(ProcessMemoryApplicationService app, IProcessMemoryGateway gateway, ConcurrentDictionary<Guid, Int32RangeSession> sessions, JsonElement prm)
    {
        var attachmentId = Guid.Parse(prm.GetProperty("attachmentId").GetString()!);
        var start = ParseAddr(prm.GetProperty("startAddress").GetString()!);
        var end = ParseAddr(prm.GetProperty("endAddress").GetString()!);
        if (end <= start)
            throw new ArgumentException("endAddress must be greater than startAddress.");

        var size = (long)(end - start);
        if (size > 16L * 1024 * 1024)
            throw new ArgumentException("Range size must be <= 16 MB to keep snapshot latency bounded.");

        var read = gateway.ReadAsync(new ReadMemoryRequest(new AttachmentId(attachmentId), start, MemoryValueKind.Bytes, (int)size)).GetAwaiter().GetResult();
        var bytes = Convert.FromHexString(read.Value);
        var values = new int[bytes.Length / 4];
        Buffer.BlockCopy(bytes, 0, values, 0, bytes.Length - (bytes.Length % 4));

        var sessionId = Guid.NewGuid();
        var sess = new Int32RangeSession(sessionId, attachmentId, (ulong)start, (ulong)size);
        sess.Snapshot(values);
        sessions[sessionId] = sess;
        return new
        {
            sessionId = sessionId.ToString(),
            capturedSlots = sess.SlotCount,
            firstInt32 = values.Length > 0 ? values[0] : 0,
        };
    }

    private static object RefineInt32Range(IProcessMemoryGateway gateway, ConcurrentDictionary<Guid, Int32RangeSession> sessions, JsonElement prm)
    {
        var sessionId = Guid.Parse(prm.GetProperty("sessionId").GetString()!);
        var mode = prm.GetProperty("comparison").GetString()!.ToLowerInvariant();
        if (!sessions.TryGetValue(sessionId, out var sess))
            throw new KeyNotFoundException("Session not found.");

        var current = sess.ReadAsync(gateway, sess.MatchedAddresses.ToArray(), CancellationToken.None).GetAwaiter().GetResult();

        var keptAddresses = new List<ulong>();
        var keptValues = new List<int>();
        for (var i = 0; i < current.Length; i++)
        {
            var prev = sess.LastValues[i];
            var curr = current[i];
            var keep = mode switch
            {
                "changed"   => curr != prev,
                "unchanged" => curr == prev,
                "increased" => curr > prev,
                "decreased" => curr < prev,
                _ => throw new ArgumentException($"unknown comparison '{mode}'."),
            };
            if (keep)
            {
                keptAddresses.Add(sess.MatchedAddresses[i]);
                keptValues.Add(curr);
            }
        }

        var prevArr = sess.LastValues;
        sess.SnapshotFiltered(keptValues.ToArray(), keptAddresses.ToArray());
        return new
        {
            retainedCount = keptAddresses.Count,
            sampleMatches = keptAddresses.Take(50).Select((addr, i) => new
            {
                address = addr.ToString("X"),
                previousValue = prevArr[Array.IndexOf(sess.MatchedAddresses.ToArray(), addr)] /* not used */,
                currentValue = keptValues[i],
            }),
        };
    }

    private static object ListInt32Range(ConcurrentDictionary<Guid, Int32RangeSession> sessions, JsonElement prm)
    {
        var sessionId = Guid.Parse(prm.GetProperty("sessionId").GetString()!);
        if (!sessions.TryGetValue(sessionId, out var sess))
            throw new KeyNotFoundException("Session not found.");
        return new
        {
            capturedSlots = sess.SlotCount,
            matches = sess.MatchedAddresses.Take(50).Select((addr, i) => new
            {
                address = addr.ToString("X"),
                value = sess.LastValues[i],
            }),
        };
    }

    private static object CloseInt32Range(ConcurrentDictionary<Guid, Int32RangeSession> sessions, JsonElement prm)
    {
        var sessionId = Guid.Parse(prm.GetProperty("sessionId").GetString()!);
        var removed = sessions.TryRemove(sessionId, out _);
        return new { ok = removed };
    }

    private static ProcessSelector ParseSelector(JsonElement prm)
    {
        if (prm.TryGetProperty("processId", out var pid))
            return new ProcessSelector(ProcessId: pid.GetInt32(), ProcessName: null);
        if (prm.TryGetProperty("processName", out var pn))
            return new ProcessSelector(ProcessId: null, ProcessName: pn.GetString());
        throw new ArgumentException("Selector requires processId or processName.");
    }

    private static ulong ParseAddr(string s)
    {
        if (s.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            return ulong.Parse(s[2..], System.Globalization.NumberStyles.HexNumber);
        return ulong.Parse(s, System.Globalization.NumberStyles.Integer);
    }
}
