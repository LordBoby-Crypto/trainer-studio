using TrainerStudio.Core.Projects;

namespace TrainerStudio.App.ViewModels;

public sealed record DiscoveryViewModel(SavedDiscovery Discovery)
{
    public string Name => Discovery.Name;
    public string Notes => Discovery.Notes;
    public string Reliability => Discovery.Reliability switch
    {
        DiscoveryReliability.Experimental => "Experimental",
        DiscoveryReliability.SessionStable => "Session stable",
        DiscoveryReliability.RestartStable => "Restart stable",
        DiscoveryReliability.UpdateStable => "Update stable",
        _ => Discovery.Reliability.ToString()
    };

    public string Address => Discovery.AddressResolution switch
    {
        AddressResolutionKind.MainModuleRelative
            => $"{Discovery.ModuleName}+0x{Discovery.ModuleOffset:X}",
        _ => $"0x{Discovery.LastKnownAddress:X16}"
    };

    public string ValidationSummary
    {
        get
        {
            var confirmations = Discovery.Validations.Count(validation => validation.Confirmed);
            var sessions = Discovery.Validations
                .Where(validation => validation.Confirmed
                    && validation.AttachmentSessionId != Guid.Empty)
                .Select(validation => validation.AttachmentSessionId)
                .Distinct()
                .Count();
            return $"{confirmations} confirmations · {sessions} sessions";
        }
    }

    public string PointerPathSummary
    {
        get
        {
            var modulePaths = Discovery.PointerPaths.Count(path =>
                path.RootKind == PointerRootKind.MainModuleRelative);
            var absolutePaths = Discovery.PointerPaths.Count - modulePaths;
            return Discovery.PointerPaths.Count == 0
                ? "No pointer paths saved"
                : $"{Discovery.PointerPaths.Count} paths · {modulePaths} module · {absolutePaths} experimental";
        }
    }
}
