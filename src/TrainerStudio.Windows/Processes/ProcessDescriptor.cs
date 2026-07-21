using TrainerStudio.Core.Projects;

namespace TrainerStudio.Windows.Processes;

public sealed record ProcessDescriptor(
    int Id,
    string Name,
    string? FilePath,
    string MainModuleName,
    ulong MainModuleBaseAddress,
    ulong MainModuleSize,
    string ExecutableIdentity)
{
    public string DisplayName => $"{Name}  ·  PID {Id}";

    public bool TryGetMainModuleOffset(ulong address, out ulong offset)
        => ModuleAddressMath.TryGetOffset(MainModuleBaseAddress, MainModuleSize, address,
            out offset);

    public bool TryResolveMainModuleOffset(ulong offset, out ulong address)
        => ModuleAddressMath.TryResolve(MainModuleBaseAddress, MainModuleSize, offset,
            out address);
}
