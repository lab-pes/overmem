using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Overmem.Abstractions;
using Overmem.Abstractions.Memory;
using Overmem.Abstractions.Processes;
using Overmem.Runtime;

namespace Overmem.Extensions.Pes2021.Fixtures;

/// <summary>
/// Identity key used by <see cref="Pes2021CalendarSessionCache"/>. It bundles every field
/// the plan requires to distinguish two attaches to the same PID: the public attachment id,
/// the PID, the optional process start time (null when the OS denies access), and the
/// profile identity (id + version + SHA-256).
/// </summary>
public sealed record CalendarSessionCacheKey(
    AttachmentId AttachmentId,
    int ProcessId,
    DateTimeOffset? ProcessStartedAtUtc,
    string ProfileId,
    string ProfileVersion,
    string ProfileSha256);

/// <summary>
/// Cached value associated with a key. The anchor finder and the extractor share the same
/// entry; <see cref="AnchorAddress"/>, <see cref="CompetitionBlockBaseAddress"/> and
/// <see cref="CalendarArrayBaseAddress"/> mirror the same fields in <see cref="CalendarSession"/>.
/// The <see cref="ValidationSampleSha256"/> is what the cache rechecks before reusing the
/// entry; if the bytes changed the entry is invalidated and the caller is asked to
/// rediscover.
/// </summary>
public sealed record CalendarSessionCacheEntry(
    CacheDisposition Disposition,
    string AnchorAddress,
    string CompetitionBlockBaseAddress,
    string? CalendarArrayBaseAddress,
    int AnchorIndex,
    string ValidationSampleSha256,
    DateTimeOffset ValidatedAtUtc);

/// <summary>
/// In-memory cache for PES 2021 calendar sessions. The cache is keyed by the
/// <see cref="CalendarSessionCacheKey"/> tuple: changing any of its parts produces a
/// different entry, so a PES restart naturally invalidates the previous session.
///
/// The cache never persists across host process restarts: <see cref="Invalidate"/> is
/// called on detach and any failure leaves the entry usable only when the
/// <see cref="ValidationSampleSha256"/> still matches the bytes at the stored address.
/// </summary>
public sealed class Pes2021CalendarSessionCache
{
    private readonly ConcurrentDictionary<CalendarSessionCacheKey, CalendarSessionCacheEntry> _entries = new();
    private readonly IProcessMemoryGateway _gateway;
    private readonly ISystemClock _clock;

    public Pes2021CalendarSessionCache(IProcessMemoryGateway gateway, ISystemClock clock)
    {
        _gateway = gateway;
        _clock = clock;
    }

    public int Count => _entries.Count;

    public bool TryGet(CalendarSessionCacheKey key, out CalendarSessionCacheEntry? entry)
        => _entries.TryGetValue(key, out entry);

    public void Store(CalendarSessionCacheKey key, CalendarSessionCacheEntry entry)
        => _entries[key] = entry;

    public void Invalidate(CalendarSessionCacheKey key)
        => _entries.TryRemove(key, out _);

    public void InvalidateByAttachment(AttachmentId attachmentId)
    {
        foreach (var pair in _entries.Where(pair => pair.Key.AttachmentId == attachmentId).ToArray())
        {
            _entries.TryRemove(pair.Key, out _);
        }
    }

    public async Task<CacheDisposition> TryReuseAsync(
        CalendarSessionCacheKey key,
        CancellationToken cancellationToken)
    {
        if (!_entries.TryGetValue(key, out var entry))
        {
            return CacheDisposition.Refused;
        }

        var validation = await ReValidateAsync(entry, key.AttachmentId, cancellationToken);
        if (!validation)
        {
            _entries.TryRemove(key, out _);
            return CacheDisposition.Refused;
        }

        return CacheDisposition.Reused;
    }

    private async Task<bool> ReValidateAsync(CalendarSessionCacheEntry entry, AttachmentId attachmentId, CancellationToken cancellationToken)
    {
        if (!TryParseHex(entry.AnchorAddress, out var anchorAddress))
        {
            return false;
        }

        try
        {
            var probe = await _gateway.ReadAsync(
                new ReadMemoryRequest(attachmentId, anchorAddress, MemoryValueKind.Bytes, 0x254),
                cancellationToken);
            var bytes = Convert.FromHexString(probe.Value);
            var hash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes))
                .ToLowerInvariant();
            return string.Equals(hash, entry.ValidationSampleSha256, System.StringComparison.OrdinalIgnoreCase);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            return false;
        }
    }

    private static bool TryParseHex(string text, out ulong value)
    {
        if (text.StartsWith("0x", System.StringComparison.OrdinalIgnoreCase))
        {
            return ulong.TryParse(text.AsSpan(2), System.Globalization.NumberStyles.HexNumber, CultureInfo.InvariantCulture, out value);
        }

        return ulong.TryParse(text, System.Globalization.NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
    }
}
