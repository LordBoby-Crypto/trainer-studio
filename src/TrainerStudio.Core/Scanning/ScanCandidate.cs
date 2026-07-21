namespace TrainerStudio.Core.Scanning;

public sealed record ScanCandidate(ulong Address, byte[] LastValue)
{
    public string AddressText => $"0x{Address:X16}";
}
