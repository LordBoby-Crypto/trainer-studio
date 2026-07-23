using System.Windows;
using System.Windows.Threading;

namespace TrainerStudio.App;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        DispatcherUnhandledException += HandleDispatcherException;
        AppDomain.CurrentDomain.UnhandledException += HandleDomainException;
        TaskScheduler.UnobservedTaskException += HandleUnobservedTaskException;
        StartupDiagnostics.BeginSession();

        try
        {
            StartupDiagnostics.Write("Creating the main window.");
            var window = new MainWindow();
            MainWindow = window;
            window.Show();
            StartupDiagnostics.Write("The main window was shown successfully.");
        }
        catch (Exception exception)
        {
            StartupDiagnostics.WriteException("Trainer Studio failed during startup.", exception);
            ShowFatalError(exception);
            Shutdown(1);
        }
    }

    private void HandleDispatcherException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        StartupDiagnostics.WriteException("Unhandled UI exception.", e.Exception);
        e.Handled = true;
        ShowFatalError(e.Exception);
    }

    private static void HandleDomainException(object? sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception exception)
        {
            StartupDiagnostics.WriteException("Unhandled application exception.", exception);
        }
        else
        {
            StartupDiagnostics.Write($"Unhandled application error: {e.ExceptionObject}");
        }
    }

    private static void HandleUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        StartupDiagnostics.WriteException("Unobserved background task exception.", e.Exception);
        e.SetObserved();
    }

    private static void ShowFatalError(Exception exception)
    {
        var message =
            $"Trainer Studio encountered an error.\n\n{exception.Message}\n\n" +
            $"A diagnostic log was written to:\n{StartupDiagnostics.LogPath}";

        try
        {
            MessageBox.Show(message, "Trainer Studio error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        catch
        {
            // The diagnostic file remains available if Windows cannot display a message box.
        }
    }
}
