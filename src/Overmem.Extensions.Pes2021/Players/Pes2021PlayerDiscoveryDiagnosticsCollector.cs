using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Overmem.Extensions.Pes2021.Fixtures;

namespace Overmem.Extensions.Pes2021.Players;

/// <summary>
/// Aggregates counters, rejection reasons, and stage timings for a player-memory discovery
/// run. The collector itself does not read memory; every value is reported by the caller
/// (anchor finder, region scanner, parser, validator, cache). Stage names are stable
/// strings that show up in the JSON payload.
/// </summary>
public sealed class Pes2021PlayerDiscoveryDiagnosticsCollector
{
    private readonly Dictionary<string, double> _stageDurationMs = new();
    private readonly Dictionary<string, int> _rejectionReasons = new();
    private readonly List<string> _warnings = new();
    private readonly List<PlayerRegionDiagnostic> _regions = new();
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
    private int _duplicatePlayerIds;
    private int _ambiguousResolutions;
    private CacheDisposition _cacheDisposition = CacheDisposition.Discovered;
    private readonly Dictionary<string, Stopwatch> _activeStages = new();

    public CacheDisposition CacheDisposition
    {
        get => _cacheDisposition;
        set => _cacheDisposition = value;
    }

    public void AddRegions(IEnumerable<PlayerRegionDiagnostic> regions)
    {
        _regions.AddRange(regions);
        _regionsEnumerated = _regions.Count;
        _regionsAccepted = _regions.Count(r => string.Equals(r.Decision, "accepted", StringComparison.OrdinalIgnoreCase));
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

    public void AddDuplicatePlayerIds(int count) => _duplicatePlayerIds += Math.Max(0, count);

    public void AddAmbiguousResolutions(int count) => _ambiguousResolutions += Math.Max(0, count);

    public void AddRejection(string reason)
    {
        if (string.IsNullOrEmpty(reason)) return;
        if (_rejectionReasons.TryGetValue(reason, out var current)) _rejectionReasons[reason] = current + 1;
        else _rejectionReasons[reason] = 1;
    }

    public void AddWarning(string warning)
    {
        if (!string.IsNullOrEmpty(warning)) _warnings.Add(warning);
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

    public PlayerDiscoveryDiagnostics Build()
    {
        foreach (var pair in _activeStages.ToArray())
        {
            EndStage(pair.Key, pair.Value);
        }

        return new PlayerDiscoveryDiagnostics(
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
            DuplicatePlayerIds: _duplicatePlayerIds,
            AmbiguousResolutions: _ambiguousResolutions,
            RejectionReasons: new Dictionary<string, int>(_rejectionReasons, StringComparer.Ordinal),
            StageDurationMs: new Dictionary<string, double>(_stageDurationMs, StringComparer.Ordinal),
            Regions: _regions.ToArray(),
            Warnings: _warnings.ToArray());
    }

    private sealed class StageScope : IDisposable
    {
        private readonly Pes2021PlayerDiscoveryDiagnosticsCollector _owner;
        private readonly string _name;
        private readonly Stopwatch _stopwatch;

        public StageScope(Pes2021PlayerDiscoveryDiagnosticsCollector owner, string name, Stopwatch stopwatch)
        {
            _owner = owner;
            _name = name;
            _stopwatch = stopwatch;
        }

        public void Dispose() => _owner.EndStage(_name, _stopwatch);
    }
}