namespace TrainerStudio.Windows.Processes;

public sealed record ProcessDescriptor(int Id, string Name, string? FilePath)
{
    public string DisplayName => $"{Name}  ·  PID {Id}";
}
