using System.Buffers.Binary;
using Overmem.Abstractions.Memory;
using Overmem.Abstractions.Processes;
using Overmem.Application;

namespace Overmem.Extensions.Pes2021.Fixtures;

/// <summary>
/// Counters for a single block reader call. The caller accumulates these into
/// <see cref="ExtractionDiagnostics"/>.
/// </summary>
public sealed record CalendarBlockReadMetrics(
    int ReadCalls,
    int BlocksRead,
    ulong BytesRequested,
    ulong BytesRead,
    int PartialReads);

/// <summary>
/// Result of reading one block. Either the read succeeded with a contiguous buffer covering
/// <paramref name="RecordCount"/> records, or the read failed because the requested range
/// crossed a region boundary, exceeded <see cref="Pes2021FixtureProfile.Calendar"/> limits
/// or could not be satisfied by the underlying gateway.
/// </summary>
public sealed record CalendarRecordBlock(
    ulong BaseAddress,
    int StartRecordIndex,
    int RecordCount,
    byte[] Bytes,
    CalendarBlockReadMetrics Metrics,
    string? FailureReason);

/// <summary>
/// Narrow read-only reader for PES 2021 calendar records. It depends only on the read-only
/// surface of <see cref="ProcessMemoryApplicationService"/>; the test fake gateway fails
/// immediately if any code path inside the fixtures namespace ever calls
/// <c>WriteAsync</c>, which is the architectural guarantee behind G6.
///
/// The reader is responsible for:
/// <list type="bullet">
/// <item>respecting the <see cref="Pes2021FixtureProfile.Calendar"/> block-record limits;</item>
/// <item>clipping reads to the end of the current memory region;</item>
/// <item>issuing at least one <c>ReadAsync</c> per segment so the caller can report
/// <c>readCalls</c> accurately;</item>
/// <item>flagging <see cref="FixtureRejectionReasons.PartialRead"/> when the bytes returned
/// by the gateway are not an exact multiple of <see cref="Pes2021FixtureProfile.Stride"/>.</item>
/// </list>
/// </summary>
public static class Pes2021CalendarBlockReader
{
    /// <summary>
    /// Reads up to <paramref name="recordCount"/> consecutive calendar records starting at
    /// <paramref name="baseAddress"/> + <paramref name="startRecordIndex"/> *
    /// <see cref="Pes2021FixtureProfile.Stride"/>. The returned block either covers the
    /// full requested range, or it is clipped to the end of the memory region containing
    /// the start address. A failure produces a <see cref="CalendarRecordBlock"/> with
    /// <see cref="CalendarRecordBlock.FailureReason"/> set.
    /// </summary>
    public static async Task<CalendarRecordBlock> ReadCalendarRecordsBlockAsync(
        ProcessMemoryApplicationService memoryService,
        AttachmentId attachmentId,
        ulong baseAddress,
        int startRecordIndex,
        int recordCount,
        Pes2021FixtureProfile profile,
        IReadOnlyList<MemoryRegionInfo>? regions,
        CancellationToken cancellationToken)
    {
        if (recordCount <= 0)
        {
            return new CalendarRecordBlock(
                baseAddress,
                startRecordIndex,
                0,
                [],
                new CalendarBlockReadMetrics(0, 0, 0, 0, 0),
                FixtureRejectionReasons.ProfileConstraint);
        }

        if (startRecordIndex < 0)
        {
            return new CalendarRecordBlock(
                baseAddress,
                startRecordIndex,
                0,
                [],
                new CalendarBlockReadMetrics(0, 0, 0, 0, 0),
                FixtureRejectionReasons.ProfileConstraint);
        }

        var stride = profile.Stride;
        var offset = checked(startRecordIndex * stride);
        var startAddress = checked(baseAddress + (ulong)offset);
        var requestedBytes = checked(recordCount * stride);

        var region = FindRegionContaining(regions, startAddress);
        if (region is null)
        {
            return new CalendarRecordBlock(
                startAddress,
                startRecordIndex,
                0,
                [],
                new CalendarBlockReadMetrics(0, 0, (ulong)requestedBytes, 0, 0),
                FixtureRejectionReasons.OutsideRegion);
        }

        var regionStop = checked(region.BaseAddress + region.RegionSize);
        var availableInRegion = (long)regionStop - (long)startAddress;
        if (availableInRegion <= 0)
        {
            return new CalendarRecordBlock(
                startAddress,
                startRecordIndex,
                0,
                [],
                new CalendarBlockReadMetrics(0, 0, (ulong)requestedBytes, 0, 0),
                FixtureRejectionReasons.OutsideRegion);
        }

        var bytesToRequest = (int)Math.Min((long)requestedBytes, availableInRegion);
        var chunkSize = Math.Min(profile.RegionFilter.ChunkBytes, bytesToRequest);
        if (chunkSize <= 0)
        {
            chunkSize = bytesToRequest;
        }

        var totalBytesRead = 0;
        var readCalls = 0;
        var partialReads = 0;
        var buffer = new byte[bytesToRequest];
        var cursor = startAddress;
        var remaining = bytesToRequest;
        var failureReason = (string?)null;

        while (remaining > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var chunkBytes = Math.Min(chunkSize, remaining);
            var readResult = await ReadBytesAsync(memoryService, attachmentId, cursor, chunkBytes, cancellationToken);
            readCalls++;

            if (readResult is null)
            {
                failureReason = FixtureRejectionReasons.PartialRead;
                partialReads++;
                break;
            }

            if (readResult.Length == 0)
            {
                failureReason = FixtureRejectionReasons.PartialRead;
                partialReads++;
                break;
            }

            readResult.CopyTo(buffer.AsSpan(totalBytesRead, readResult.Length));
            totalBytesRead += readResult.Length;
            cursor = checked(cursor + (ulong)readResult.Length);
            remaining -= readResult.Length;

            if (readResult.Length < chunkBytes)
            {
                failureReason = FixtureRejectionReasons.PartialRead;
                partialReads++;
                break;
            }

            if (cursor >= regionStop)
            {
                failureReason = FixtureRejectionReasons.OutsideRegion;
                break;
            }
        }

        var completedRecords = totalBytesRead / stride;
        var trailing = totalBytesRead - (completedRecords * stride);
        if (trailing > 0)
        {
            failureReason ??= FixtureRejectionReasons.PartialRead;
            partialReads++;
        }

        var bytesRequested = (ulong)requestedBytes;
        var metrics = new CalendarBlockReadMetrics(
            readCalls,
            readCalls == 0 ? 0 : 1,
            bytesRequested,
            (ulong)totalBytesRead,
            partialReads);

        return new CalendarRecordBlock(
            startAddress,
            startRecordIndex,
            completedRecords,
            buffer.AsSpan(0, totalBytesRead).ToArray(),
            metrics,
            failureReason);
    }

    /// <summary>
    /// Convenience enumerator that drives the reader in <see cref="Pes2021FixtureProfile.Calendar"/>.<c>DefaultBlockRecords</c>-sized
    /// chunks until the requested <paramref name="recordLimit"/> is reached or the read
    /// fails. When a block ends because the cursor left the current memory region, the
    /// enumerator advances to the first record of the next region and continues, so a
    /// calendar split across two private regions is read correctly.
    /// </summary>
    public static async IAsyncEnumerable<CalendarRecordBlock> ReadCalendarRecordBlocksAsync(
        ProcessMemoryApplicationService memoryService,
        AttachmentId attachmentId,
        ulong baseAddress,
        int recordLimit,
        Pes2021FixtureProfile profile,
        IReadOnlyList<MemoryRegionInfo>? regions,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var defaultBlock = profile.Calendar.DefaultBlockRecords;
        var maxBlock = profile.Calendar.MaxBlockRecords;
        var blockSize = Math.Min(defaultBlock, maxBlock);
        var stride = profile.Stride;

        var cursorIndex = 0;
        while (cursorIndex < recordLimit)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var cursorAddress = checked(baseAddress + (ulong)cursorIndex * (ulong)stride);
            var region = FindRegionContaining(regions, cursorAddress);
            if (region is null)
            {
                var advance = TryAdvanceToNextRecordCursor(baseAddress, cursorIndex, stride, regions);
                if (advance is null)
                {
                    yield break;
                }

                cursorIndex = advance.Value;
                continue;
            }

            var remaining = recordLimit - cursorIndex;
            var count = Math.Min(blockSize, remaining);
            var block = await ReadCalendarRecordsBlockAsync(
                memoryService,
                attachmentId,
                baseAddress,
                cursorIndex,
                count,
                profile,
                regions,
                cancellationToken);

            yield return block;

            if (block.FailureReason is FixtureRejectionReasons.OutsideRegion)
            {
                var advance = TryAdvanceToNextRecordCursor(baseAddress, cursorIndex, stride, regions);
                if (advance is null)
                {
                    yield break;
                }

                cursorIndex = advance.Value;
                continue;
            }

            if (block.FailureReason is not null)
            {
                yield break;
            }

            if (block.RecordCount == 0)
            {
                yield break;
            }

            cursorIndex += block.RecordCount;
        }
    }

    /// <summary>
    /// Returns the record index that corresponds to the first byte of the next memory
    /// region after the current cursor, or <c>null</c> when no such region exists.
    /// </summary>
    private static int? TryAdvanceToNextRecordCursor(
        ulong baseAddress,
        int cursorIndex,
        int stride,
        IReadOnlyList<MemoryRegionInfo>? regions)
    {
        if (regions is null || regions.Count == 0)
        {
            return null;
        }

        var currentAddress = checked(baseAddress + (ulong)cursorIndex * (ulong)stride);
        MemoryRegionInfo? next = null;
        for (var index = 0; index < regions.Count; index++)
        {
            var region = regions[index];
            if (region.BaseAddress <= currentAddress)
            {
                continue;
            }

            if (next is null || region.BaseAddress < next.BaseAddress)
            {
                next = region;
            }
        }

        if (next is null)
        {
            return null;
        }

        if (next.BaseAddress < baseAddress)
        {
            return null;
        }

        var delta = next.BaseAddress - baseAddress;
        if (delta % (ulong)stride != 0)
        {
            return null;
        }

        var nextIndex = (int)(delta / (ulong)stride);
        return nextIndex;
    }

    private static async Task<byte[]?> ReadBytesAsync(
        ProcessMemoryApplicationService memoryService,
        AttachmentId attachmentId,
        ulong address,
        int size,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await memoryService.ReadAsync(
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

    private static MemoryRegionInfo? FindRegionContaining(IReadOnlyList<MemoryRegionInfo>? regions, ulong address)
    {
        if (regions is null)
        {
            return null;
        }

        for (var index = 0; index < regions.Count; index++)
        {
            var region = regions[index];
            if (address < region.BaseAddress)
            {
                continue;
            }

            var regionStop = checked(region.BaseAddress + region.RegionSize);
            if (address < regionStop)
            {
                return region;
            }
        }

        return null;
    }
}
