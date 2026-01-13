using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
using System.Windows.Input;
using System.Windows.Threading;
using BatchRunner.Models;
using BatchRunner.Services;

namespace BatchRunner.ViewModels;

public class MainViewModel : ObservableObject
{
    private readonly StateStore _stateStore;
    private readonly JobManager _jobManager;
    private readonly Dispatcher _dispatcher;
    private BatchJob? _selectedJob;

    public MainViewModel()
    {
        _dispatcher = Dispatcher.CurrentDispatcher;
        _stateStore = new StateStore(Path.Combine(Directory.GetCurrentDirectory(), "batchrunner_state.json"));
        Settings = new AppSettings();

        var state = _stateStore.Load();
        if (state.Settings is not null)
        {
            Settings.AutoRetryFailedJobs = state.Settings.AutoRetryFailedJobs;
            Settings.ShowConsoleWindow = state.Settings.ShowConsoleWindow;
        }

        Jobs = new ObservableCollection<BatchJob>(state.Jobs ?? new List<BatchJob>());
        NormalizeLoadedJobs();

        var logRoot = Path.Combine(Directory.GetCurrentDirectory(), "logs");
        _jobManager = new JobManager(Jobs, _dispatcher, CpuInfo.GetPhysicalCoreCount(), logRoot)
        {
            AutoRetryFailedJobs = Settings.AutoRetryFailedJobs,
            ShowConsoleWindow = Settings.ShowConsoleWindow
        };

        Settings.PropertyChanged += SettingsOnPropertyChanged;

        Jobs.CollectionChanged += JobsOnCollectionChanged;
        foreach (var job in Jobs)
        {
            HookJob(job);
        }

        RemoveJobCommand = new RelayCommand(RemoveJob, CanRemoveJob);
        CancelJobCommand = new RelayCommand(CancelJob, CanCancelJob);
        RestartJobCommand = new RelayCommand(RestartJob, CanRestartJob);
        StartQueueCommand = new RelayCommand(StartQueue, CanStartQueue);

        UpdateCoreCounts();
        SaveState();
    }

    public ObservableCollection<BatchJob> Jobs { get; }

    public AppSettings Settings { get; }

    public BatchJob? SelectedJob
    {
        get => _selectedJob;
        set
        {
            if (SetProperty(ref _selectedJob, value))
            {
                CommandManager.InvalidateRequerySuggested();
            }
        }
    }

    public int TotalCores => _jobManager.TotalCores;

    public int UsedCores => _jobManager.UsedCores;

    public int AvailableCores => _jobManager.AvailableCores;

    public ICommand RemoveJobCommand { get; }

    public ICommand CancelJobCommand { get; }

    public ICommand RestartJobCommand { get; }

    public ICommand StartQueueCommand { get; }

    public void AddJobs(IEnumerable<string> paths)
    {
        foreach (var path in paths)
        {
            if (!File.Exists(path))
            {
                continue;
            }

            var job = new BatchJob
            {
                Id = Guid.NewGuid(),
                BatPath = path,
                Name = Path.GetFileNameWithoutExtension(path),
                RequiredCores = BatchFileParser.GetRequiredCores(path),
                Status = JobStatus.Queued,
                AddedAt = DateTimeOffset.Now
            };

            Jobs.Add(job);
        }
    }

    public void MoveJob(int fromIndex, int toIndex)
    {
        if (fromIndex == toIndex || fromIndex < 0 || toIndex < 0)
        {
            return;
        }

        if (fromIndex >= Jobs.Count || toIndex >= Jobs.Count)
        {
            return;
        }

        Jobs.Move(fromIndex, toIndex);
    }

    private void NormalizeLoadedJobs()
    {
        foreach (var job in Jobs)
        {
            if (job.Id == Guid.Empty)
            {
                job.Id = Guid.NewGuid();
            }

            if (string.IsNullOrWhiteSpace(job.Name))
            {
                job.Name = Path.GetFileNameWithoutExtension(job.BatPath);
            }

            if (File.Exists(job.BatPath))
            {
                job.RequiredCores = BatchFileParser.GetRequiredCores(job.BatPath);
            }
            else if (job.RequiredCores < 1)
            {
                job.RequiredCores = 1;
            }

            if (job.AddedAt == default)
            {
                job.AddedAt = DateTimeOffset.Now;
            }

            if (job.Status == JobStatus.Running)
            {
                job.Status = JobStatus.Queued;
                job.StartedAt = null;
                job.EndedAt = null;
                job.ExitCode = null;
                job.LogPath = null;
            }
        }
    }

    private void JobsOnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.NewItems is not null)
        {
            foreach (BatchJob job in e.NewItems)
            {
                HookJob(job);
            }
        }

        if (e.OldItems is not null)
        {
            foreach (BatchJob job in e.OldItems)
            {
                UnhookJob(job);
            }
        }

        UpdateCoreCounts();
        SaveState();
        CommandManager.InvalidateRequerySuggested();
    }

    private void HookJob(BatchJob job)
    {
        job.PropertyChanged += JobOnPropertyChanged;
    }

    private void UnhookJob(BatchJob job)
    {
        job.PropertyChanged -= JobOnPropertyChanged;
    }

    private void JobOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        UpdateCoreCounts();
        SaveState();
        CommandManager.InvalidateRequerySuggested();
    }

    private void SettingsOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        _jobManager.AutoRetryFailedJobs = Settings.AutoRetryFailedJobs;
        _jobManager.ShowConsoleWindow = Settings.ShowConsoleWindow;
        SaveState();
    }

    private void UpdateCoreCounts()
    {
        OnPropertyChanged(nameof(UsedCores));
        OnPropertyChanged(nameof(AvailableCores));
    }

    private void SaveState()
    {
        var snapshot = new RunnerState
        {
            Jobs = Jobs.ToList(),
            Settings = new AppSettings
            {
                AutoRetryFailedJobs = Settings.AutoRetryFailedJobs,
                ShowConsoleWindow = Settings.ShowConsoleWindow
            }
        };

        _stateStore.Save(snapshot);
    }

    private void RemoveJob(object? parameter)
    {
        var job = parameter as BatchJob ?? SelectedJob;
        if (job is null)
        {
            return;
        }

        if (job.Status == JobStatus.Running)
        {
            _jobManager.CancelJob(job);
        }

        Jobs.Remove(job);
    }

    private bool CanRemoveJob(object? parameter)
    {
        return parameter is BatchJob || SelectedJob is not null;
    }

    private void CancelJob(object? parameter)
    {
        var job = parameter as BatchJob ?? SelectedJob;
        if (job is null)
        {
            return;
        }

        _jobManager.CancelJob(job);
    }

    private bool CanCancelJob(object? parameter)
    {
        var job = parameter as BatchJob ?? SelectedJob;
        return job is not null && (job.Status == JobStatus.Running || job.Status == JobStatus.Queued);
    }

    private void RestartJob(object? parameter)
    {
        var job = parameter as BatchJob ?? SelectedJob;
        if (job is null)
        {
            return;
        }

        _jobManager.RestartJob(job);
    }

    private bool CanRestartJob(object? parameter)
    {
        return parameter is BatchJob || SelectedJob is not null;
    }

    private void StartQueue(object? parameter)
    {
        _jobManager.StartQueue();
        CommandManager.InvalidateRequerySuggested();
    }

    private bool CanStartQueue(object? parameter)
    {
        return !_jobManager.IsQueueRunning && Jobs.Any(job => job.Status == JobStatus.Queued);
    }
}
