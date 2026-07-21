namespace TrainerStudio.Core.Scanning;

public sealed class ScanSession
{
    public ScanSession(ValueType valueType, IReadOnlyList<ScanCandidate> candidates)
    {
        ValueType = valueType;
        Candidates = candidates;
    }

    public ValueType ValueType { get; }
    public IReadOnlyList<ScanCandidate> Candidates { get; private set; }

    public void ReplaceCandidates(IReadOnlyList<ScanCandidate> candidates)
        => Candidates = candidates;
}
