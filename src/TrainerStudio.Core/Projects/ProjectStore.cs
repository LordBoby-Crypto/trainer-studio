using System.Text.Json;
using System.Text.Json.Serialization;

namespace TrainerStudio.Core.Projects;

public static class ProjectStore
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public static async Task SaveAsync(TrainerProject project, string path,
        CancellationToken cancellationToken = default)
    {
        project.ModifiedUtc = DateTimeOffset.UtcNow;
        await using var stream = File.Create(path);
        await JsonSerializer.SerializeAsync(stream, project, Options, cancellationToken);
    }

    public static async Task<TrainerProject> LoadAsync(string path,
        CancellationToken cancellationToken = default)
    {
        await using var stream = File.OpenRead(path);
        var project = await JsonSerializer.DeserializeAsync<TrainerProject>(
            stream, Options, cancellationToken);
        return project ?? throw new InvalidDataException("The project file is empty or invalid.");
    }
}
