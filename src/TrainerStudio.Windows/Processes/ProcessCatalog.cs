using System.Diagnostics;

namespace TrainerStudio.Windows.Processes;

public static class ProcessCatalog
{
    public static IReadOnlyList<ProcessDescriptor> GetAttachableProcesses()
    {
        var currentId = Environment.ProcessId;
        var processes = new List<ProcessDescriptor>();

        foreach (var process in Process.GetProcesses())
        {
            using (process)
            {
                if (process.Id == currentId)
                {
                    continue;
                }

                try
                {
                    if (string.IsNullOrWhiteSpace(process.ProcessName) || process.HasExited)
                    {
                        continue;
                    }

                    string? path = null;
                    try
                    {
                        path = process.MainModule?.FileName;
                    }
                    catch
                    {
                        // Many system processes intentionally deny module queries.
                    }

                    processes.Add(new ProcessDescriptor(process.Id, process.ProcessName, path));
                }
                catch
                {
                    // A process can exit or become inaccessible during enumeration.
                }
            }
        }

        return processes
            .OrderBy(process => process.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(process => process.Id)
            .ToArray();
    }
}
