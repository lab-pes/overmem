using Microsoft.Win32.SafeHandles;
using System.Runtime.InteropServices;

namespace Overmem.Windows.Interop;

internal sealed class SafeProcessHandle : SafeHandleZeroOrMinusOneIsInvalid
{
    private SafeProcessHandle()
        : base(ownsHandle: true)
    {
    }

    protected override bool ReleaseHandle() => Kernel32Native.CloseHandle(handle);
}