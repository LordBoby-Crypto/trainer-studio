namespace TrainerStudio.Core.Projects;

public static class DiscoveryReliabilityEvaluator
{
    public static DiscoveryReliability Evaluate(SavedDiscovery discovery)
    {
        ArgumentNullException.ThrowIfNull(discovery);

        var confirmed = discovery.Validations
            .Where(validation => validation.Confirmed)
            .OrderBy(validation => validation.TestedUtc)
            .ToArray();
        if (confirmed.Length < 2)
        {
            return DiscoveryReliability.Experimental;
        }

        var automatic = confirmed
            .Where(validation => validation.AddressResolvedAutomatically)
            .ToArray();
        var automaticSessions = automatic
            .Select(validation => validation.AttachmentSessionId)
            .Where(sessionId => sessionId != Guid.Empty)
            .Distinct()
            .Count();
        var executableVersions = automatic
            .Select(validation => validation.ExecutableIdentity)
            .Where(identity => !string.IsNullOrWhiteSpace(identity))
            .Distinct(StringComparer.Ordinal)
            .Count();

        if (automaticSessions >= 3 && executableVersions >= 2)
        {
            return DiscoveryReliability.UpdateStable;
        }

        if (automaticSessions >= 2)
        {
            return DiscoveryReliability.RestartStable;
        }

        var repeatedInOneSession = confirmed
            .Where(validation => validation.AttachmentSessionId != Guid.Empty)
            .GroupBy(validation => validation.AttachmentSessionId)
            .Any(group => group.Count() >= 2);
        return repeatedInOneSession
            ? DiscoveryReliability.SessionStable
            : DiscoveryReliability.Experimental;
    }

    public static void Refresh(SavedDiscovery discovery)
        => discovery.Reliability = Evaluate(discovery);
}
