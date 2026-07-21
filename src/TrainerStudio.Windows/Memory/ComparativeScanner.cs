using TrainerStudio.Core.Scanning;
using ScanValueType = TrainerStudio.Core.Scanning.ValueType;

namespace TrainerStudio.Windows.Memory;

public sealed class ComparativeScanner
{
    private const int ChunkSize = 1024 * 1024;
    public const int MaximumCandidates = 250_000;
    private readonly ProcessMemorySession memory;

    public ComparativeScanner(ProcessMemorySession memory) => this.memory = memory;

    public Task<ScanSession> FirstScanAsync(
        ScanValueType type,
        byte[] exactValue,
        IProgress<ScanProgress>? progress,
        CancellationToken cancellationToken)
        => Task.Run(() => FirstScan(type, exactValue, progress, cancellationToken),
            cancellationToken);

    public Task<ScanSession> NextScanAsync(
        ScanSession session,
        ComparisonMode mode,
        byte[] exactValue,
        IProgress<ScanProgress>? progress,
        CancellationToken cancellationToken)
        => Task.Run(() => NextScan(session, mode, exactValue, progress, cancellationToken),
            cancellationToken);

    private ScanSession FirstScan(
        ScanValueType type,
        byte[] exactValue,
        IProgress<ScanProgress>? progress,
        CancellationToken cancellationToken)
    {
        var candidates = new List<ScanCandidate>();
        var regions = memory.EnumerateReadableRegions().ToArray();
        ulong processed = 0;
        var total = regions.Aggregate<MemoryRegion, ulong>(0, (sum, region) => sum + region.Size);
        var overlap = Math.Max(0, exactValue.Length - 1);

        foreach (var region in regions)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ulong regionOffset = 0;
            var prefix = Array.Empty<byte>();

            while (regionOffset < region.Size)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var wanted = (int)Math.Min((ulong)ChunkSize, region.Size - regionOffset);
                var readBuffer = new byte[wanted];
                var read = memory.Read(region.BaseAddress + regionOffset, readBuffer, wanted);
                if (read > 0)
                {
                    var visible = new byte[prefix.Length + read];
                    Buffer.BlockCopy(prefix, 0, visible, 0, prefix.Length);
                    Buffer.BlockCopy(readBuffer, 0, visible, prefix.Length, read);
                    foreach (var offset in ScanMatcher.FindExactOffsets(visible, exactValue))
                    {
                        var visibleBase = region.BaseAddress + regionOffset
                            - checked((ulong)prefix.Length);
                        var address = visibleBase + checked((ulong)offset);
                        candidates.Add(new ScanCandidate(address, exactValue.ToArray()));
                        if (candidates.Count >= MaximumCandidates)
                        {
                            throw new ScanLimitExceededException(MaximumCandidates);
                        }
                    }

                    var prefixCount = Math.Min(overlap, visible.Length);
                    if (prefixCount > 0 && read == wanted)
                    {
                        prefix = visible[^prefixCount..];
                    }
                    else
                    {
                        prefix = Array.Empty<byte>();
                    }
                }
                else
                {
                    prefix = Array.Empty<byte>();
                }

                regionOffset += checked((ulong)wanted);
                processed += checked((ulong)wanted);
                progress?.Report(new ScanProgress(processed, total, candidates.Count));
            }
        }

        return new ScanSession(type, candidates);
    }

    private ScanSession NextScan(
        ScanSession session,
        ComparisonMode mode,
        byte[] exactValue,
        IProgress<ScanProgress>? progress,
        CancellationToken cancellationToken)
    {
        var remaining = new List<ScanCandidate>();
        var size = ValueCodec.SizeOf(session.ValueType);

        for (var index = 0; index < session.Candidates.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var candidate = session.Candidates[index];
            var current = memory.TryRead(candidate.Address, size);
            if (current is not null && ScanMatcher.IsMatch(candidate.LastValue, current,
                    session.ValueType, mode, exactValue))
            {
                remaining.Add(new ScanCandidate(candidate.Address, current));
            }

            if (index % 128 == 0 || index == session.Candidates.Count - 1)
            {
                progress?.Report(new ScanProgress(
                    checked((ulong)(index + 1)),
                    checked((ulong)session.Candidates.Count),
                    remaining.Count));
            }
        }

        session.ReplaceCandidates(remaining);
        return session;
    }
}

public sealed record ScanProgress(ulong Completed, ulong Total, int Candidates)
{
    public double Fraction => Total == 0 ? 0 : Math.Clamp((double)Completed / Total, 0, 1);
}

public sealed class ScanLimitExceededException(int maximum)
    : Exception($"The scan reached the safety limit of {maximum:N0} candidates. "
        + "Change the gameplay value and run a more distinctive exact scan.");
