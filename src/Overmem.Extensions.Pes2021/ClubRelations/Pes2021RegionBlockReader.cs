using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Overmem.Abstractions.Memory;
using Overmem.Abstractions.Processes;
using Overmem.Application;

namespace Overmem.Extensions.Pes2021.ClubRelations;

public sealed record RegionBlockRead(
    ulong RegionBaseAddress,
    ulong BlockOffset,
    int BlockBytes,
    string Sha256,
    int BytesRead,
    byte[] Payload);

public sealed class Pes2021RegionBlockReader
{
    public const int DefaultOverlapBytes = 16;
    public const int MaxBlockBytes = 4 * 1024 * 1024;
    public const int MinBlockBytes = 64 * 1024;
    public const int DefaultMaxBytesPerRegion = 8 * 1024 * 1024;

    private readonly ProcessMemoryApplicationService _memoryService;

    public Pes2021RegionBlockReader(ProcessMemoryApplicationService memoryService)
    {
        _memoryService = memoryService ?? throw new ArgumentNullException(nameof(memoryService));
    }

    public async Task<IReadOnlyList<RegionBlockRead>> ReadRegionBlocksAsync(
        AttachmentId attachmentId,
        MemoryRegionInfo region,
        int blockBytes,
        int overlapBytes,
        int maxBytesPerRegion,
        CancellationToken cancellationToken)
    {
        if (region.RegionSize == 0)
        {
            return Array.Empty<RegionBlockRead>();
        }

        if (blockBytes < MinBlockBytes)
        {
            blockBytes = MinBlockBytes;
        }

        if (blockBytes > MaxBlockBytes)
        {
            blockBytes = MaxBlockBytes;
        }

        if (overlapBytes < 0)
        {
            overlapBytes = 0;
        }

        if (overlapBytes >= blockBytes)
        {
            overlapBytes = Math.Max(0, blockBytes / 4);
        }

        if (maxBytesPerRegion <= 0)
        {
            maxBytesPerRegion = DefaultMaxBytesPerRegion;
        }

        var stride = blockBytes - overlapBytes;
        if (stride <= 0)
        {
            stride = blockBytes;
        }

        var blocks = new List<RegionBlockRead>();
        var regionStop = region.BaseAddress + region.RegionSize;
        var readBudget = (long)Math.Min((ulong)maxBytesPerRegion, region.RegionSize);
        var totalRead = 0L;
        var cursor = region.BaseAddress;
        var blockIndex = 0;
        while (cursor < regionStop && totalRead < readBudget)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var remainingRegion = (long)(regionStop - cursor);
            var remainingBudget = readBudget - totalRead;
            var requested = (int)Math.Min(blockBytes, Math.Min(remainingRegion, remainingBudget));
            if (requested <= 0)
            {
                break;
            }

            var bytes = await ReadBytesAsync(attachmentId, cursor, requested, cancellationToken);
            if (bytes is null || bytes.Length == 0)
            {
                break;
            }

            var hash = ComputeSha256(bytes);
            blocks.Add(new RegionBlockRead(region.BaseAddress, cursor - region.BaseAddress, bytes.Length, hash, bytes.Length, bytes));

            totalRead += bytes.Length;
            cursor += (ulong)stride;
            blockIndex++;

            if (bytes.Length < requested)
            {
                break;
            }
        }

        return blocks;
    }

    private async Task<byte[]?> ReadBytesAsync(AttachmentId attachmentId, ulong address, int size, CancellationToken cancellationToken)
    {
        try
        {
            var result = await _memoryService.ReadAsync(
                new ReadMemoryRequest(attachmentId, address, MemoryValueKind.Bytes, size),
                cancellationToken);
            return Convert.FromHexString(result.Value);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            return null;
        }
    }

    private static string ComputeSha256(byte[] data)
    {
        using var sha = SHA256.Create();
        var hash = sha.ComputeHash(data);
        var builder = new StringBuilder(hash.Length * 2);
        foreach (var b in hash)
        {
            builder.Append(b.ToString("x2", CultureInfo.InvariantCulture));
        }

        return builder.ToString();
    }
}
