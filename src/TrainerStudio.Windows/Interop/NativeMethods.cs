using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace TrainerStudio.Windows.Interop;

internal static partial class NativeMethods
{
    internal const uint ProcessVmOperation = 0x0008;
    internal const uint ProcessVmRead = 0x0010;
    internal const uint ProcessVmWrite = 0x0020;
    internal const uint ProcessQueryLimitedInformation = 0x1000;

    internal const uint MemCommit = 0x1000;
    internal const uint PageNoAccess = 0x01;
    internal const uint PageGuard = 0x100;
    internal const ushort ImageFileMachineUnknown = 0;
    internal const ushort ImageFileMachineAmd64 = 0x8664;

    [LibraryImport("kernel32.dll", SetLastError = true)]
    internal static partial SafeProcessHandle OpenProcess(
        uint desiredAccess,
        [MarshalAs(UnmanagedType.Bool)] bool inheritHandle,
        uint processId);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool ReadProcessMemory(
        SafeProcessHandle process,
        nint baseAddress,
        [Out] byte[] buffer,
        nuint size,
        out nuint bytesRead);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool WriteProcessMemory(
        SafeProcessHandle process,
        nint baseAddress,
        byte[] buffer,
        nuint size,
        out nuint bytesWritten);

    [LibraryImport("kernel32.dll")]
    internal static partial nuint VirtualQueryEx(
        SafeProcessHandle process,
        nint address,
        out MemoryBasicInformation64 information,
        nuint length);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool IsWow64Process2(
        SafeProcessHandle process,
        out ushort processMachine,
        out ushort nativeMachine);
}

[StructLayout(LayoutKind.Sequential)]
internal struct MemoryBasicInformation64
{
    internal ulong BaseAddress;
    internal ulong AllocationBase;
    internal uint AllocationProtect;
    public uint Alignment1;
    internal ulong RegionSize;
    internal uint State;
    internal uint Protect;
    internal uint Type;
    public uint Alignment2;
}
