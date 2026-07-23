using System.Text.Json;
using System.Text.Json.Serialization;

namespace TrainerStudio.Core.Projects;

public static class ProjectStore
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public static async Task SaveAsync(TrainerProject project, string path,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        project.FormatVersion = TrainerProject.CurrentFormatVersion;
        project.ModifiedUtc = DateTimeOffset.UtcNow;
        var fullPath = Path.GetFullPath(path);
        var directory = Path.GetDirectoryName(fullPath)
            ?? throw new InvalidOperationException("The project path has no parent directory.");
        Directory.CreateDirectory(directory);
        var temporaryPath = Path.Combine(directory,
            $".{Path.GetFileName(fullPath)}.{Guid.NewGuid():N}.tmp");

        try
        {
            await using (var stream = new FileStream(temporaryPath, FileMode.CreateNew,
                             FileAccess.Write, FileShare.None, 64 * 1024, useAsync: true))
            {
                await JsonSerializer.SerializeAsync(stream, project, Options, cancellationToken);
                await stream.FlushAsync(cancellationToken);
            }

            File.Move(temporaryPath, fullPath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }

    public static async Task<TrainerProject> LoadAsync(string path,
        CancellationToken cancellationToken = default)
    {
        await using var stream = File.OpenRead(path);
        var project = await JsonSerializer.DeserializeAsync<TrainerProject>(
            stream, Options, cancellationToken);
        if (project is null)
        {
            throw new InvalidDataException("The project file is empty or invalid.");
        }

        NormalizeAndValidate(project);
        return project;
    }

    private static void NormalizeAndValidate(TrainerProject project)
    {
        if (project.FormatVersion > TrainerProject.CurrentFormatVersion)
        {
            throw new InvalidDataException(
                $"This project uses format {project.FormatVersion}, but this version of Trainer Studio supports up to {TrainerProject.CurrentFormatVersion}.");
        }

        if (string.IsNullOrWhiteSpace(project.Name))
        {
            throw new InvalidDataException("The project does not contain a valid name.");
        }

        if (project.Discoveries is null)
        {
            throw new InvalidDataException("The project discoveries collection is invalid.");
        }

        project.ProjectId = project.ProjectId == Guid.Empty ? Guid.NewGuid() : project.ProjectId;
        foreach (var discovery in project.Discoveries)
        {
            if (string.IsNullOrWhiteSpace(discovery.Name))
            {
                throw new InvalidDataException("A saved discovery does not contain a valid name.");
            }

            discovery.Id = discovery.Id == Guid.Empty ? Guid.NewGuid() : discovery.Id;
            discovery.Validations ??= [];
            discovery.PointerPaths ??= [];
            foreach (var pointerPath in discovery.PointerPaths)
            {
                pointerPath.Id = pointerPath.Id == Guid.Empty ? Guid.NewGuid() : pointerPath.Id;
                pointerPath.ModuleName ??= string.Empty;
                pointerPath.Offsets ??= [];
                pointerPath.Validations ??= [];
                if (pointerPath.PointerSize != 8 || pointerPath.Offsets.Count == 0)
                {
                    throw new InvalidDataException(
                        $"The pointer path saved for {discovery.Name} is invalid.");
                }
            }

            DiscoveryReliabilityEvaluator.Refresh(discovery);
        }

        project.FormatVersion = TrainerProject.CurrentFormatVersion;
    }
}
