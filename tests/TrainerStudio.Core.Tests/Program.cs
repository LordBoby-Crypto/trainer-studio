using TrainerStudio.Core.Projects;
using TrainerStudio.Core.Scanning;
using ScanValueType = TrainerStudio.Core.Scanning.ValueType;

var tests = new (string Name, Func<Task> Run)[]
{
    ("Int32 codec round-trip", () => RunSync(Int32CodecRoundTrip)),
    ("Float32 codec round-trip", () => RunSync(Float32CodecRoundTrip)),
    ("Exact offset search", () => RunSync(ExactOffsetSearch)),
    ("Comparative matching", () => RunSync(ComparativeMatching)),
    ("Project JSON round-trip", ProjectRoundTripAsync)
};

var failures = new List<string>();
foreach (var test in tests)
{
    try
    {
        await test.Run();
        Console.WriteLine($"PASS  {test.Name}");
    }
    catch (Exception exception)
    {
        failures.Add($"{test.Name}: {exception.Message}");
        Console.WriteLine($"FAIL  {test.Name}: {exception.Message}");
    }
}

Console.WriteLine($"{tests.Length - failures.Count}/{tests.Length} tests passed.");
return failures.Count == 0 ? 0 : 1;

static Task RunSync(Action action)
{
    action();
    return Task.CompletedTask;
}

static void Int32CodecRoundTrip()
{
    Assert(ValueCodec.TryEncode("-1452", ScanValueType.Int32, out var bytes, out _),
        "Int32 value was rejected.");
    Assert(ValueCodec.Decode(bytes, ScanValueType.Int32) == "-1452", "Int32 changed on decode.");
}

static void Float32CodecRoundTrip()
{
    Assert(ValueCodec.TryEncode("12.25", ScanValueType.Float32, out var bytes, out _),
        "Float32 value was rejected.");
    Assert(ValueCodec.Decode(bytes, ScanValueType.Float32) == "12.25", "Float32 changed on decode.");
    Assert(!ValueCodec.TryEncode("NaN", ScanValueType.Float32, out _, out _),
        "NaN must not be accepted as a scan value.");
}

static void ExactOffsetSearch()
{
    byte[] memory = [4, 8, 9, 8, 9, 7];
    byte[] sought = [8, 9];
    var offsets = ScanMatcher.FindExactOffsets(memory, sought);
    Assert(offsets.SequenceEqual([1, 3]), "Expected offsets 1 and 3.");
}

static void ComparativeMatching()
{
    ValueCodec.TryEncode("100", ScanValueType.Int32, out var previous, out _);
    ValueCodec.TryEncode("125", ScanValueType.Int32, out var current, out _);
    Assert(ScanMatcher.IsMatch(previous, current, ScanValueType.Int32,
        ComparisonMode.Increased, []), "125 should be increased from 100.");
    Assert(!ScanMatcher.IsMatch(previous, current, ScanValueType.Int32,
        ComparisonMode.Decreased, []), "125 should not be decreased from 100.");
    Assert(ScanMatcher.IsMatch(previous, current, ScanValueType.Int32,
        ComparisonMode.Exact, current), "Exact comparison failed.");
}

static async Task ProjectRoundTripAsync()
{
    var path = Path.Combine(Path.GetTempPath(), $"trainer-studio-{Guid.NewGuid():N}.json");
    try
    {
        var project = new TrainerProject { Name = "Test Trainer", ExecutableName = "test.exe" };
        project.Discoveries.Add(new SavedDiscovery
        {
            Name = "Credits",
            LastKnownAddress = 0x1234,
            LastKnownValue = "2500",
            ValueType = ScanValueType.Int32
        });
        await ProjectStore.SaveAsync(project, path);
        var loaded = await ProjectStore.LoadAsync(path);
        Assert(loaded.Name == project.Name, "Project name changed.");
        Assert(loaded.Discoveries.Single().LastKnownAddress == 0x1234,
            "Discovery address changed.");
    }
    finally
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }
}

static void Assert(bool condition, string message)
{
    if (!condition)
    {
        throw new InvalidOperationException(message);
    }
}
