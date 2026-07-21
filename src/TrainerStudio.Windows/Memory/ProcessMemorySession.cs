using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
using TrainerStudio.Windows.Interop;
using TrainerStudio.Windows.Processes;

namespace TrainerStudio.Windows.Memory;

public sealed class ProcessMemorySession : IDisposable
{
    private const ulong HighestUserAddress = 0x00007FFFFFFFFFFF;
    private readonly SafeProcessHandle handle;

    private ProcessMemorySession(ProcessDescriptor process, SafeProcessHandle handle)
    {
        Process = process;
        this.handle = handle;
    }

    public ProcessDescriptor Process { get; }
    public bool IsClosed => handle.IsClosed || handle.IsInvalid;

    public static ProcessMemorySession Attach(ProcessDescriptor process)
    {
        var access = NativeMethods.ProcessQueryLimitedInformation
            | NativeMethods.ProcessVmOperation
            | NativeMethods.ProcessVmRead
            | NativeMethods.ProcessVmWrite;
        var handle = NativeMethods.OpenProcess(access, false, checked((uint)process.Id));
        if (handle.IsInvalid)
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(),
                $"Windows refused access to {process.DisplayName}.");
        }

        if (!NativeMethods.IsWow64Process2(handle, out var processMachine, out var nativeMachine))
        {
            var error = Marshal.GetLastWin32Error();
            handle.Dispose();
            throw new Win32Exception(error, "Could not determine the target architecture.");
        }

        var isX64 = nativeMachine == NativeMethods.ImageFileMachineAmd64
            && (processMachine == NativeMethods.ImageFileMachineUnknown
                || processMachine == NativeMethods.ImageFileMachineAmd64);
        if (!isX64)
        {
            handle.Dispose();
            throw new NotSupportedException("This milestone supports x64 targets only.");
        }

        return new ProcessMemorySession(process, handle);
    }

    internal IEnumerable<MemoryRegion> EnumerateReadableRegions()
    {
        ulong address = 0;
        var structureSize = (nuint)Marshal.SizeOf<MemoryBasicInformation64>();

        while (address < HighestUserAddress)
        {
            var result = NativeMethods.VirtualQueryEx(handle, unchecked((nint)address),
                out var information, structureSize);
            if (result == 0)
            {
                yield break;
            }

            var next = information.BaseAddress + information.RegionSize;
            if (information.State == NativeMethods.MemCommit
                && (information.Protect & NativeMethods.PageNoAccess) == 0
                && (information.Protect & NativeMethods.PageGuard) == 0
                && information.RegionSize > 0)
            {
                yield return new MemoryRegion(information.BaseAddress, information.RegionSize);
            }

            if (next <= address)
            {
                yield break;
            }

            address = next;
        }
    }

    internal int Read(ulong address, byte[] buffer, int requestedCount)
    {
        if (requestedCount < 0 || requestedCount > buffer.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(requestedCount));
        }

        var success = NativeMethods.ReadProcessMemory(handle, unchecked((nint)address),
            buffer, checked((nuint)requestedCount), out var bytesRead);
        if (!success && bytesRead == 0)
        {
            return 0;
        }

        return checked((int)bytesRead);
    }

    public byte[]? TryRead(ulong address, int count)
    {
        var buffer = new byte[count];
        return Read(address, buffer, count) == count ? buffer : null;
    }

    public void Write(ulong address, byte[] value)
    {
        if (!NativeMethods.WriteProcessMemory(handle, unchecked((nint)address), value,
                checked((nuint)value.Length), out var written)
            || written != checked((nuint)value.Length))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(),
                $"Windows could not write {value.Length} bytes at 0x{address:X16}.");
        }
    }

    public void Dispose() => handle.Dispose();
}
