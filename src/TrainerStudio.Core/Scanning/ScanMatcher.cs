namespace TrainerStudio.Core.Scanning;

public static class ScanMatcher
{
    public static IReadOnlyList<int> FindExactOffsets(
        ReadOnlyMemory<byte> memory,
        ReadOnlyMemory<byte> sought,
        int alignment = 1)
    {
        var results = new List<int>();
        if (sought.IsEmpty || memory.Length < sought.Length)
        {
            return results;
        }

        if (alignment < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(alignment));
        }

        var haystack = memory.Span;
        var needle = sought.Span;
        for (var offset = 0; offset <= haystack.Length - needle.Length; offset += alignment)
        {
            if (haystack.Slice(offset, needle.Length).SequenceEqual(needle))
            {
                results.Add(offset);
            }
        }

        return results;
    }

    public static bool IsMatch(
        ReadOnlySpan<byte> previous,
        ReadOnlySpan<byte> current,
        ValueType type,
        ComparisonMode mode,
        ReadOnlySpan<byte> exactValue)
    {
        return mode switch
        {
            ComparisonMode.Exact => current.SequenceEqual(exactValue),
            ComparisonMode.Changed => !current.SequenceEqual(previous),
            ComparisonMode.Unchanged => current.SequenceEqual(previous),
            ComparisonMode.Increased => ValueCodec.Compare(current, previous, type) > 0,
            ComparisonMode.Decreased => ValueCodec.Compare(current, previous, type) < 0,
            _ => throw new ArgumentOutOfRangeException(nameof(mode))
        };
    }
}
