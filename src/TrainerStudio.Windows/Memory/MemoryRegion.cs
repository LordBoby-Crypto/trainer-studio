namespace TrainerStudio.Windows.Memory;

internal sealed record MemoryRegion(
    ulong BaseAddress,
    ulong AllocationBase,
    ulong Size,
    uint Protection,
    uint Type);
