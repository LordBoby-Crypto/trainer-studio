using TrainerStudio.Core.Projects;
using TrainerStudio.Core.Scanning;
using ScanValueType = TrainerStudio.Core.Scanning.ValueType;

var tests = new (string Name, Func<Task> Run)[]
{
    ("Int32 codec round-trip", () => RunSync(Int32CodecRoundTrip)),
    ("Float32 codec round-trip", () => RunSync(Float32CodecRoundTrip)),
    ("Exact offset search", () => RunSync(ExactOffsetSearch)),
    ("Comparative matching", () => RunSync(ComparativeMatching)),
    ("Project JSON round-trip", ProjectRoundTripAsync),
    ("Project atomic overwrite", ProjectAtomicOverwriteAsync),
    ("Legacy project migration", LegacyProjectMigrationAsync),
    ("Module-relative address math", () => RunSync(ModuleRelativeAddressMath)),
    ("Session reliability", () => RunSync(SessionReliability)),
    ("Restart and update reliability", () => RunSync(RestartAndUpdateReliability))
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
        Assert(loaded.FormatVersion == TrainerProject.CurrentFormatVersion,
            "Project format was not current after load.");
        Assert(loaded.ProjectId != Guid.Empty, "Project ID was not persisted.");
        Assert(loaded.Discoveries.Single().Id != Guid.Empty, "Discovery ID was not persisted.");
    }
    finally
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }
}

static async Task ProjectAtomicOverwriteAsync()
{
    var directory = Path.Combine(Path.GetTempPath(), $"trainer-studio-{Guid.NewGuid():N}");
    var path = Path.Combine(directory, "overwrite.trainerstudio.json");
    try
    {
        var project = new TrainerProject { Name = "First" };
        await ProjectStore.SaveAsync(project, path);
        project.Name = "Second";
        await ProjectStore.SaveAsync(project, path);

        var loaded = await ProjectStore.LoadAsync(path);
        Assert(loaded.Name == "Second", "Atomic overwrite retained the old project.");
        Assert(Directory.GetFiles(directory, "*.tmp").Length == 0,
            "Atomic save left a temporary file behind.");
    }
    finally
    {
        if (Directory.Exists(directory))
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}

static async Task LegacyProjectMigrationAsync()
{
    var path = Path.Combine(Path.GetTempPath(), $"trainer-studio-{Guid.NewGuid():N}.json");
    try
    {
        const string json = """
            {
              "formatVersion": 1,
              "name": "Legacy Trainer",
              "executableName": "LegacyGame.exe",
              "discoveries": [
                {
                  "name": "Health",
                  "lastKnownAddress": 4096,
                  "valueType": "Int32",
                  "lastKnownValue": "100"
                }
              ]
            }
            """;
        await File.WriteAllTextAsync(path, json);
        var loaded = await ProjectStore.LoadAsync(path);
        Assert(loaded.FormatVersion == TrainerProject.CurrentFormatVersion,
            "Legacy project was not migrated.");
        Assert(loaded.ProjectId != Guid.Empty, "Legacy project did not receive an ID.");
        Assert(loaded.Discoveries.Single().Id != Guid.Empty,
            "Legacy discovery did not receive an ID.");
        Assert(loaded.Discoveries.Single().Validations.Count == 0,
            "Migration invented validation evidence.");
    }
    finally
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }
}

static void SessionReliability()
{
    var session = Guid.NewGuid();
    var discovery = NewDiscovery();
    discovery.Validations.Add(NewValidation(session, "game-v1", automatic: false));
    Assert(DiscoveryReliabilityEvaluator.Evaluate(discovery)
        == DiscoveryReliability.Experimental, "One confirmation must remain experimental.");

    discovery.Validations.Add(NewValidation(session, "game-v1", automatic: true));
    Assert(DiscoveryReliabilityEvaluator.Evaluate(discovery)
        == DiscoveryReliability.SessionStable, "Repeated same-session confirmation was missed.");
}

static void ModuleRelativeAddressMath()
{
    Assert(ModuleAddressMath.TryGetOffset(0x1000, 0x500, 0x1234, out var offset),
        "Address inside the module was rejected.");
    Assert(offset == 0x234, "The module offset was calculated incorrectly.");
    Assert(ModuleAddressMath.TryResolve(0x7000, 0x500, offset, out var resolved),
        "Valid module offset did not resolve.");
    Assert(resolved == 0x7234, "The relocated module address was incorrect.");
    Assert(!ModuleAddressMath.TryGetOffset(0x1000, 0x500, 0x1500, out _),
        "The first address beyond the module was accepted.");
    Assert(!ModuleAddressMath.TryResolve(ulong.MaxValue - 2, 10, 5, out _),
        "Overflowing module address was accepted.");
}

static void RestartAndUpdateReliability()
{
    var discovery = NewDiscovery();
    discovery.Validations.Add(NewValidation(Guid.NewGuid(), "game-v1", automatic: true));
    discovery.Validations.Add(NewValidation(Guid.NewGuid(), "game-v1", automatic: true));
    Assert(DiscoveryReliabilityEvaluator.Evaluate(discovery)
        == DiscoveryReliability.RestartStable, "Two automatic sessions must be restart-stable.");

    discovery.Validations.Add(NewValidation(Guid.NewGuid(), "game-v2", automatic: true));
    Assert(DiscoveryReliabilityEvaluator.Evaluate(discovery)
        == DiscoveryReliability.UpdateStable,
        "Three automatic sessions across two executable identities must be update-stable.");
}

static SavedDiscovery NewDiscovery() => new()
{
    Name = "Health",
    ValueType = ScanValueType.Int32,
    LastKnownValue = "100"
};

static DiscoveryValidation NewValidation(Guid session, string executableIdentity, bool automatic)
    => new()
    {
        AttachmentSessionId = session,
        ExecutableIdentity = executableIdentity,
        ObservedAddress = 0x1234,
        ObservedValue = "100",
        Confirmed = true,
        AddressResolvedAutomatically = automatic
    };

static void Assert(bool condition, string message)
{
    if (!condition)
    {
        throw new InvalidOperationException(message);
    }
}
