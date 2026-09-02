using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Overmem.Abstractions;
using Overmem.Abstractions.Memory;
using Overmem.Abstractions.Processes;
using Overmem.Extensions.Pes2021.Fixtures;
using Overmem.Runtime;

namespace Overmem.Extensions.Pes2021.Players;

/// <summary>
/// In-memory cache for PES 2021 player sessions. The cache is keyed by
/// <see cref="PlayerSessionCacheKey"/>: changing any of its parts produces a different
/// entry, so a PES restart naturally invalidates the previous session.
///
/// The cache never persists across host process restarts. <see cref="InvalidateByAttachment"/>
/// is called on detach. Reuse revalidates the cached <see cref="ValidationSampleSha256"/>
/// against the bytes at the stored anchor; mismatch invalidates and forces rediscover.
/// </summary>
public sealed class Pes2021PlayerSessionCache
{
    private readonly ConcurrentDictionary<PlayerSessionCacheKey, PlayerSessionCacheEntry> _entries = new();
    private readonly IProcessMemoryGateway _gateway;

    public Pes2021PlayerSessionCache(IProcessMemoryGateway gateway)
    {
        _gateway = gateway;
    }

    public int Count => _entries.Count;

    public bool TryGet(PlayerSessionCacheKey key, out PlayerSessionCacheEntry? entry)
        => _entries.TryGetValue(key, out entry);

    public void Store(PlayerSessionCacheKey key, PlayerSessionCacheEntry entry)
        => _entries[key] = entry;

    public void Invalidate(PlayerSessionCacheKey key)
        => _entries.TryRemove(key, out _);

    public void InvalidateByAttachment(AttachmentId attachmentId)
    {
        foreach (var pair in _entries.Where(pair => pair.Key.AttachmentId == attachmentId).ToArray())
        {
            _entries.TryRemove(pair.Key, out _);
        }
    }

    public async Task<CacheDisposition> TryReuseAsync(
        PlayerSessionCacheKey key,
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

    private async Task<bool> ReValidateAsync(PlayerSessionCacheEntry entry, AttachmentId attachmentId, CancellationToken cancellationToken)
    {
        if (!TryParseHex(entry.AnchorAddress, out var anchorAddress))
        {
            return false;
        }

        try
        {
            var probe = await _gateway.ReadAsync(
                new ReadMemoryRequest(attachmentId, anchorAddress, MemoryValueKind.Bytes, 380 * 2),
                cancellationToken);
            var bytes = Convert.FromHexString(probe.Value);
            var hash = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
            return string.Equals(hash, entry.ValidationSampleSha256, System.StringComparison.OrdinalIgnoreCase);
        }
        catch (System.OperationCanceledException)
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