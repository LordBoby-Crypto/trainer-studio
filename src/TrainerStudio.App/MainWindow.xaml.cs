using System.Windows;
using System.Windows.Threading;
using TrainerStudio.App.ViewModels;

namespace TrainerStudio.App;

public partial class MainWindow : Window
{
    private MainViewModel? viewModel;

    public MainWindow()
    {
        InitializeComponent();
        ContentRendered += HandleContentRendered;
    }

    private void HandleContentRendered(object? sender, EventArgs e)
    {
        ContentRendered -= HandleContentRendered;
        _ = Dispatcher.BeginInvoke(DispatcherPriority.Background,
            new Action(InitializeWorkspaceAsync));
    }

    private async void InitializeWorkspaceAsync()
    {
        try
        {
            StartupDiagnostics.Write("Initializing the workspace.");
            viewModel = new MainViewModel();
            DataContext = viewModel;
            await viewModel.InitializeAsync();
            StartupDiagnostics.Write("Workspace initialization completed.");
        }
        catch (Exception exception)
        {
            StartupDiagnostics.WriteException("Workspace initialization failed.", exception);
            MessageBox.Show(
                $"Trainer Studio opened, but its workspace could not be initialized.\n\n" +
                $"{exception.Message}\n\nDiagnostic log:\n{StartupDiagnostics.LogPath}",
                "Trainer Studio initialization error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        viewModel?.Dispose();
        base.OnClosed(e);
    }
}
