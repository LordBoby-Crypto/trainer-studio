using ScanValueType = TrainerStudio.Core.Scanning.ValueType;

namespace TrainerStudio.Core.Projects;

public sealed class TrainerProject
{
    public int FormatVersion { get; init; } = 1;
    public required string Name { get; set; }
    public string ExecutableName { get; set; } = string.Empty;
    public DateTimeOffset CreatedUtc { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset ModifiedUtc { get; set; } = DateTimeOffset.UtcNow;
    public List<SavedDiscovery> Discoveries { get; init; } = [];
}

public sealed class SavedDiscovery
{
    public required string Name { get; set; }
    public string Notes { get; set; } = string.Empty;
    public ulong LastKnownAddress { get; set; }
    public ScanValueType ValueType { get; set; }
    public string LastKnownValue { get; set; } = string.Empty;
    public DiscoveryReliability Reliability { get; set; } = DiscoveryReliability.Experimental;
}

public enum DiscoveryReliability
{
    Experimental,
    SessionStable,
    RestartStable,
    UpdateStable
}
