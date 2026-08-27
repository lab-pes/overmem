using Overmem.Abstractions;
using Overmem.Abstractions.Memory;
using Overmem.Abstractions.Processes;
using Overmem.Windows.Interop;
using Overmem.Windows.Memory;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Overmem.Windows.Processes;

public sealed class WindowsProcessMemoryGateway : IProcessMemoryGateway, IDisposable
{
    private readonly ConcurrentDictionary<AttachmentId, AttachedProcess> _attachments = new();

    public Task<AttachmentInfo> AttachAsync(ProcessSelector selector, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        EnsureWindows();

        var process = ResolveProcess(selector);
        var handle = Kernel32Native.OpenProcess(
            Kernel32Native.ProcessAccessRights.QueryInformation |
            Kernel32Native.ProcessAccessRights.VirtualMemoryOperation |
            Kernel32Native.ProcessAccessRights.VirtualMemoryRead |
            Kernel32Native.ProcessAccessRights.VirtualMemoryWrite,
            inheritHandle: false,
            process.Id);

        if (handle.IsInvalid)
        {
            throw new InvalidOperationException($"Failed to open process {process.Id}. Win32={Marshal.GetLastWin32Error()}.");
        }

        var attachment = new AttachmentInfo(
            AttachmentId.New(),
            process.Id,
            process.ProcessName,
            GetArchitecture(handle),
            TryGetStartTimeUtc(process));

        var attachedProcess = new AttachedProcess(process, handle, attachment);
        if (!_attachments.TryAdd(attachment.AttachmentId, attachedProcess))
        {
            attachedProcess.Dispose();
            throw new InvalidOperationException("Failed to register the attached process.");
        }

        return Task.FromResult(attachment);
    }

    public Task DetachAsync(AttachmentId attachmentId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (_attachments.TryRemove(attachmentId, out var attachedProcess))
        {
            attachedProcess.Dispose();
        }

        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<ModuleInfo>> ListModulesAsync(AttachmentId attachmentId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var attachedProcess = GetAttachedProcess(attachmentId);
        var modules = attachedProcess.Process.Modules
            .Cast<ProcessModule>()
            .Select(module => new ModuleInfo(
                module.ModuleName,
                unchecked((ulong)module.BaseAddress.ToInt64()),
                module.ModuleMemorySize))
            .ToArray();

        return Task.FromResult<IReadOnlyList<ModuleInfo>>(modules);
    }

    public Task<IReadOnlyList<MemoryRegionInfo>> ListRegionsAsync(AttachmentId attachmentId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var attachedProcess = GetAttachedProcess(attachmentId);
        var regions = new List<MemoryRegionInfo>();
        var address = 0UL;
        var limit = GetMaximumUserAddress(attachedProcess.Info.Architecture);
        var infoSize = checked((nuint)Marshal.SizeOf<Kernel32Native.MemoryBasicInformation>());

        while (address < limit)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var result = Kernel32Native.VirtualQueryEx(attachedProcess.Handle, checked((nuint)address), out var mbi, infoSize);
            if (result == 0)
            {
                break;
            }

            regions.Add(ToRegionInfo(mbi));

            var nextAddress = checked((ulong)mbi.BaseAddress + (ulong)mbi.RegionSize);
            if (nextAddress <= address)
            {
                break;
            }

            address = nextAddress;
        }

        return Task.FromResult<IReadOnlyList<MemoryRegionInfo>>(regions);
    }

    public Task<ResolvePointerResult> ResolvePointerAsync(ResolvePointerRequest request, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var attachedProcess = GetAttachedProcess(request.AttachmentId);
        var pointerSize = GetPointerSize(attachedProcess.Info.Architecture);
        ulong currentAddress = request.BaseAddress;

        foreach (var offset in request.Offsets)
        {
            var buffer = ReadBytes(attachedProcess, currentAddress, pointerSize);
            currentAddress = pointerSize == sizeof(uint)
                ? BitConverter.ToUInt32(buffer, 0)
                : BitConverter.ToUInt64(buffer, 0);

            currentAddress = checked((ulong)((long)currentAddress + offset));
        }

        return Task.FromResult(new ResolvePointerResult(request.BaseAddress, request.Offsets, currentAddress));
    }

    public Task<ResolvePointerResult> ResolveModulePointerAsync(ResolveModulePointerRequest request, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var module = GetModule(request.AttachmentId, request.ModuleName);
        var absoluteBaseAddress = AddSignedOffset(module.BaseAddress, request.BaseOffset);
        return ResolvePointerAsync(new ResolvePointerRequest(request.AttachmentId, absoluteBaseAddress, request.Offsets), cancellationToken);
    }

    public Task<PatternScanResult> ScanPatternAsync(PatternScanRequest request, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var attachedProcess = GetAttachedProcess(request.AttachmentId);
        var pattern = PatternScanner.Parse(request.Pattern);
        var searchRegions = GetSearchRegions(attachedProcess, request.ModuleName);
        var addresses = new List<ulong>();

        foreach (var region in searchRegions)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!region.IsReadable || region.RegionSize < (ulong)pattern.Bytes.Length)
            {
                continue;
            }

            var readSize = region.RegionSize > int.MaxValue ? int.MaxValue : (int)region.RegionSize;
            byte[] buffer;
            try
            {
                buffer = ReadBytes(attachedProcess, region.BaseAddress, readSize);
            }
            catch (InvalidOperationException)
            {
                continue;
            }

            var remaining = request.MaxResults - addresses.Count;
            if (remaining <= 0)
            {
                break;
            }

            addresses.AddRange(PatternScanner.FindMatches(buffer, region.BaseAddress, pattern, remaining));
            if (addresses.Count >= request.MaxResults)
            {
                break;
            }
        }

        return Task.FromResult(new PatternScanResult(request.Pattern, request.ModuleName, addresses));
    }

    public Task<ReadMemoryResult> ReadAsync(ReadMemoryRequest request, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var attachedProcess = GetAttachedProcess(request.AttachmentId);
        var size = MemoryValueCodec.ResolveByteCount(request.ValueKind, request.Size);
        var actualBuffer = ReadBytes(attachedProcess, request.Address, size);
        var value = MemoryValueCodec.FormatValue(request.ValueKind, actualBuffer);
        return Task.FromResult(new ReadMemoryResult(request.Address, request.ValueKind, value, actualBuffer.Length));
    }

    public Task<WriteMemoryResult> WriteAsync(WriteMemoryRequest request, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var attachedProcess = GetAttachedProcess(request.AttachmentId);
        var buffer = MemoryValueCodec.ParseValue(request.ValueKind, request.Value, request.Size);
        if (!Kernel32Native.WriteProcessMemory(attachedProcess.Handle, checked((nuint)request.Address), buffer, buffer.Length, out var bytesWritten))
        {
            throw new InvalidOperationException($"Failed to write memory at 0x{request.Address:X}. Win32={Marshal.GetLastWin32Error()}.");
        }

        return Task.FromResult(new WriteMemoryResult(request.Address, request.ValueKind, checked((int)bytesWritten)));
    }

    public void Dispose()
    {
        foreach (var attachment in _attachments.Values)
        {
            attachment.Dispose();
        }

        _attachments.Clear();
    }

    private static void EnsureWindows()
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("Overmem currently supports Windows only.");
        }
    }

    private static Process ResolveProcess(ProcessSelector selector)
    {
        if (selector.ProcessId is > 0)
        {
            return Process.GetProcessById(selector.ProcessId.Value);
        }

        var candidates = Process.GetProcessesByName(selector.ProcessName!);
        return candidates.FirstOrDefault()
            ?? throw new InvalidOperationException($"Process '{selector.ProcessName}' was not found.");
    }

    private static DateTimeOffset? TryGetStartTimeUtc(Process process)
    {
        try
        {
            return process.StartTime.ToUniversalTime();
        }
        catch
        {
            return null;
        }
    }

    private static ProcessArchitecture GetArchitecture(SafeProcessHandle handle)
    {
        if (!Environment.Is64BitOperatingSystem)
        {
            return ProcessArchitecture.X86;
        }

        if (!Kernel32Native.IsWow64Process(handle, out var isWow64))
        {
            return ProcessArchitecture.Unknown;
        }

        return isWow64 ? ProcessArchitecture.X86 : ProcessArchitecture.X64;
    }

    private AttachedProcess GetAttachedProcess(AttachmentId attachmentId)
    {
        if (_attachments.TryGetValue(attachmentId, out var attachedProcess))
        {
            return attachedProcess;
        }

        throw new KeyNotFoundException($"Attachment '{attachmentId}' was not found.");
    }

    private ModuleInfo GetModule(AttachmentId attachmentId, string moduleName)
    {
        var attachedProcess = GetAttachedProcess(attachmentId);
        return attachedProcess.Process.Modules
            .Cast<ProcessModule>()
            .Select(module => new ModuleInfo(
                module.ModuleName,
                unchecked((ulong)module.BaseAddress.ToInt64()),
                module.ModuleMemorySize))
            .FirstOrDefault(module => string.Equals(module.Name, moduleName, StringComparison.OrdinalIgnoreCase))
            ?? throw new KeyNotFoundException($"Module '{moduleName}' was not found.");
    }

    private IReadOnlyList<MemoryRegionInfo> GetSearchRegions(AttachedProcess attachedProcess, string? moduleName)
    {
        if (string.IsNullOrWhiteSpace(moduleName))
        {
            return ListRegionsAsync(attachedProcess.Info.AttachmentId).GetAwaiter().GetResult();
        }

        var module = GetModule(attachedProcess.Info.AttachmentId, moduleName);
        var moduleEnd = checked(module.BaseAddress + (ulong)module.Size);

        return ListRegionsAsync(attachedProcess.Info.AttachmentId).GetAwaiter().GetResult()
            .Where(region =>
                region.BaseAddress < moduleEnd &&
                checked(region.BaseAddress + region.RegionSize) > module.BaseAddress)
            .ToArray();
    }

    private static byte[] ReadBytes(AttachedProcess attachedProcess, ulong address, int size)
    {
        var buffer = new byte[size];
        if (!Kernel32Native.ReadProcessMemory(attachedProcess.Handle, checked((nuint)address), buffer, buffer.Length, out var bytesRead))
        {
            throw new InvalidOperationException($"Failed to read memory at 0x{address:X}. Win32={Marshal.GetLastWin32Error()}.");
        }

        return buffer.Take(checked((int)bytesRead)).ToArray();
    }

    private static int GetPointerSize(ProcessArchitecture architecture) => architecture switch
    {
        ProcessArchitecture.X86 => sizeof(uint),
        ProcessArchitecture.X64 => sizeof(ulong),
        _ => throw new InvalidOperationException("The target process architecture is unknown."),
    };

    private static ulong GetMaximumUserAddress(ProcessArchitecture architecture) => architecture switch
    {
        ProcessArchitecture.X86 => uint.MaxValue,
        ProcessArchitecture.X64 => 0x0000_7FFF_FFFF_FFFF,
        _ => ulong.MaxValue,
    };

    private static ulong AddSignedOffset(ulong address, long offset)
    {
        if (offset >= 0)
        {
            return checked(address + (ulong)offset);
        }

        return checked(address - (ulong)(-offset));
    }

    private static MemoryRegionInfo ToRegionInfo(Kernel32Native.MemoryBasicInformation mbi)
    {
        var protection = mbi.Protect;
        var effectiveProtection = protection & ~Kernel32Native.MemoryProtection.Guard & ~Kernel32Native.MemoryProtection.NoCache & ~Kernel32Native.MemoryProtection.WriteCombine;
        var isReadable = effectiveProtection is Kernel32Native.MemoryProtection.ReadOnly
            or Kernel32Native.MemoryProtection.ReadWrite
            or Kernel32Native.MemoryProtection.WriteCopy
            or Kernel32Native.MemoryProtection.ExecuteRead
            or Kernel32Native.MemoryProtection.ExecuteReadWrite
            or Kernel32Native.MemoryProtection.ExecuteWriteCopy;
        var isWritable = effectiveProtection is Kernel32Native.MemoryProtection.ReadWrite
            or Kernel32Native.MemoryProtection.WriteCopy
            or Kernel32Native.MemoryProtection.ExecuteReadWrite
            or Kernel32Native.MemoryProtection.ExecuteWriteCopy;
        var isExecutable = effectiveProtection is Kernel32Native.MemoryProtection.Execute
            or Kernel32Native.MemoryProtection.ExecuteRead
            or Kernel32Native.MemoryProtection.ExecuteReadWrite
            or Kernel32Native.MemoryProtection.ExecuteWriteCopy;

        return new MemoryRegionInfo(
            (ulong)mbi.BaseAddress,
            (ulong)mbi.RegionSize,
            mbi.State.ToString(),
            protection.ToString(),
            mbi.Type.ToString(),
            isReadable,
            isWritable,
            isExecutable);
    }

    private sealed class AttachedProcess(Process process, SafeProcessHandle handle, AttachmentInfo info) : IDisposable
    {
        public Process Process { get; } = process;

        public SafeProcessHandle Handle { get; } = handle;

        public AttachmentInfo Info { get; } = info;

        public void Dispose()
        {
            Handle.Dispose();
            Process.Dispose();
        }
    }
}