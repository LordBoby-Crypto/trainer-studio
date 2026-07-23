namespace TrainerStudio.Core.Projects;

public sealed class PointerPath
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public PointerRootKind RootKind { get; set; }
    public string ModuleName { get; set; } = string.Empty;
    public ulong RootOffset { get; set; }
    public ulong AbsoluteRootAddress { get; set; }
    public int PointerSize { get; set; } = 8;
    public List<ulong> Offsets { get; set; } = [];
    public DateTimeOffset FoundUtc { get; set; } = DateTimeOffset.UtcNow;
    public List<PointerPathValidation> Validations { get; set; } = [];
}

public sealed class PointerPathValidation
{
    public DateTimeOffset TestedUtc { get; set; } = DateTimeOffset.UtcNow;
    public Guid AttachmentSessionId { get; set; }
    public string ExecutableIdentity { get; set; } = string.Empty;
    public ulong RootAddress { get; set; }
    public ulong ResolvedAddress { get; set; }
    public bool Resolved { get; set; }
}

public enum PointerRootKind
{
    MainModuleRelative,
    Absolute
}

public static class PointerPathResolver
{
    public static bool TryResolve(
        PointerPath path,
        Func<string, ulong, ulong?> resolveModuleRoot,
        Func<ulong, ulong?> readPointer,
        out ulong rootAddress,
        out ulong resolvedAddress)
    {
        ArgumentNullException.ThrowIfNull(path);
        ArgumentNullException.ThrowIfNull(resolveModuleRoot);
        ArgumentNullException.ThrowIfNull(readPointer);

        rootAddress = 0;
        resolvedAddress = 0;
        if (path.PointerSize != 8 || path.Offsets is null || path.Offsets.Count == 0)
        {
            return false;
        }

        ulong? root = path.RootKind switch
        {
            PointerRootKind.MainModuleRelative
                => resolveModuleRoot(path.ModuleName, path.RootOffset),
            PointerRootKind.Absolute => path.AbsoluteRootAddress,
            _ => null
        };
        if (root is null)
        {
            return false;
        }

        rootAddress = root.Value;
        var pointerAddress = root.Value;
        for (var index = 0; index < path.Offsets.Count; index++)
        {
            var pointer = readPointer(pointerAddress);
            if (pointer is null
                || ulong.MaxValue - pointer.Value < path.Offsets[index])
            {
                return false;
            }

            var nextAddress = pointer.Value + path.Offsets[index];
            if (index == path.Offsets.Count - 1)
            {
                resolvedAddress = nextAddress;
                return true;
            }

            pointerAddress = nextAddress;
        }

        return false;
    }

    public static string GetIdentity(PointerPath path)
    {
        ArgumentNullException.ThrowIfNull(path);
        var root = path.RootKind == PointerRootKind.MainModuleRelative
            ? $"module:{path.ModuleName}:{path.RootOffset:X}"
            : $"absolute:{path.AbsoluteRootAddress:X16}";
        return $"{root}|{string.Join(",", path.Offsets.Select(offset => offset.ToString("X")))}";
    }
}
