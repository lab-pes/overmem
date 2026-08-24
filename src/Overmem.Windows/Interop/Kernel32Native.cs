using System.Runtime.InteropServices;

namespace Overmem.Windows.Interop;

internal static partial class Kernel32Native
{
    [Flags]
    internal enum MemoryProtection : uint
    {
        NoAccess = 0x01,
        ReadOnly = 0x02,
        ReadWrite = 0x04,
        WriteCopy = 0x08,
        Execute = 0x10,
        ExecuteRead = 0x20,
        ExecuteReadWrite = 0x40,
        ExecuteWriteCopy = 0x80,
        Guard = 0x100,
        NoCache = 0x200,
        WriteCombine = 0x400,
    }

    internal enum MemoryState : uint
    {
        Commit = 0x1000,
        Reserve = 0x2000,
        Free = 0x10000,
    }

    internal enum MemoryType : uint
    {
        Private = 0x20000,
        Mapped = 0x40000,
        Image = 0x1000000,
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct MemoryBasicInformation
    {
        internal nuint BaseAddress;
        internal nuint AllocationBase;
        internal MemoryProtection AllocationProtect;
        internal nuint RegionSize;
        internal MemoryState State;
        internal MemoryProtection Protect;
        internal MemoryType Type;
    }

    [Flags]
    internal enum ProcessAccessRights : uint
    {
        QueryInformation = 0x0400,
        VirtualMemoryOperation = 0x0008,
        VirtualMemoryRead = 0x0010,
        VirtualMemoryWrite = 0x0020,
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    internal static extern SafeProcessHandle OpenProcess(ProcessAccessRights desiredAccess, [MarshalAs(UnmanagedType.Bool)] bool inheritHandle, int processId);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool ReadProcessMemory(SafeProcessHandle processHandle, nuint baseAddress, byte[] buffer, int size, out nuint numberOfBytesRead);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool WriteProcessMemory(SafeProcessHandle processHandle, nuint baseAddress, byte[] buffer, int size, out nuint numberOfBytesWritten);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool IsWow64Process(SafeProcessHandle processHandle, [MarshalAs(UnmanagedType.Bool)] out bool wow64Process);

    [DllImport("kernel32.dll", SetLastError = true)]
    internal static extern nuint VirtualQueryEx(SafeProcessHandle processHandle, nuint address, out MemoryBasicInformation buffer, nuint length);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool CloseHandle(nint handle);
}