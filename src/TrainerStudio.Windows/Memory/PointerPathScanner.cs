using System.Buffers.Binary;
using TrainerStudio.Core.Projects;
using TrainerStudio.Windows.Processes;

namespace TrainerStudio.Windows.Memory;

public sealed class PointerPathScanner
{
    private const int ChunkSize = 1024 * 1024;
    private const ulong PageSize = 0x1000;
    private readonly ProcessMemorySession memory;

    public PointerPathScanner(ProcessMemorySession memory) => this.memory = memory;

    public Task<PointerScanResult> FindPathsAsync(
        ulong targetAddress,
        ProcessDescriptor process,
        PointerScanOptions options,
        IProgress<PointerScanProgress>? progress,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(process);
        ArgumentNullException.ThrowIfNull(options);
        options.Validate();
        return Task.Run(
            () => FindPaths(targetAddress, process, options, progress, cancellationToken),
            cancellationToken);
    }

    private PointerScanResult FindPaths(
        ulong targetAddress,
        ProcessDescriptor process,
        PointerScanOptions options,
        IProgress<PointerScanProgress>? progress,
        CancellationToken cancellationToken)
    {
        var regions = memory.EnumerateReadableRegions()
            .OrderByDescending(region => IsInMainModule(region.BaseAddress, process))
            .ThenBy(region => region.BaseAddress)
            .ToArray();
        var bytesPerLevel = regions.Aggregate<MemoryRegion, ulong>(
            0, (total, region) => AddSaturating(total, region.Size));
        var totalBytes = MultiplySaturating(bytesPerLevel, checked((ulong)options.MaximumDepth));
        ulong processedBytes = 0;
        var paths = new Dictionary<string, PointerPath>(StringComparer.Ordinal);
        var frontier = new List<FrontierNode>
        {
            new(targetAddress, [])
        };
        var truncated = false;
        var levelsCompleted = 0;

        for (var level = 1; level <= options.MaximumDepth && frontier.Count > 0; level++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var links = FindPointerSources(
                frontier,
                regions,
                options,
                level,
                bytesPerLevel,
                totalBytes,
                ref processedBytes,
                progress,
                cancellationToken,
                out var levelTruncated);
            truncated |= levelTruncated;
            levelsCompleted = level;

            var next = new Dictionary<string, FrontierNode>(StringComparer.Ordinal);
            foreach (var link in links)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var offsets = new List<ulong>(link.Target.Offsets.Count + 1)
                {
                    link.Offset
                };
                offsets.AddRange(link.Target.Offsets);

                if (process.TryGetMainModuleOffset(link.SourceAddress, out var moduleOffset))
                {
                    AddPath(paths, new PointerPath
                    {
                        RootKind = PointerRootKind.MainModuleRelative,
                        ModuleName = process.MainModuleName,
                        RootOffset = moduleOffset,
                        Offsets = offsets
                    });
                }

                if (link.SourceAddress % PageSize == 0)
                {
                    AddPath(paths, new PointerPath
                    {
                        RootKind = PointerRootKind.Absolute,
                        AbsoluteRootAddress = link.SourceAddress,
                        Offsets = offsets
                    });
                }

                if (level < options.MaximumDepth)
                {
                    var node = new FrontierNode(link.SourceAddress, offsets);
                    var identity = GetFrontierIdentity(node);
                    if (!next.ContainsKey(identity))
                    {
                        if (next.Count >= options.MaximumFrontierNodes)
                        {
                            truncated = true;
                            continue;
                        }

                        next.Add(identity, node);
                    }
                }
            }

            if (paths.Count >= options.MaximumPaths)
            {
                truncated = true;
                break;
            }

            frontier = next.Values.ToList();
        }

        var ordered = paths.Values
            .OrderBy(path => path.RootKind == PointerRootKind.MainModuleRelative ? 0 : 1)
            .ThenBy(path => path.Offsets.Count)
            .ThenBy(path => path.RootKind == PointerRootKind.MainModuleRelative
                ? path.RootOffset
                : path.AbsoluteRootAddress)
            .Take(options.MaximumPaths)
            .ToArray();
        if (paths.Count > ordered.Length)
        {
            truncated = true;
        }

        progress?.Report(new PointerScanProgress(
            levelsCompleted,
            options.MaximumDepth,
            processedBytes,
            totalBytes,
            ordered.Length,
            frontier.Count));
        return new PointerScanResult(ordered, levelsCompleted, processedBytes, truncated);
    }

    private IReadOnlyList<PointerLink> FindPointerSources(
        IReadOnlyList<FrontierNode> frontier,
        IReadOnlyList<MemoryRegion> regions,
        PointerScanOptions options,
        int level,
        ulong bytesPerLevel,
        ulong totalBytes,
        ref ulong processedBytes,
        IProgress<PointerScanProgress>? progress,
        CancellationToken cancellationToken,
        out bool truncated)
    {
        truncated = false;
        var targetsByAddress = frontier
            .GroupBy(node => node.Address)
            .ToDictionary(group => group.Key, group => group.ToArray());
        var targetAddresses = targetsByAddress.Keys.Order().ToArray();
        var links = new List<PointerLink>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        ulong levelBytes = 0;

        foreach (var region in regions)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ulong regionOffset = 0;
            while (regionOffset < region.Size)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var wanted = (int)Math.Min((ulong)ChunkSize, region.Size - regionOffset);
                var buffer = new byte[wanted];
                var read = memory.Read(region.BaseAddress + regionOffset, buffer, wanted);
                if (read >= sizeof(ulong))
                {
                    var chunkAddress = region.BaseAddress + regionOffset;
                    var alignment = checked((ulong)options.Alignment);
                    var firstOffset = checked((int)((alignment
                        - (chunkAddress % alignment))
                        % alignment));
                    for (var offset = firstOffset;
                         offset <= read - sizeof(ulong);
                         offset += options.Alignment)
                    {
                        var pointerValue = BinaryPrimitives.ReadUInt64LittleEndian(
                            buffer.AsSpan(offset, sizeof(ulong)));
                        if (pointerValue == 0)
                        {
                            continue;
                        }

                        var maximumTarget = AddSaturating(pointerValue, options.MaximumOffset);
                        var index = LowerBound(targetAddresses, pointerValue);
                        while (index < targetAddresses.Length
                               && targetAddresses[index] <= maximumTarget)
                        {
                            var targetAddress = targetAddresses[index];
                            var sourceAddress = chunkAddress + checked((ulong)offset);
                            if (sourceAddress != targetAddress)
                            {
                                var pointerOffset = targetAddress - pointerValue;
                                foreach (var target in targetsByAddress[targetAddress])
                                {
                                    var link = new PointerLink(sourceAddress, pointerOffset, target);
                                    var identity = GetLinkIdentity(link);
                                    if (seen.Add(identity))
                                    {
                                        if (links.Count >= options.MaximumLinksPerLevel)
                                        {
                                            truncated = true;
                                            break;
                                        }

                                        links.Add(link);
                                    }
                                }
                            }

                            if (truncated)
                            {
                                break;
                            }

                            index++;
                        }

                        if (truncated)
                        {
                            break;
                        }
                    }
                }

                regionOffset += checked((ulong)wanted);
                levelBytes += checked((ulong)wanted);
                processedBytes = AddSaturating(
                    MultiplySaturating(bytesPerLevel, checked((ulong)(level - 1))),
                    levelBytes);
                progress?.Report(new PointerScanProgress(
                    level,
                    options.MaximumDepth,
                    processedBytes,
                    totalBytes,
                    0,
                    links.Count));

                if (truncated)
                {
                    return links;
                }
            }
        }

        return links;
    }

    private static int LowerBound(ulong[] values, ulong sought)
    {
        var low = 0;
        var high = values.Length;
        while (low < high)
        {
            var middle = low + ((high - low) / 2);
            if (values[middle] < sought)
            {
                low = middle + 1;
            }
            else
            {
                high = middle;
            }
        }

        return low;
    }

    private static bool IsInMainModule(ulong address, ProcessDescriptor process)
        => process.TryGetMainModuleOffset(address, out _);

    private static void AddPath(IDictionary<string, PointerPath> paths, PointerPath path)
    {
        var identity = PointerPathResolver.GetIdentity(path);
        paths.TryAdd(identity, path);
    }

    private static string GetFrontierIdentity(FrontierNode node)
        => $"{node.Address:X16}|{string.Join(",", node.Offsets.Select(value => value.ToString("X")))}";

    private static string GetLinkIdentity(PointerLink link)
        => $"{link.SourceAddress:X16}|{link.Offset:X}|{GetFrontierIdentity(link.Target)}";

    private static ulong AddSaturating(ulong left, ulong right)
        => ulong.MaxValue - left < right ? ulong.MaxValue : left + right;

    private static ulong MultiplySaturating(ulong left, ulong right)
        => left != 0 && right > ulong.MaxValue / left ? ulong.MaxValue : left * right;

    private sealed record FrontierNode(ulong Address, List<ulong> Offsets);
    private sealed record PointerLink(ulong SourceAddress, ulong Offset, FrontierNode Target);
}

public sealed class PointerScanOptions
{
    public int MaximumDepth { get; init; } = 3;
    public ulong MaximumOffset { get; init; } = 0x1000;
    public int Alignment { get; init; } = 8;
    public int MaximumPaths { get; init; } = 200;
    public int MaximumFrontierNodes { get; init; } = 25_000;
    public int MaximumLinksPerLevel { get; init; } = 50_000;

    internal void Validate()
    {
        if (MaximumDepth is < 1 or > 8)
        {
            throw new ArgumentOutOfRangeException(
                nameof(MaximumDepth), "Pointer depth must be between 1 and 8.");
        }

        if (MaximumOffset > 0x100000)
        {
            throw new ArgumentOutOfRangeException(
                nameof(MaximumOffset), "Maximum pointer offset cannot exceed 0x100000.");
        }

        if (Alignment is not (4 or 8))
        {
            throw new ArgumentOutOfRangeException(
                nameof(Alignment), "Pointer alignment must be 4 or 8 bytes.");
        }

        if (MaximumPaths < 1 || MaximumFrontierNodes < 1 || MaximumLinksPerLevel < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(MaximumPaths), "Pointer scan safety limits must be positive.");
        }
    }
}

public sealed record PointerScanResult(
    IReadOnlyList<PointerPath> Paths,
    int LevelsCompleted,
    ulong BytesScanned,
    bool Truncated);

public sealed record PointerScanProgress(
    int CurrentLevel,
    int MaximumDepth,
    ulong CompletedBytes,
    ulong TotalBytes,
    int Paths,
    int FrontierNodes)
{
    public double Fraction => TotalBytes == 0
        ? 0
        : Math.Clamp((double)CompletedBytes / TotalBytes, 0, 1);
}
