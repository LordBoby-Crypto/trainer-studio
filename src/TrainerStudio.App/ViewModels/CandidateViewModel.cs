using TrainerStudio.Core.Scanning;

namespace TrainerStudio.App.ViewModels;

public sealed record CandidateViewModel(ScanCandidate Candidate, string CurrentValue)
{
    public string Address => Candidate.AddressText;
}
