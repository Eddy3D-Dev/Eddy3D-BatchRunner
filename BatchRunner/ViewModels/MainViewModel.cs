using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
using System.Windows.Input;
using System.Windows.Threading;
using BatchRunner.Models;
using BatchRunner.Services;

using System.Windows;

namespace BatchRunner.ViewModels;

public class MainViewModel : ObservableObject
{
    private readonly StateStore _stateStore;
    private readonly JobManager _jobManager;
    private readonly Dispatcher _dispatcher;
    private BatchFolder? _selectedFolder;
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

        Folders = new ObservableCollection<BatchFolder>(state.Folders ?? new List<BatchFolder>());
        NormalizeLoadedFolders();

        var logRoot = Path.Combine(Directory.GetCurrentDirectory(), "logs");
        _jobManager = new JobManager(Folders, _dispatcher, CpuInfo.GetPhysicalCoreCount(), logRoot)
        {
            AutoRetryFailedJobs = Settings.AutoRetryFailedJobs,
            ShowConsoleWindow = Settings.ShowConsoleWindow
        };

        Settings.PropertyChanged += SettingsOnPropertyChanged;

        Folders.CollectionChanged += FoldersOnCollectionChanged;
        foreach (var folder in Folders)
        {
            HookFolder(folder);
        }

        RemoveFolderCommand = new RelayCommand(RemoveFolder, CanRemoveFolder);
        CancelJobCommand = new RelayCommand(CancelJob, CanCancelJob);
        RestartJobCommand = new RelayCommand(RestartJob, CanRestartJob);
        StartQueueCommand = new RelayCommand(StartQueue, CanStartQueue);

        UpdateCoreCounts();
        SaveState();

        _jobManager.QueueFinished += () => 
        {
            CommandManager.InvalidateRequerySuggested();
        };
    }

    public ObservableCollection<BatchFolder> Folders { get; }

    public AppSettings Settings { get; }
    
    public BatchFolder? SelectedFolder
    {
        get => _selectedFolder;
        set => SetProperty(ref _selectedFolder, value);
    }

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

    public ICommand RemoveFolderCommand { get; }

    public ICommand CancelJobCommand { get; }

    public ICommand RestartJobCommand { get; }

    public ICommand StartQueueCommand { get; }

    public void AddFolders(IEnumerable<string> paths)
    {
        var completedFolders = new List<string>();
        var foldersToAdd = new List<string>();

        foreach (var path in paths)
        {
            // Path could be a file or folder. If file, take directory.
            var folderPath = File.Exists(path) ? Path.GetDirectoryName(path) : path;
            
            if (folderPath == null || !Directory.Exists(folderPath))
            {
                continue;
            }

            // Check if folder is already in the queue
            if (Folders.Any(f => f.Path.Equals(folderPath, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            if (CheckIfFolderIsCompleted(folderPath))
            {
                completedFolders.Add(folderPath);
            }
            foldersToAdd.Add(folderPath); // We collect valid paths first, then filtering or warning
        }

        if (completedFolders.Any())
        {
            var message = "The following folders are already completed (found 'batch_runner_summary.log' or 'save_results.log') and will be skipped:\n\n" + 
                          string.Join("\n", completedFolders.Select(p => new DirectoryInfo(p).Name));

            MessageBox.Show(message, "Completed Folders Skipped", MessageBoxButton.OK, MessageBoxImage.Information);            
            
            // Remove completed folders from the add list
            foldersToAdd.RemoveAll(f => completedFolders.Contains(f));
        }

        foreach (var folderPath in foldersToAdd)
        {
            var folderName = new DirectoryInfo(folderPath).Name;
            
            // Check for specific batch files in order
            var batchFiles = new[]
            {
                "run_mesh.bat",
                "symbolic_link_creator.bat",
                "run_sim_all.bat",
                "run_divU_all.bat",
                "save_results_to_dataset.bat"
            };

            var jobs = new ObservableCollection<BatchJob>();
            
            foreach (var batchFile in batchFiles)
            {
                var fullPath = Path.Combine(folderPath, batchFile);
                if (File.Exists(fullPath))
                {
                    jobs.Add(new BatchJob
                    {
                        Id = Guid.NewGuid(),
                        BatPath = fullPath,
                        Name = batchFile, // Or Path.GetFileNameWithoutExtension(batchFile)
                        RequiredCores = BatchFileParser.GetRequiredCores(fullPath),
                        Status = JobStatus.Queued,
                        AddedAt = DateTimeOffset.Now
                    });
                }
            }

            if (jobs.Any())
            {
                var folder = new BatchFolder
                {
                    Id = Guid.NewGuid(),
                    Name = folderName,
                    Path = folderPath,
                    Jobs = jobs,
                    Status = JobStatus.Queued,
                    IsExpanded = true
                };
                
                Folders.Add(folder);
            }
        }
    }

    private bool CheckIfFolderIsCompleted(string folderPath)
    {
        // Check for batch_runner_summary.log OR save_results.log
        var summaryLog = Path.Combine(folderPath, "batch_runner_summary.log");
        var saveResultsLog = Path.Combine(folderPath, "save_results.log");
        
        return File.Exists(summaryLog) || File.Exists(saveResultsLog);
    }

    public void AddBatchFiles(IEnumerable<string> paths)
    {
        foreach (var path in paths)
        {
            if (!File.Exists(path))
            {
                continue;
            }

            var folderName = Path.GetFileName(path); // Use file name as "folder" name for UI
            var dirPath = Path.GetDirectoryName(path) ?? path;

            var job = new BatchJob
            {
                Id = Guid.NewGuid(),
                BatPath = path,
                Name = Path.GetFileNameWithoutExtension(path),
                RequiredCores = BatchFileParser.GetRequiredCores(path),
                Status = JobStatus.Queued,
                AddedAt = DateTimeOffset.Now
            };

            var jobs = new ObservableCollection<BatchJob> { job };

            var folder = new BatchFolder
            {
                Id = Guid.NewGuid(),
                Name = folderName,
                Path = dirPath,
                Jobs = jobs,
                Status = JobStatus.Queued,
                IsExpanded = true
            };

            Folders.Add(folder);
        }
    }

    private void NormalizeLoadedFolders()
    {
        foreach (var folder in Folders)
        {
            if (folder.Id == Guid.Empty) folder.Id = Guid.NewGuid();
            
            foreach (var job in folder.Jobs)
            {
                if (job.Id == Guid.Empty)
                {
                    job.Id = Guid.NewGuid();
                }

                if (string.IsNullOrWhiteSpace(job.Name))
                {
                    job.Name = Path.GetFileName(job.BatPath);
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
                    folder.Status = JobStatus.Queued; // Reset folder too
                }
            }
        }
    }

    private void FoldersOnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.NewItems is not null)
        {
            foreach (BatchFolder folder in e.NewItems)
            {
                HookFolder(folder);
            }
        }

        if (e.OldItems is not null)
        {
            foreach (BatchFolder folder in e.OldItems)
            {
                UnhookFolder(folder);
            }
        }

        UpdateCoreCounts();
        SaveState();
        CommandManager.InvalidateRequerySuggested();
    }

    private void HookFolder(BatchFolder folder)
    {
        folder.PropertyChanged += FolderOnPropertyChanged;
        folder.Jobs.CollectionChanged += JobsOnCollectionChanged;
        foreach(var job in folder.Jobs)
        {
            HookJob(job);
        }
    }

    private void UnhookFolder(BatchFolder folder)
    {
        folder.PropertyChanged -= FolderOnPropertyChanged;
        folder.Jobs.CollectionChanged -= JobsOnCollectionChanged;
        foreach (var job in folder.Jobs)
        {
            UnhookJob(job);
        }
    }

    private void FolderOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        // Folder property changed
        SaveState();
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
            Folders = Folders.ToList(),
            Settings = new AppSettings
            {
                AutoRetryFailedJobs = Settings.AutoRetryFailedJobs,
                ShowConsoleWindow = Settings.ShowConsoleWindow
            }
        };

        _stateStore.Save(snapshot);
    }

    private void RemoveFolder(object? parameter)
    {
        var folder = parameter as BatchFolder ?? SelectedFolder;
        
        // If parameter is a job? No, RemoveFolder removes folders.
        // What if user wants to remove a job?
        // Let's assume user removes entire folder.
        
        if (folder is null)
        {
             // Check if SelectedJob is set, if so find its folder?
             // For now, let's keep it simple: Select a folder to remove it.
             return;
        }

        // Cancel running jobs in this folder
        foreach(var job in folder.Jobs)
        {
            if (job.Status == JobStatus.Running)
            {
                _jobManager.CancelJob(job);
            }
        }

        Folders.Remove(folder);
    }

    private bool CanRemoveFolder(object? parameter)
    {
        return parameter is BatchFolder || SelectedFolder is not null;
    }

    private void CancelJob(object? parameter)
    {
        // Parameter might be a Job (from button in row) or null (context menu/global button)
        var job = parameter as BatchJob ?? SelectedJob;
        
        if (job is null && SelectedFolder != null)
        {
             // Cancel all jobs in folder?
             // Let's focus on cancelling specific job or selected job.
        }

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
        
        if (job is null && SelectedFolder != null)
        {
             // Restart folder?
             // Logic for folder restart: reset all jobs in folder.
             var folder = SelectedFolder;
             foreach(var j in folder.Jobs)
             {
                 _jobManager.RestartJob(j); 
             }
             return;
        }

        if (job is null)
        {
            return;
        }

        _jobManager.RestartJob(job);
    }

    private bool CanRestartJob(object? parameter)
    {
        return parameter is BatchJob || SelectedJob is not null || SelectedFolder is not null;
    }

    private void StartQueue(object? parameter)
    {
        _jobManager.StartQueue();
        CommandManager.InvalidateRequerySuggested();
    }

    private bool CanStartQueue(object? parameter)
    {
        // Check if any folder has queued jobs
        return !_jobManager.IsQueueRunning && Folders.Any(f => f.Jobs.Any(j => j.Status == JobStatus.Queued || j.Status == JobStatus.Running));
    }
}
