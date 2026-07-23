using ScanValueType = TrainerStudio.Core.Scanning.ValueType;

namespace TrainerStudio.Core.Projects;

public sealed class TrainerProject
{
    public const int CurrentFormatVersion = 3;

    public int FormatVersion { get; set; } = CurrentFormatVersion;
    public Guid ProjectId { get; set; } = Guid.NewGuid();
    public required string Name { get; set; }
    public string ExecutableName { get; set; } = string.Empty;
    public DateTimeOffset CreatedUtc { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset ModifiedUtc { get; set; } = DateTimeOffset.UtcNow;
    public List<SavedDiscovery> Discoveries { get; init; } = [];
}

public sealed class SavedDiscovery
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string Name { get; set; }
    public string Notes { get; set; } = string.Empty;
    public ulong LastKnownAddress { get; set; }
    public AddressResolutionKind AddressResolution { get; set; } = AddressResolutionKind.Absolute;
    public string ModuleName { get; set; } = string.Empty;
    public ulong ModuleOffset { get; set; }
    public ScanValueType ValueType { get; set; }
    public string LastKnownValue { get; set; } = string.Empty;
    public DiscoveryReliability Reliability { get; set; } = DiscoveryReliability.Experimental;
    public List<DiscoveryValidation> Validations { get; set; } = [];
    public List<PointerPath> PointerPaths { get; set; } = [];
}

public sealed class DiscoveryValidation
{
    public DateTimeOffset TestedUtc { get; set; } = DateTimeOffset.UtcNow;
    public Guid AttachmentSessionId { get; set; }
    public string ExecutableIdentity { get; set; } = string.Empty;
    public ulong ObservedAddress { get; set; }
    public string ObservedValue { get; set; } = string.Empty;
    public bool Confirmed { get; set; }
    public bool AddressResolvedAutomatically { get; set; }
}

public enum AddressResolutionKind
{
    Absolute,
    MainModuleRelative
}

public enum DiscoveryReliability
{
    Experimental,
    SessionStable,
    RestartStable,
    UpdateStable
}
