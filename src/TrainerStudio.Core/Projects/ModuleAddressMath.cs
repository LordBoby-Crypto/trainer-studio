namespace TrainerStudio.Core.Projects;

public static class ModuleAddressMath
{
    public static bool TryGetOffset(ulong moduleBase, ulong moduleSize, ulong address,
        out ulong offset)
    {
        offset = 0;
        if (moduleSize == 0 || address < moduleBase)
        {
            return false;
        }

        var candidateOffset = address - moduleBase;
        if (candidateOffset >= moduleSize)
        {
            return false;
        }

        offset = candidateOffset;
        return true;
    }

    public static bool TryResolve(ulong moduleBase, ulong moduleSize, ulong offset,
        out ulong address)
    {
        address = 0;
        if (moduleSize == 0 || offset >= moduleSize || ulong.MaxValue - moduleBase < offset)
        {
            return false;
        }

        address = moduleBase + offset;
        return true;
    }
}
