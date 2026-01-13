namespace BatchRunner.Models;

public class AppSettings : ObservableObject
{
    private bool _autoRetryFailedJobs;
    private bool _showConsoleWindow = true;

    public bool AutoRetryFailedJobs
    {
        get => _autoRetryFailedJobs;
        set => SetProperty(ref _autoRetryFailedJobs, value);
    }

    public bool ShowConsoleWindow
    {
        get => _showConsoleWindow;
        set => SetProperty(ref _showConsoleWindow, value);
    }
}
