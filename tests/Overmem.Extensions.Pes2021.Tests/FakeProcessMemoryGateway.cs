using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Overmem.Abstractions;
using Overmem.Abstractions.Memory;
using Overmem.Abstractions.Processes;
using Overmem.Runtime;

namespace Overmem.Extensions.Pes2021.Tests;

/// <summary>
/// In-memory process gateway used by player-memory tests. Stores a byte buffer per
/// address and serves reads/writes against it. The implementation is single-threaded
/// per call but tests should not depend on parallel access.
/// </summary>
public sealed class FakeProcessMemoryGateway : IProcessMemoryGateway
{
    private readonly SortedDictionary<ulong, byte[]> _segments = new();
    public List<(ulong Address, int Size)> Reads { get; } = new();
    public List<(ulong Address, byte[] Value)> Writes { get; } = new();

    public void MapRegion(ulong baseAddress, byte[] bytes) => _segments[baseAddress] = bytes;

    public void ClearReads() => Reads.Clear();

    public Task<AttachmentInfo> AttachAsync(ProcessSelector selector, CancellationToken cancellationToken = default)
        => Task.FromResult(new AttachmentInfo(AttachmentId.New(), 1234, "TestProcess",
            ProcessArchitecture.X64, System.DateTimeOffset.UtcNow));

    public Task DetachAsync(AttachmentId attachmentId, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task<IReadOnlyList<ModuleInfo>> ListModulesAsync(AttachmentId attachmentId, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<ModuleInfo>>(System.Array.Empty<ModuleInfo>());

    public Task<IReadOnlyList<MemoryRegionInfo>> ListRegionsAsync(AttachmentId attachmentId, CancellationToken cancellationToken = default)
    {
        var list = new List<MemoryRegionInfo>();
        foreach (var segment in _segments)
        {
            list.Add(new MemoryRegionInfo(
                BaseAddress: segment.Key,
                RegionSize: (ulong)segment.Value.Length,
                State: "Commit",
                Protection: "RW",
                Type: "Private",
                IsReadable: true,
                IsWritable: true,
                IsExecutable: false));
        }
        return Task.FromResult<IReadOnlyList<MemoryRegionInfo>>(list);
    }

    public Task<ResolvePointerResult> ResolvePointerAsync(ResolvePointerRequest request, CancellationToken cancellationToken = default)
        => throw new System.NotSupportedException();

    public Task<ResolvePointerResult> ResolveModulePointerAsync(ResolveModulePointerRequest request, CancellationToken cancellationToken = default)
        => throw new System.NotSupportedException();

    public Task<PatternScanResult> ScanPatternAsync(PatternScanRequest request, CancellationToken cancellationToken = default)
        => throw new System.NotSupportedException();

    public Task<ReadMemoryResult> ReadAsync(ReadMemoryRequest request, CancellationToken cancellationToken = default)
    {
        Reads.Add((request.Address, request.Size));
        var bytes = SliceBytes(request.Address, request.Size);
        return Task.FromResult(new ReadMemoryResult(request.Address, request.ValueKind, Convert.ToHexString(bytes), bytes.Length));
    }

    public Task<WriteMemoryResult> WriteAsync(WriteMemoryRequest request, CancellationToken cancellationToken = default)
    {
        var bytes = Convert.FromHexString(request.Value);
        Writes.Add((request.Address, bytes));
        var target = FindSegmentFor(request.Address);
        if (target is null) return Task.FromResult(new WriteMemoryResult(request.Address, request.ValueKind, 0));

        var offset = (int)(request.Address - target.Value.Key);
        for (var i = 0; i < bytes.Length; i++)
        {
            if (offset + i < target.Value.Value.Length)
            {
                target.Value.Value[offset + i] = bytes[i];
            }
        }

        return Task.FromResult(new WriteMemoryResult(request.Address, request.ValueKind, bytes.Length));
    }

    private byte[] SliceBytes(ulong address, int size)
    {
        foreach (var segment in _segments)
        {
            var segStart = segment.Key;
            var segStop = checked(segStart + (ulong)segment.Value.Length);
            if (address >= segStart && checked(address + (ulong)size) <= segStop)
            {
                var offset = (int)(address - segStart);
                return segment.Value[offset..(offset + size)];
            }
        }

        return new byte[size];
    }

    private KeyValuePair<ulong, byte[]>? FindSegmentFor(ulong address)
    {
        foreach (var segment in _segments)
        {
            var segStart = segment.Key;
            var segStop = checked(segStart + (ulong)segment.Value.Length);
            if (address >= segStart && address < segStop) return segment;
        }

        return null;
    }
}

/// <summary>
/// Deterministic system clock used by player-memory tests.
/// </summary>
public sealed class FakeSystemClock : ISystemClock
{
    public System.DateTimeOffset UtcNow { get; set; } = System.DateTimeOffset.UtcNow;
}