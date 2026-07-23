using System.Buffers.Binary;
using System.Diagnostics;
using System.Runtime.InteropServices;
using TrainerStudio.Core.Projects;
using TrainerStudio.Windows.Memory;
using TrainerStudio.Windows.Processes;

return await RunAsync();

static unsafe Task<int> RunAsync()
{
    GameValues* values = null;
    PointerNode* node = null;
    nint root = 0;

    try
    {
        values = (GameValues*)NativeMemory.AllocZeroed((nuint)sizeof(GameValues));
        node = (PointerNode*)NativeMemory.AllocZeroed((nuint)sizeof(PointerNode));
        root = NativeMethods.VirtualAlloc(
            0,
            0x1000,
            NativeMethods.MemReserve | NativeMethods.MemCommit,
            NativeMethods.PageReadWrite);
        Assert(values != null && node != null && root != 0, "Could not allocate test fixture.");

        values->Credits = 2500;
        node->Values = (ulong)values;
        *(ulong*)root = (ulong)node;
        var targetAddress = (ulong)values + 8;

        using var process = Process.GetCurrentProcess();
        var module = process.MainModule
            ?? throw new InvalidOperationException("The test process has no main module.");
        var descriptor = new ProcessDescriptor(
            process.Id,
            process.ProcessName,
            module.FileName,
            module.ModuleName,
            unchecked((ulong)module.BaseAddress.ToInt64()),
            checked((ulong)module.ModuleMemorySize),
            "pointer-integration-test");

        using var memory = ProcessMemorySession.Attach(descriptor);
        var scanner = new PointerPathScanner(memory);
        var result = scanner.FindPathsAsync(
                targetAddress,
                descriptor,
                new PointerScanOptions
                {
                    MaximumDepth = 2,
                    MaximumOffset = 0x1000,
                    MaximumPaths = 200,
                    MaximumFrontierNodes = 10_000,
                    MaximumLinksPerLevel = 25_000
                },
                progress: null,
                CancellationToken.None)
            .GetAwaiter()
            .GetResult();

        var expectedPath = result.Paths.FirstOrDefault(path =>
            path.RootKind == PointerRootKind.Absolute
            && path.AbsoluteRootAddress == (ulong)root
            && path.Offsets.SequenceEqual([0x18UL, 0x08UL]));
        if (expectedPath is null)
        {
            throw new InvalidOperationException(
                $"Expected two-level path was not found among {result.Paths.Count} paths.");
        }

        var resolved = PointerPathResolver.TryResolve(
            expectedPath,
            (_, _) => null,
            memory.TryReadPointer,
            out var resolvedRoot,
            out var resolvedAddress);
        Assert(resolved, "Discovered path did not resolve.");
        Assert(resolvedRoot == (ulong)root, "Discovered root changed during resolution.");
        Assert(resolvedAddress == targetAddress, "Discovered path resolved the wrong address.");

        var replacement = new byte[sizeof(int)];
        BinaryPrimitives.WriteInt32LittleEndian(replacement, 9100);
        memory.Write(resolvedAddress, replacement);
        Assert(values->Credits == 9100, "Writing through the resolved path did not change Credits.");

        Console.WriteLine("PASS  Live two-level pointer discovery, resolution, and write");
        Console.WriteLine($"1/1 Windows pointer integration tests passed. "
            + $"{result.Paths.Count} bounded paths found.");
        return Task.FromResult(0);
    }
    catch (Exception exception)
    {
        Console.WriteLine($"FAIL  Windows pointer integration test: {exception}");
        return Task.FromResult(1);
    }
    finally
    {
        if (root != 0)
        {
            NativeMethods.VirtualFree(root, 0, NativeMethods.MemRelease);
        }

        NativeMemory.Free(node);
        NativeMemory.Free(values);
    }
}

static void Assert(bool condition, string message)
{
    if (!condition)
    {
        throw new InvalidOperationException(message);
    }
}

[StructLayout(LayoutKind.Explicit, Size = 0x20)]
internal struct PointerNode
{
    [FieldOffset(0x18)]
    public ulong Values;
}

[StructLayout(LayoutKind.Sequential)]
internal struct GameValues
{
    public int Health;
    public int Ammo;
    public int Credits;
}

internal static class NativeMethods
{
    internal const uint MemCommit = 0x1000;
    internal const uint MemReserve = 0x2000;
    internal const uint MemRelease = 0x8000;
    internal const uint PageReadWrite = 0x04;

    [DllImport("kernel32.dll", SetLastError = true)]
    internal static extern nint VirtualAlloc(
        nint address,
        nuint size,
        uint allocationType,
        uint protect);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool VirtualFree(
        nint address,
        nuint size,
        uint freeType);
}
