using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace Overmem.Extensions.Pes2021.Fixtures;

/// <summary>
/// Aggregates counters, rejection reasons and stage timings for a fixture extraction. The
/// collector itself does not read memory; every value is reported by the caller (block
/// reader, parser, anchor finder, resolver, cache). Stage names are stable strings that
/// show up in the JSON payload.
/// </summary>
public sealed class Pes2021ExtractionDiagnosticsCollector
{
    private readonly Dictionary<string, double> _stageDurationMs = new();
    private readonly Dictionary<string, int> _rejectionReasons = new();
    private readonly List<string> _warnings = new();
    private int _regionsEnumerated;
    private int _regionsAccepted;
    private int _regionsRejected;
    private ulong _bytesRequested;
    private ulong _bytesRead;
    private int _readCalls;
    private int _blocksRead;
    private int _recordsDecoded;
    private int _recordsAccepted;
    private int _recordsRejected;
    private List<RegionDiagnostic> _regions = new();
    private CacheDisposition _cacheDisposition = CacheDisposition.Discovered;
    private readonly Dictionary<string, Stopwatch> _activeStages = new();

    public CacheDisposition CacheDisposition
    {
        get => _cacheDisposition;
        set => _cacheDisposition = value;
    }

    public void AddRegions(IEnumerable<RegionDiagnostic> regions)
    {
        _regions.AddRange(regions);
        _regionsEnumerated = _regions.Count;
        _regionsAccepted = _regions.Count(r => string.Equals(r.Decision, "accepted", System.StringComparison.OrdinalIgnoreCase));
        _regionsRejected = _regionsEnumerated - _regionsAccepted;
    }

    public void AddReadCall(int bytesRequested, int bytesRead)
    {
        _readCalls++;
        _blocksRead = _readCalls;
        _bytesRequested += (ulong)Math.Max(0, bytesRequested);
        _bytesRead += (ulong)Math.Max(0, bytesRead);
    }

    public void AddRecords(int decoded, int accepted, int rejected)
    {
        _recordsDecoded += decoded;
        _recordsAccepted += accepted;
        _recordsRejected += rejected;
    }

    public void AddRejection(string reason)
    {
        if (string.IsNullOrEmpty(reason))
        {
            return;
        }

        if (_rejectionReasons.TryGetValue(reason, out var count))
        {
            _rejectionReasons[reason] = count + 1;
        }
        else
        {
            _rejectionReasons[reason] = 1;
        }
    }

    public void AddWarning(string warning)
    {
        if (!string.IsNullOrEmpty(warning))
        {
            _warnings.Add(warning);
        }
    }

    public IDisposable BeginStage(string name)
    {
        var stopwatch = Stopwatch.StartNew();
        _activeStages[name] = stopwatch;
        return new StageScope(this, name, stopwatch);
    }

    private void EndStage(string name, Stopwatch stopwatch)
    {
        stopwatch.Stop();
        _stageDurationMs[name] = stopwatch.Elapsed.TotalMilliseconds;
        _activeStages.Remove(name);
    }

    public ExtractionDiagnostics Build()
    {
        foreach (var pair in _activeStages.ToArray())
        {
            EndStage(pair.Key, pair.Value);
        }

        return new ExtractionDiagnostics(
            CacheDisposition: _cacheDisposition,
            RegionsEnumerated: _regionsEnumerated,
            RegionsAccepted: _regionsAccepted,
            RegionsRejected: _regionsRejected,
            BytesRequested: _bytesRequested,
            BytesRead: _bytesRead,
            ReadCalls: _readCalls,
            BlocksRead: _blocksRead,
            RecordsDecoded: _recordsDecoded,
            RecordsAccepted: _recordsAccepted,
            RecordsRejected: _recordsRejected,
            RejectionReasons: new Dictionary<string, int>(_rejectionReasons, System.StringComparer.Ordinal),
            StageDurationMs: new Dictionary<string, double>(_stageDurationMs, System.StringComparer.Ordinal),
            Regions: _regions.ToArray(),
            Warnings: _warnings.ToArray());
    }

    private sealed class StageScope : IDisposable
    {
        private readonly Pes2021ExtractionDiagnosticsCollector _owner;
        private readonly string _name;
        private readonly Stopwatch _stopwatch;

        public StageScope(Pes2021ExtractionDiagnosticsCollector owner, string name, Stopwatch stopwatch)
        {
            _owner = owner;
            _name = name;
            _stopwatch = stopwatch;
        }

        public void Dispose()
        {
            _owner.EndStage(_name, _stopwatch);
        }
    }
}
