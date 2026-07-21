using System.Collections.ObjectModel;
using Microsoft.Win32;
using TrainerStudio.App.Infrastructure;
using TrainerStudio.Core.Projects;
using TrainerStudio.Core.Scanning;
using TrainerStudio.Windows.Memory;
using TrainerStudio.Windows.Processes;
using ScanValueType = TrainerStudio.Core.Scanning.ValueType;

namespace TrainerStudio.App.ViewModels;

public sealed class MainViewModel : ObservableObject, IDisposable
{
    private const int MaximumDisplayedCandidates = 10_000;
    private readonly List<ProcessDescriptor> allProcesses = [];
    private ProcessMemorySession? memory;
    private ComparativeScanner? scanner;
    private ScanSession? scanSession;
    private CancellationTokenSource? scanCancellation;
    private ProcessDescriptor? selectedProcess;
    private CandidateViewModel? selectedCandidate;
    private ScanValueType selectedValueType = ScanValueType.Int32;
    private ComparisonMode selectedComparison = ComparisonMode.Exact;
    private string processFilter = string.Empty;
    private string searchValue = "2500";
    private string writeValue = string.Empty;
    private string discoveryName = "Credits";
    private string status = "Choose a process to begin.";
    private string attachedProcess = "No process attached";
    private bool isBusy;
    private double progress;
    private TrainerProject project = new() { Name = "Untitled Trainer" };

    public MainViewModel()
    {
        RefreshProcessesCommand = new RelayCommand(RefreshProcesses, () => !IsBusy);
        AttachCommand = new RelayCommand(Attach, () => SelectedProcess is not null && !IsBusy);
        FirstScanCommand = new AsyncRelayCommand(FirstScanAsync,
            () => scanner is not null && !IsBusy, SetError);
        NextScanCommand = new AsyncRelayCommand(NextScanAsync,
            () => scanner is not null && scanSession is not null && !IsBusy, SetError);
        CancelCommand = new RelayCommand(() => scanCancellation?.Cancel(), () => IsBusy);
        WriteValueCommand = new RelayCommand(WriteValue,
            () => memory is not null && SelectedCandidate is not null && !IsBusy);
        AddDiscoveryCommand = new RelayCommand(AddDiscovery,
            () => SelectedCandidate is not null && !string.IsNullOrWhiteSpace(DiscoveryName));
        SaveProjectCommand = new AsyncRelayCommand(SaveProjectAsync, () => !IsBusy, SetError);
        OpenProjectCommand = new AsyncRelayCommand(OpenProjectAsync, () => !IsBusy, SetError);
        NewProjectCommand = new RelayCommand(NewProject, () => !IsBusy);
        RefreshProcesses();
    }

    public Array ValueTypes { get; } = Enum.GetValues(typeof(ScanValueType));
    public Array ComparisonModes { get; } = Enum.GetValues(typeof(ComparisonMode));
    public ObservableCollection<ProcessDescriptor> Processes { get; } = [];
    public ObservableCollection<CandidateViewModel> Candidates { get; } = [];

    public RelayCommand RefreshProcessesCommand { get; }
    public RelayCommand AttachCommand { get; }
    public AsyncRelayCommand FirstScanCommand { get; }
    public AsyncRelayCommand NextScanCommand { get; }
    public RelayCommand CancelCommand { get; }
    public RelayCommand WriteValueCommand { get; }
    public RelayCommand AddDiscoveryCommand { get; }
    public AsyncRelayCommand SaveProjectCommand { get; }
    public AsyncRelayCommand OpenProjectCommand { get; }
    public RelayCommand NewProjectCommand { get; }

    public ProcessDescriptor? SelectedProcess
    {
        get => selectedProcess;
        set
        {
            if (SetProperty(ref selectedProcess, value))
            {
                RaiseCommandStates();
            }
        }
    }

    public CandidateViewModel? SelectedCandidate
    {
        get => selectedCandidate;
        set
        {
            if (SetProperty(ref selectedCandidate, value))
            {
                WriteValue = value?.CurrentValue ?? string.Empty;
                RaiseCommandStates();
            }
        }
    }

    public ScanValueType SelectedValueType
    {
        get => selectedValueType;
        set => SetProperty(ref selectedValueType, value);
    }

    public ComparisonMode SelectedComparison
    {
        get => selectedComparison;
        set => SetProperty(ref selectedComparison, value);
    }

    public string ProcessFilter
    {
        get => processFilter;
        set
        {
            if (SetProperty(ref processFilter, value))
            {
                ApplyProcessFilter();
            }
        }
    }

    public string SearchValue { get => searchValue; set => SetProperty(ref searchValue, value); }
    public string WriteValue { get => writeValue; set => SetProperty(ref writeValue, value); }

    public string DiscoveryName
    {
        get => discoveryName;
        set
        {
            if (SetProperty(ref discoveryName, value))
            {
                RaiseCommandStates();
            }
        }
    }

    public string Status { get => status; private set => SetProperty(ref status, value); }
    public string AttachedProcess { get => attachedProcess; private set => SetProperty(ref attachedProcess, value); }
    public string ProjectName => project.Name;
    public string DiscoveryCount => $"{project.Discoveries.Count} saved discoveries";
    public string ResultSummary
    {
        get
        {
            var total = scanSession?.Candidates.Count ?? 0;
            return total > MaximumDisplayedCandidates
                ? $"{total:N0} candidates · showing first {MaximumDisplayedCandidates:N0}"
                : $"{total:N0} candidates";
        }
    }
    public bool HasScan => scanSession is not null;

    public bool IsBusy
    {
        get => isBusy;
        private set
        {
            if (SetProperty(ref isBusy, value))
            {
                RaiseCommandStates();
            }
        }
    }

    public double Progress { get => progress; private set => SetProperty(ref progress, value); }

    private void RefreshProcesses()
    {
        allProcesses.Clear();
        allProcesses.AddRange(ProcessCatalog.GetAttachableProcesses());
        ApplyProcessFilter();
        Status = $"Found {allProcesses.Count:N0} running processes.";
    }

    private void ApplyProcessFilter()
    {
        var previousId = SelectedProcess?.Id;
        Processes.Clear();
        foreach (var process in allProcesses.Where(process =>
                     string.IsNullOrWhiteSpace(ProcessFilter)
                     || process.DisplayName.Contains(ProcessFilter, StringComparison.OrdinalIgnoreCase)))
        {
            Processes.Add(process);
        }

        SelectedProcess = Processes.FirstOrDefault(process => process.Id == previousId);
    }

    private void Attach()
    {
        if (SelectedProcess is null)
        {
            return;
        }

        try
        {
            memory?.Dispose();
            memory = null;
            scanner = null;
            scanSession = null;
            Candidates.Clear();
            SelectedCandidate = null;
            OnPropertyChanged(nameof(HasScan));
            OnPropertyChanged(nameof(ResultSummary));
            AttachedProcess = "No process attached";
            memory = ProcessMemorySession.Attach(SelectedProcess);
            scanner = new ComparativeScanner(memory);
            project.ExecutableName = SelectedProcess.FilePath ?? SelectedProcess.Name;
            AttachedProcess = SelectedProcess.DisplayName;
            Status = "Attached. Choose a value type and run the first exact scan.";
            OnPropertyChanged(nameof(HasScan));
            OnPropertyChanged(nameof(ResultSummary));
        }
        catch (Exception exception)
        {
            SetError(exception);
        }

        RaiseCommandStates();
    }

    private async Task FirstScanAsync()
    {
        if (scanner is null || !TryGetSearchBytes(SelectedValueType, out var value))
        {
            return;
        }

        await RunScanAsync(async (progressReporter, cancellationToken) =>
        {
            scanSession = await scanner.FirstScanAsync(SelectedValueType, value,
                progressReporter, cancellationToken);
        });
    }

    private async Task NextScanAsync()
    {
        if (scanner is null || scanSession is null)
        {
            return;
        }

        byte[] exactValue;
        if (SelectedComparison == ComparisonMode.Exact)
        {
            if (!TryGetSearchBytes(scanSession.ValueType, out exactValue))
            {
                return;
            }
        }
        else
        {
            exactValue = new byte[ValueCodec.SizeOf(scanSession.ValueType)];
        }

        await RunScanAsync(async (progressReporter, cancellationToken) =>
        {
            scanSession = await scanner.NextScanAsync(scanSession, SelectedComparison,
                exactValue, progressReporter, cancellationToken);
        });
    }

    private async Task RunScanAsync(Func<IProgress<ScanProgress>, CancellationToken, Task> action)
    {
        scanCancellation?.Dispose();
        scanCancellation = new CancellationTokenSource();
        IsBusy = true;
        Progress = 0;
        Status = "Scanning readable memory…";
        var reporter = new Progress<ScanProgress>(scanProgress =>
        {
            Progress = scanProgress.Fraction * 100;
            Status = $"Scanning… {scanProgress.Fraction:P0} · {scanProgress.Candidates:N0} candidates";
        });

        try
        {
            await action(reporter, scanCancellation.Token);
            PopulateCandidates();
            Status = $"Scan complete. {(scanSession?.Candidates.Count ?? 0):N0} candidates remain.";
            OnPropertyChanged(nameof(HasScan));
            OnPropertyChanged(nameof(ResultSummary));
        }
        catch (OperationCanceledException)
        {
            Status = "Scan canceled. Previous completed results were preserved.";
        }
        finally
        {
            IsBusy = false;
            scanCancellation.Dispose();
            scanCancellation = null;
        }
    }

    private bool TryGetSearchBytes(ScanValueType type, out byte[] value)
    {
        if (ValueCodec.TryEncode(SearchValue, type, out value, out var error))
        {
            return true;
        }

        Status = error;
        return false;
    }

    private void PopulateCandidates()
    {
        Candidates.Clear();
        if (scanSession is null)
        {
            return;
        }

        foreach (var candidate in scanSession.Candidates.Take(MaximumDisplayedCandidates))
        {
            Candidates.Add(new CandidateViewModel(candidate,
                ValueCodec.Decode(candidate.LastValue, scanSession.ValueType)));
        }
    }

    private void WriteValue()
    {
        if (memory is null || SelectedCandidate is null || scanSession is null)
        {
            return;
        }

        if (!ValueCodec.TryEncode(WriteValue, scanSession.ValueType, out var value, out var error))
        {
            Status = error;
            return;
        }

        try
        {
            memory.Write(SelectedCandidate.Candidate.Address, value);
            Status = $"Wrote {WriteValue} to {SelectedCandidate.Address}.";
            var updated = new CandidateViewModel(
                new ScanCandidate(SelectedCandidate.Candidate.Address, value), WriteValue);
            var index = Candidates.IndexOf(SelectedCandidate);
            Candidates[index] = updated;
            SelectedCandidate = updated;
        }
        catch (Exception exception)
        {
            SetError(exception);
        }
    }

    private void AddDiscovery()
    {
        if (SelectedCandidate is null || scanSession is null)
        {
            return;
        }

        project.Discoveries.Add(new SavedDiscovery
        {
            Name = DiscoveryName.Trim(),
            LastKnownAddress = SelectedCandidate.Candidate.Address,
            LastKnownValue = SelectedCandidate.CurrentValue,
            ValueType = scanSession.ValueType
        });
        OnPropertyChanged(nameof(DiscoveryCount));
        Status = $"Saved {DiscoveryName.Trim()} to the current project.";
    }

    private async Task SaveProjectAsync()
    {
        var dialog = new SaveFileDialog
        {
            Title = "Save Trainer Studio project",
            Filter = "Trainer Studio project (*.trainerstudio.json)|*.trainerstudio.json",
            FileName = MakeSafeFileName(project.Name) + ".trainerstudio.json",
            AddExtension = true
        };
        if (dialog.ShowDialog() != true)
        {
            return;
        }

        await ProjectStore.SaveAsync(project, dialog.FileName);
        Status = $"Project saved to {dialog.FileName}.";
    }

    private async Task OpenProjectAsync()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Open Trainer Studio project",
            Filter = "Trainer Studio project (*.trainerstudio.json)|*.trainerstudio.json"
        };
        if (dialog.ShowDialog() != true)
        {
            return;
        }

        project = await ProjectStore.LoadAsync(dialog.FileName);
        OnPropertyChanged(nameof(ProjectName));
        OnPropertyChanged(nameof(DiscoveryCount));
        Status = $"Opened {project.Name}. Saved addresses are not assumed valid until re-tested.";
    }

    private void NewProject()
    {
        project = new TrainerProject { Name = $"Trainer Project {DateTime.Now:yyyy-MM-dd}" };
        OnPropertyChanged(nameof(ProjectName));
        OnPropertyChanged(nameof(DiscoveryCount));
        Status = "Created a new project.";
    }

    private static string MakeSafeFileName(string name)
    {
        foreach (var invalid in Path.GetInvalidFileNameChars())
        {
            name = name.Replace(invalid, '-');
        }

        return name;
    }

    private void SetError(Exception exception) => Status = $"Error: {exception.Message}";

    private void RaiseCommandStates()
    {
        RefreshProcessesCommand.RaiseCanExecuteChanged();
        AttachCommand.RaiseCanExecuteChanged();
        FirstScanCommand.RaiseCanExecuteChanged();
        NextScanCommand.RaiseCanExecuteChanged();
        CancelCommand.RaiseCanExecuteChanged();
        WriteValueCommand.RaiseCanExecuteChanged();
        AddDiscoveryCommand.RaiseCanExecuteChanged();
        SaveProjectCommand.RaiseCanExecuteChanged();
        OpenProjectCommand.RaiseCanExecuteChanged();
        NewProjectCommand.RaiseCanExecuteChanged();
    }

    public void Dispose()
    {
        scanCancellation?.Cancel();
        scanCancellation?.Dispose();
        memory?.Dispose();
    }
}
