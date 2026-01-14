using System.IO;

namespace BatchRunner.Models;

public class BatchJob : ObservableObject
{
    private Guid _id = Guid.NewGuid();
    private string _name = string.Empty;
    private string _batPath = string.Empty;
    private int _requiredCores = 1;
    private JobStatus _status = JobStatus.Queued;
    private DateTimeOffset _addedAt = DateTimeOffset.Now;
    private DateTimeOffset? _startedAt;
    private DateTimeOffset? _endedAt;
    private int? _exitCode;
    private string? _logPath;
    private int _retryCount;

    public Guid Id
    {
        get => _id;
        set => SetProperty(ref _id, value);
    }

    public string Name
    {
        get => _name;
        set => SetProperty(ref _name, value);
    }

    public string BatPath
    {
        get => _batPath;
        set => SetProperty(ref _batPath, value);
    }

    public int RequiredCores
    {
        get => _requiredCores;
        set => SetProperty(ref _requiredCores, value);
    }

    public JobStatus Status
    {
        get => _status;
        set => SetProperty(ref _status, value);
    }

    public DateTimeOffset AddedAt
    {
        get => _addedAt;
        set => SetProperty(ref _addedAt, value);
    }

    public DateTimeOffset? StartedAt
    {
        get => _startedAt;
        set
        {
            if (SetProperty(ref _startedAt, value))
            {
                OnPropertyChanged(nameof(Duration));
            }
        }
    }

    public DateTimeOffset? EndedAt
    {
        get => _endedAt;
        set
        {
            if (SetProperty(ref _endedAt, value))
            {
                OnPropertyChanged(nameof(Duration));
            }
        }
    }

    public int? ExitCode
    {
        get => _exitCode;
        set => SetProperty(ref _exitCode, value);
    }

    public string? LogPath
    {
        get => _logPath;
        set
        {
            if (SetProperty(ref _logPath, value))
            {
                OnPropertyChanged(nameof(LogFileName));
            }
        }
    }

    public int RetryCount
    {
        get => _retryCount;
        set => SetProperty(ref _retryCount, value);
    }

    public TimeSpan? Duration
    {
        get
        {
            if (StartedAt is null)
            {
                return null;
            }

            if (EndedAt is not null)
            {
                return EndedAt.Value - StartedAt.Value;
            }
            
            // Should we return current duration if running?
            if (Status == JobStatus.Running)
            {
                 return DateTimeOffset.Now - StartedAt.Value;
            }

            return null;
        }
    }
    
    public void RefreshDuration()
    {
        OnPropertyChanged(nameof(Duration));
    }

    public string? LogFileName => string.IsNullOrWhiteSpace(LogPath) ? null : Path.GetFileName(LogPath);
}
