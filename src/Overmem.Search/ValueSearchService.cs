using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Overmem.Abstractions;
using Overmem.Abstractions.Memory;
using Overmem.Abstractions.Search;
using Overmem.Runtime;
using Overmem.Runtime.Diagnostics;

namespace Overmem.Search;

public sealed class ValueSearchService : IValueSearchService
{
    private const int ChunkSize = 64 * 1024;

    private readonly ISystemClock _clock;
    private readonly IProcessMemoryGateway _gateway;
    private readonly ILogger<ValueSearchService> _logger;
    private readonly IOperationJournal _operationJournal;
    private readonly Dictionary<ValueSearchSessionId, SearchSessionState> _sessions = [];
    private readonly object _sync = new();

    public ValueSearchService(IProcessMemoryGateway gateway, ISystemClock clock, IOperationJournal operationJournal, ILogger<ValueSearchService> logger)
    {
        _gateway = gateway;
        _clock = clock;
        _operationJournal = operationJournal;
        _logger = logger;
    }

    public ValueSearchService(IProcessMemoryGateway gateway)
        : this(gateway, SystemClock.Instance, new InMemoryOperationJournal(), NullLogger<ValueSearchService>.Instance)
    {
    }

    public Task<bool> CloseSessionAsync(ValueSearchSessionId sessionId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var removed = false;
        lock (_sync)
        {
            removed = _sessions.Remove(sessionId);
        }

        if (removed)
        {
            Record("close_value_search_session", "Succeeded", null, $"Session={sessionId}");
        }

        return Task.FromResult(removed);
    }

    public Task<ValueSearchResult> GetResultsAsync(ValueSearchSessionId sessionId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        SearchSessionState session;
        lock (_sync)
        {
            session = GetSession(sessionId);
        }

        return Task.FromResult(ToResult(session, ValueSearchComparison.Exact));
    }

    public Task<IReadOnlyList<ValueSearchSessionInfo>> ListSessionsAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (_sync)
        {
            return Task.FromResult<IReadOnlyList<ValueSearchSessionInfo>>(_sessions.Values
                .Select(session => session.Info)
                .OrderByDescending(session => session.UpdatedAtUtc)
                .ToArray());
        }
    }

    public async Task<ValueSearchResult> RefineAsync(RefineValueSearchRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        SearchSessionState session;
        lock (_sync)
        {
            session = GetSession(request.SessionId);
        }

        byte[]? exactBytes = null;
        if (request.Comparison is ValueSearchComparison.Exact or ValueSearchComparison.NotEqual)
        {
            if (string.IsNullOrWhiteSpace(request.Value))
            {
                throw new ArgumentException($"A value is required for {request.Comparison} refinements.", nameof(request));
            }

            exactBytes = ValueSearchCodec.ParseExactValue(session.Info.ValueKind, request.Value, session.Info.Size);
        }

        double betweenLow = 0;
        double betweenHigh = 0;
        double deltaValue = 0;

        if (request.Comparison is ValueSearchComparison.Increased
            or ValueSearchComparison.Decreased
            or ValueSearchComparison.IncreasedBy
            or ValueSearchComparison.DecreasedBy
            or ValueSearchComparison.ChangedBy
            or ValueSearchComparison.Between)
        {
            EnsureRelativeNumericComparisonSupported(session.Info.ValueKind);
        }

        if (request.Comparison is ValueSearchComparison.IncreasedBy
            or ValueSearchComparison.DecreasedBy
            or ValueSearchComparison.ChangedBy)
        {
            if (string.IsNullOrWhiteSpace(request.Value))
            {
                throw new ArgumentException($"A delta value is required for {request.Comparison} refinements.", nameof(request));
            }

            deltaValue = ValueSearchCodec.ParseNumeric(session.Info.ValueKind, request.Value);
        }

        if (request.Comparison is ValueSearchComparison.Between)
        {
            if (string.IsNullOrWhiteSpace(request.Value) || string.IsNullOrWhiteSpace(request.SecondaryValue))
            {
                throw new ArgumentException("Between requires both Value (lower bound) and SecondaryValue (upper bound).", nameof(request));
            }

            betweenLow = ValueSearchCodec.ParseNumeric(session.Info.ValueKind, request.Value);
            betweenHigh = ValueSearchCodec.ParseNumeric(session.Info.ValueKind, request.SecondaryValue);
            if (betweenLow > betweenHigh)
            {
                (betweenLow, betweenHigh) = (betweenHigh, betweenLow);
            }
        }

        var retained = new List<SearchMatchState>(session.Matches.Count);
        foreach (var match in session.Matches)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var currentBytes = await ReadBytesAsync(session.Info.AttachmentId, match.Address, session.ValueSize, cancellationToken);
            var keep = request.Comparison switch
            {
                ValueSearchComparison.Exact => currentBytes.SequenceEqual(exactBytes!),
                ValueSearchComparison.NotEqual => !currentBytes.SequenceEqual(exactBytes!),
                ValueSearchComparison.Changed => !currentBytes.SequenceEqual(match.LastBytes),
                ValueSearchComparison.Unchanged => currentBytes.SequenceEqual(match.LastBytes),
                ValueSearchComparison.Increased => ValueSearchCodec.Compare(session.Info.ValueKind, currentBytes, match.LastBytes) > 0,
                ValueSearchComparison.Decreased => ValueSearchCodec.Compare(session.Info.ValueKind, currentBytes, match.LastBytes) < 0,
                ValueSearchComparison.IncreasedBy => NumericDelta(session.Info.ValueKind, currentBytes, match.LastBytes) == deltaValue,
                ValueSearchComparison.DecreasedBy => NumericDelta(session.Info.ValueKind, currentBytes, match.LastBytes) == -deltaValue,
                ValueSearchComparison.ChangedBy => Math.Abs(NumericDelta(session.Info.ValueKind, currentBytes, match.LastBytes)) == Math.Abs(deltaValue),
                ValueSearchComparison.Between => IsBetween(session.Info.ValueKind, currentBytes, betweenLow, betweenHigh),
                _ => throw new ArgumentOutOfRangeException(nameof(request))
            };

            if (keep)
            {
                retained.Add(new SearchMatchState(match.Address, currentBytes));
            }
        }

        var now = _clock.UtcNow;
        lock (_sync)
        {
            var current = GetSession(request.SessionId);
            current.Matches = retained;
            current.Info = current.Info with
            {
                ResultCount = retained.Count,
                UpdatedAtUtc = now,
            };

            session = current;
        }

        Record("refine_value_search", "Succeeded", session.Info.AttachmentId.ToString(), $"Session={request.SessionId}; Comparison={request.Comparison}; Results={retained.Count}");
        _logger.LogInformation("Refined value search session {SessionId} with comparison {Comparison}. Remaining results: {ResultCount}.", request.SessionId, request.Comparison, retained.Count);
        return ToResult(session, request.Comparison);
    }

    public async Task<ValueSearchResult> StartExactSearchAsync(StartValueSearchRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.Alignment <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(request), "Alignment must be greater than zero.");
        }

        if (request.MaxResults <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(request), "MaxResults must be greater than zero.");
        }

        var expectedBytes = ValueSearchCodec.ParseExactValue(request.ValueKind, request.Value, request.Size);
        var matches = await ScanMatchesAsync(request.AttachmentId, expectedBytes, request.Alignment, request.MaxResults, cancellationToken);
        var sessionId = ValueSearchSessionId.New();
        var now = _clock.UtcNow;
        var session = new SearchSessionState(
            new ValueSearchSessionInfo(sessionId, request.AttachmentId, request.ValueKind, request.Size, request.Alignment, matches.Count, now, now),
            request.ValueKind,
            expectedBytes.Length,
            matches);

        lock (_sync)
        {
            _sessions[sessionId] = session;
        }

        Record("start_value_search", "Succeeded", request.AttachmentId.ToString(), $"Session={sessionId}; Results={matches.Count}");
        _logger.LogInformation("Started value search session {SessionId} for attachment {AttachmentId}. Result count: {ResultCount}.", sessionId, request.AttachmentId, matches.Count);
        return ToResult(session, ValueSearchComparison.Exact);
    }

    public async Task<ValueSearchResult> StartUnknownSearchAsync(StartUnknownValueSearchRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.Alignment <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(request), "Alignment must be greater than zero.");
        }

        if (request.MaxResults <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(request), "MaxResults must be greater than zero.");
        }

        var valueSize = ResolveUnknownValueSize(request.ValueKind, request.Size);
        var matches = await ScanAllMatchesAsync(request.AttachmentId, request.ValueKind, valueSize, request.Alignment, request.MaxResults, cancellationToken);
        var sessionId = ValueSearchSessionId.New();
        var now = _clock.UtcNow;
        var session = new SearchSessionState(
            new ValueSearchSessionInfo(sessionId, request.AttachmentId, request.ValueKind, request.Size, request.Alignment, matches.Count, now, now, IsUnknownStart: true),
            request.ValueKind,
            valueSize,
            matches);

        lock (_sync)
        {
            _sessions[sessionId] = session;
        }

        Record("start_unknown_value_search", "Succeeded", request.AttachmentId.ToString(), $"Session={sessionId}; Results={matches.Count}");
        _logger.LogInformation("Started unknown value search session {SessionId} for attachment {AttachmentId}. Snapshot count: {ResultCount}.", sessionId, request.AttachmentId, matches.Count);
        return ToResult(session, ValueSearchComparison.Unchanged);
    }

    private SearchSessionState GetSession(ValueSearchSessionId sessionId)
        => _sessions.TryGetValue(sessionId, out var session)
            ? session
            : throw new KeyNotFoundException($"Value search session '{sessionId}' was not found.");

    private static void EnsureRelativeNumericComparisonSupported(MemoryValueKind valueKind)
    {
        if (valueKind is MemoryValueKind.Int32 or MemoryValueKind.Int64 or MemoryValueKind.Float or MemoryValueKind.Double)
        {
            return;
        }

        throw new NotSupportedException($"Comparison mode requires a numeric value kind. Received '{valueKind}'.");
    }

    private static int ResolveUnknownValueSize(MemoryValueKind valueKind, int explicitSize)
        => valueKind switch
        {
            MemoryValueKind.Int32 => 4,
            MemoryValueKind.Int64 => 8,
            MemoryValueKind.Float => 4,
            MemoryValueKind.Double => 8,
            MemoryValueKind.Bytes when explicitSize > 0 => explicitSize,
            MemoryValueKind.Utf8String when explicitSize > 0 => explicitSize,
            MemoryValueKind.Utf16String when explicitSize > 0 => explicitSize,
            _ => throw new ArgumentException($"An explicit size is required for value kind '{valueKind}' in an unknown-value search.")
        };

    private static double NumericDelta(MemoryValueKind valueKind, byte[] current, byte[] previous)
        => ValueSearchCodec.ToDouble(valueKind, current) - ValueSearchCodec.ToDouble(valueKind, previous);

    private static bool IsBetween(MemoryValueKind valueKind, byte[] current, double low, double high)
    {
        var value = ValueSearchCodec.ToDouble(valueKind, current);
        return value >= low && value <= high;
    }

    private static bool MatchesAt(byte[] buffer, int position, byte[] expected)
    {
        for (var index = 0; index < expected.Length; index++)
        {
            if (buffer[position + index] != expected[index])
            {
                return false;
            }
        }

        return true;
    }

    private void Record(string operationName, string outcome, string? attachmentId, string? detail = null)
        => _operationJournal.Record(new OperationLogEntry(Guid.NewGuid(), operationName, outcome, _clock.UtcNow, attachmentId, detail));

    private async Task<byte[]> ReadBytesAsync(Overmem.Abstractions.Processes.AttachmentId attachmentId, ulong address, int size, CancellationToken cancellationToken)
    {
        var result = await _gateway.ReadAsync(new ReadMemoryRequest(attachmentId, address, MemoryValueKind.Bytes, size), cancellationToken);
        return Convert.FromHexString(result.Value);
    }

    private async Task<List<SearchMatchState>> ScanMatchesAsync(Overmem.Abstractions.Processes.AttachmentId attachmentId, byte[] expected, int alignment, int maxResults, CancellationToken cancellationToken)
    {
        var matches = new List<SearchMatchState>();
        var overlap = Math.Max(expected.Length - 1, 0);
        var regions = await _gateway.ListRegionsAsync(attachmentId, cancellationToken);

        foreach (var region in regions.Where(region => region.IsReadable && region.RegionSize >= (ulong)expected.Length))
        {
            cancellationToken.ThrowIfCancellationRequested();

            for (ulong cursor = 0; cursor < region.RegionSize && matches.Count < maxResults; cursor += (ulong)ChunkSize)
            {
                var remaining = region.RegionSize - cursor;
                var primaryLength = (int)Math.Min((ulong)ChunkSize, remaining);
                var bytesToRead = (int)Math.Min((ulong)(ChunkSize + overlap), remaining);
                byte[] buffer;
                try
                {
                    buffer = await ReadBytesAsync(attachmentId, region.BaseAddress + cursor, bytesToRead, cancellationToken);
                }
                catch (InvalidOperationException)
                {
                    continue;
                }

                if (buffer.Length < expected.Length)
                {
                    continue;
                }

                var scanLimit = Math.Min(primaryLength, buffer.Length - expected.Length + 1);
                for (var position = 0; position < scanLimit && matches.Count < maxResults; position++)
                {
                    var absoluteAddress = region.BaseAddress + cursor + (ulong)position;
                    if (alignment > 1 && absoluteAddress % (ulong)alignment != 0)
                    {
                        continue;
                    }

                    if (MatchesAt(buffer, position, expected))
                    {
                        matches.Add(new SearchMatchState(absoluteAddress, buffer.AsSpan(position, expected.Length).ToArray()));
                    }
                }
            }
        }

        return matches;
    }

    private static ValueSearchResult ToResult(SearchSessionState session, ValueSearchComparison comparison)
        => new(
            session.Info.SessionId,
            session.Info.ValueKind,
            comparison,
            session.Matches.Count,
            session.Matches
                .Select(match => new ValueSearchMatch(match.Address, ValueSearchCodec.FormatValue(session.Info.ValueKind, match.LastBytes)))
                .ToArray());

    private async Task<List<SearchMatchState>> ScanAllMatchesAsync(Overmem.Abstractions.Processes.AttachmentId attachmentId, MemoryValueKind valueKind, int valueSize, int alignment, int maxResults, CancellationToken cancellationToken)
    {
        var matches = new List<SearchMatchState>();
        var regions = await _gateway.ListRegionsAsync(attachmentId, cancellationToken);

        foreach (var region in regions.Where(r => r.IsReadable && r.RegionSize >= (ulong)valueSize))
        {
            cancellationToken.ThrowIfCancellationRequested();

            for (ulong cursor = 0; cursor < region.RegionSize && matches.Count < maxResults; cursor += (ulong)ChunkSize)
            {
                var remaining = region.RegionSize - cursor;
                var bytesToRead = (int)Math.Min((ulong)ChunkSize, remaining);
                byte[] buffer;
                try
                {
                    buffer = await ReadBytesAsync(attachmentId, region.BaseAddress + cursor, bytesToRead, cancellationToken);
                }
                catch (InvalidOperationException)
                {
                    continue;
                }

                var scanLimit = buffer.Length - valueSize + 1;
                for (var position = 0; position < scanLimit && matches.Count < maxResults; position++)
                {
                    var absoluteAddress = region.BaseAddress + cursor + (ulong)position;
                    if (alignment > 1 && absoluteAddress % (ulong)alignment != 0)
                    {
                        continue;
                    }

                    matches.Add(new SearchMatchState(absoluteAddress, buffer.AsSpan(position, valueSize).ToArray()));
                }
            }
        }

        return matches;
    }

    private sealed record SearchMatchState(ulong Address, byte[] LastBytes);

    private sealed class SearchSessionState(ValueSearchSessionInfo info, MemoryValueKind valueKind, int valueSize, List<SearchMatchState> matches)
    {
        public ValueSearchSessionInfo Info { get; set; } = info;

        public MemoryValueKind ValueKind { get; } = valueKind;

        public int ValueSize { get; } = valueSize;

        public List<SearchMatchState> Matches { get; set; } = matches;
    }
}