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
                    var moduleName = string.Empty;
                    ulong moduleBaseAddress = 0;
                    ulong moduleSize = 0;
                    try
                    {
                        var module = process.MainModule;
                        if (module is not null)
                        {
                            path = module.FileName;
                            moduleName = module.ModuleName;
                            moduleBaseAddress = unchecked((ulong)module.BaseAddress.ToInt64());
                            moduleSize = checked((ulong)module.ModuleMemorySize);
                        }
                    }
                    catch
                    {
                        // Many system processes intentionally deny module queries.
                    }

                    processes.Add(new ProcessDescriptor(process.Id, process.ProcessName, path,
                        moduleName, moduleBaseAddress, moduleSize, CreateExecutableIdentity(path)));
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

    private static string CreateExecutableIdentity(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return "unavailable";
        }

        try
        {
            var file = new FileInfo(path);
            var version = FileVersionInfo.GetVersionInfo(path).FileVersion ?? "unversioned";
            return $"{file.Name}|{file.Length}|{file.LastWriteTimeUtc.Ticks}|{version}";
        }
        catch
        {
            return Path.GetFileName(path);
        }
    }
}
