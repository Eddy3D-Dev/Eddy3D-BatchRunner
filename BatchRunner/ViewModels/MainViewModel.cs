using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
using System.Windows.Input;
using System.Windows.Threading;
using BatchRunner.Models;
using BatchRunner.Services;

using System.Windows;
using System.Windows.Shell;
using System.Diagnostics;

namespace BatchRunner.ViewModels;

public class MainViewModel : ObservableObject
{
    private readonly StateStore _stateStore;
    private readonly JobManager _jobManager;
    private readonly Dispatcher _dispatcher;
    private BatchFolder? _selectedFolder;
    private BatchJob? _selectedJob;
    private readonly DispatcherTimer _saveStateTimer;

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
            Settings.CompressCompletedCases = state.Settings.CompressCompletedCases;
        }

        Folders = new ObservableCollection<BatchFolder>(state.Folders ?? new List<BatchFolder>());
        NormalizeLoadedFolders();

        var logRoot = Path.Combine(Directory.GetCurrentDirectory(), "logs");
        _jobManager = new JobManager(Folders, _dispatcher, CpuInfo.GetPhysicalCoreCount(), logRoot)
        {
            AutoRetryFailedJobs = Settings.AutoRetryFailedJobs,
            ShowConsoleWindow = Settings.ShowConsoleWindow,
            CompressCompletedCases = Settings.CompressCompletedCases
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
        ExpandAllCommand = new RelayCommand(ExpandAll);
        CollapseAllCommand = new RelayCommand(CollapseAll);
        RemoveAllCommand = new RelayCommand(RemoveAll, CanRemoveAll);
        OpenLogCommand = new RelayCommand(OpenLog, CanOpenLog);

        UpdateCoreCounts();

        // ⚡ Bolt: Debounce state saving to prevent blocking the UI thread on high-frequency property changes
        _saveStateTimer = new DispatcherTimer(DispatcherPriority.Background, _dispatcher)
        {
            Interval = TimeSpan.FromMilliseconds(500)
        };
        _saveStateTimer.Tick += (s, e) => ExecuteSaveState();

        SaveState();

        _jobManager.QueueFinished += () => 
        {
            CommandManager.InvalidateRequerySuggested();
        };
    }

    public ObservableCollection<BatchFolder> Folders { get; }

    public bool HasFolders => Folders.Count > 0;

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

    public int TotalCores
    {
        get => _jobManager.TotalCores;
        set
        {
            if (_jobManager.TotalCores != value)
            {
                _jobManager.TotalCores = value;
                OnPropertyChanged(nameof(TotalCores));
                OnPropertyChanged(nameof(AvailableCores));
            }
        }
    }

    public int UsedCores => _jobManager.UsedCores;

    public int AvailableCores => _jobManager.AvailableCores;

    private TaskbarItemProgressState _taskbarProgressState = TaskbarItemProgressState.None;
    public TaskbarItemProgressState TaskbarProgressState
    {
        get => _taskbarProgressState;
        set => SetProperty(ref _taskbarProgressState, value);
    }

    private double _taskbarProgressValue;
    public double TaskbarProgressValue
    {
        get => _taskbarProgressValue;
        set => SetProperty(ref _taskbarProgressValue, value);
    }

    private void UpdateTaskbarState()
    {
        // ⚡ Bolt: Use a single O(N) pass to aggregate job statuses instead of multiple O(N) LINQ passes and a ToList() allocation.
        // This method is called frequently on property changes, so avoiding memory allocations reduces GC pressure.
        int total = 0;
        int completed = 0;
        int running = 0;
        int failed = 0;

        foreach (var folder in Folders)
        {
            foreach (var job in folder.Jobs)
            {
                total++;
                switch (job.Status)
                {
                    case JobStatus.Completed:
                        completed++;
                        break;
                    case JobStatus.Running:
                        running++;
                        break;
                    case JobStatus.Failed:
                        failed++;
                        break;
                }
            }
        }

        if (total == 0)
        {
            TaskbarProgressState = TaskbarItemProgressState.None;
            TaskbarProgressValue = 0;
            return;
        }

        TaskbarProgressValue = (double)completed / total;

        if (running > 0)
        {
            TaskbarProgressState = TaskbarItemProgressState.Normal;
        }
        else if (failed > 0)
        {
            TaskbarProgressState = TaskbarItemProgressState.Error;
        }
        else if (completed < total)
        {
            TaskbarProgressState = TaskbarItemProgressState.Paused;
        }
        else
        {
            TaskbarProgressState = TaskbarItemProgressState.None;
        }
    }

    public ICommand RemoveFolderCommand { get; }

    public ICommand CancelJobCommand { get; }

    public ICommand RestartJobCommand { get; }

    public ICommand StartQueueCommand { get; }
    
    public ICommand ExpandAllCommand { get; }

    public ICommand CollapseAllCommand { get; }

    public ICommand RemoveAllCommand { get; }

    public ICommand OpenLogCommand { get; }
    
    public bool AnyJobsRunning => Folders.Any(f => f.Jobs.Any(j => j.Status == JobStatus.Running));

    public void AddFolders(IEnumerable<string> paths)
    {
        var foldersToAdd = new List<string>();

        foreach (var path in paths)
        {
            var folderPath = File.Exists(path) ? Path.GetDirectoryName(path) : path;
            
            if (folderPath == null || !Directory.Exists(folderPath))
            {
                continue;
            }

            // Check if there is a "Scripts" folder inside. If so, use that instead.
            var scriptsPath = Path.Combine(folderPath, "Scripts");
            if (Directory.Exists(scriptsPath))
            {
                folderPath = scriptsPath;
            }

            // Check if folder is already in the queue
            if (Folders.Any(f => f.Path.Equals(folderPath, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            var summaryLog = Path.Combine(folderPath, "batch_runner_summary.log");
            var saveResultsLog = Path.Combine(folderPath, "save_results.log");

            // 1. Try to load from summary log (Rich data)
            if (File.Exists(summaryLog))
            {
                var folder = TryCreateCompletedFolderFromLog(folderPath, summaryLog);
                if (folder != null)
                {
                    Folders.Add(folder);
                    continue;
                }
            }

            // 2. Try to load from save_results.log (Fallback: Mark all as completed)
            // If we are in "Scripts", save_results.log might be in the parent (case root)
            var saveResultsLogParent = Directory.GetParent(folderPath)?.FullName is string parent 
                ? Path.Combine(parent, "save_results.log") 
                : null;

            if (File.Exists(saveResultsLog) || (saveResultsLogParent != null && File.Exists(saveResultsLogParent)))
            {
                var folder = CreateCompletedFolderGeneric(folderPath);
                Folders.Add(folder);
                continue;
            }

            // 3. Normal load (Queued)
            foldersToAdd.Add(folderPath); 
        }

        foreach (var folderPath in foldersToAdd)
        {
            var resultInfo = new DirectoryInfo(folderPath);
            var folderName = resultInfo.Name;
            if (folderName.Equals("Scripts", StringComparison.OrdinalIgnoreCase) && resultInfo.Parent != null)
            {
                folderName = resultInfo.Parent.Name;
            }
            var batchFiles = new[]
            {
                "run_mesh.bat",
                "symbolic_link_creator.bat",
                "run_sim_all.bat",
                "run_postprocess_U_all.bat",
                "save_results_to_dataset.bat",
                "delete_processor_folders.bat"
            };

            // Determine reference cores from run_mesh.bat
            var refCores = 1;
            var meshBat = Path.Combine(folderPath, "run_mesh.bat");
            if (File.Exists(meshBat))
            {
                refCores = BatchFileParser.GetRequiredCores(meshBat);
            }

            var jobs = new ObservableCollection<BatchJob>();
            
            foreach (var batchFile in batchFiles)
            {
                var fullPath = Path.Combine(folderPath, batchFile);
                if (File.Exists(fullPath))
                {
                    // Use reference cores for ALL jobs in this folder, per user request.
                    // "use meshing as reference ... allocate 8 for that folder"
                    
                    jobs.Add(new BatchJob
                    {
                        Id = Guid.NewGuid(),
                        BatPath = fullPath,
                        Name = batchFile, 
                        RequiredCores = refCores, 
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

    private BatchFolder? TryCreateCompletedFolderFromLog(string folderPath, string logPath)
    {
        try
        {
            var lines = File.ReadAllLines(logPath);
            var jobs = new ObservableCollection<BatchJob>();
            
            var resultInfo = new DirectoryInfo(folderPath);
            var folderName = resultInfo.Name;
            if (folderName.Equals("Scripts", StringComparison.OrdinalIgnoreCase) && resultInfo.Parent != null)
            {
                folderName = resultInfo.Parent.Name;
            }

            // Get consistency cores
            var refCores = GetReferenceCores(folderPath);

            for (int i = 0; i < lines.Length; i++)
            {
                var line = lines[i];
                // Look for job header: [Completed] jobname.bat
                if (line.StartsWith("[") && line.Contains("]"))
                {
                    var closingBracketIndex = line.IndexOf(']');
                    var statusStr = line.Substring(1, closingBracketIndex - 1);
                    var name = line.Substring(closingBracketIndex + 1).Trim();

                    if (!Enum.TryParse<JobStatus>(statusStr, out var status))
                    {
                        status = JobStatus.Completed;
                    }

                    // Look ahead for details
                    var duration = TimeSpan.Zero;
                    int exitCode = 0;
                    DateTimeOffset? startTs = null;
                    DateTimeOffset? endTs = null;
                    
                    // Possible next lines:
                    // 1. "    Start: ... | End: ..." (New format)
                    // 2. "    Time: ... | Exit: ..." (Both formats)
                    
                    int offset = 1;
                    while (i + offset < lines.Length)
                    {
                        var detailLine = lines[i + offset];
                        if (string.IsNullOrWhiteSpace(detailLine)) break; // End of block

                        if (detailLine.Contains("Start:") && detailLine.Contains("End:"))
                        {
                            var parts = detailLine.Split('|');
                            foreach (var p in parts)
                            {
                                if (p.Trim().StartsWith("Start:"))
                                {
                                    var s = p.Trim().Substring(6).Trim();
                                    if (DateTimeOffset.TryParse(s, out var dt)) startTs = dt;
                                }
                                if (p.Trim().StartsWith("End:"))
                                {
                                    var e = p.Trim().Substring(4).Trim();
                                    if (DateTimeOffset.TryParse(e, out var dt)) endTs = dt;
                                }
                            }
                        }
                        else if (detailLine.Contains("Time:") && detailLine.Contains("Exit:"))
                        {
                            var parts = detailLine.Split('|');
                            foreach (var p in parts)
                            {
                                if (p.Trim().StartsWith("Time:"))
                                {
                                    var timeStr = p.Trim().Substring(5).Trim();
                                    TimeSpan.TryParse(timeStr, out duration);
                                }
                                if (p.Trim().StartsWith("Exit:"))
                                {
                                    var exitStr = p.Trim().Substring(5).Trim();
                                    int.TryParse(exitStr, out exitCode);
                                }
                            }
                        }
                        offset++;
                    }

                    // Reconstruct job
                    var endedAt = endTs ?? DateTimeOffset.Now;
                    var startedAt = startTs ?? endedAt.Subtract(duration);
                    
                    var fullPath = Path.Combine(folderPath, name);

                    // Logic for cores: use refCores if > 1, else parse individual
                    var cores = refCores;
                    if (cores <= 1 && File.Exists(fullPath))
                    {
                        cores = BatchFileParser.GetRequiredCores(fullPath);
                    }

                    jobs.Add(new BatchJob
                    {
                        Id = Guid.NewGuid(),
                        BatPath = fullPath,
                        Name = name,
                        Status = status,
                        AddedAt = startedAt,
                        StartedAt = startedAt,
                        EndedAt = endedAt,
                        ExitCode = exitCode,
                        RequiredCores = cores
                        // LogPath is tricky, we can try to guess it or leave empty
                    });
                }
            }

            if (jobs.Any())
            {
                return new BatchFolder
                {
                    Id = Guid.NewGuid(),
                    Name = folderName,
                    Path = folderPath,
                    Jobs = jobs,
                    Status = JobStatus.Completed,
                    IsExpanded = false // Collapse completed by default? User opened it so maybe Expanded? User said "open a completed folder". Let's expand.
                };
            }
        }
        catch
        {
            // Parse error
        }
        return null;
    }

    private BatchFolder CreateCompletedFolderGeneric(string folderPath)
    {
        var resultInfo = new DirectoryInfo(folderPath);
        var folderName = resultInfo.Name;
        if (folderName.Equals("Scripts", StringComparison.OrdinalIgnoreCase) && resultInfo.Parent != null)
        {
            folderName = resultInfo.Parent.Name;
        }
        var batchFiles = new[]
        {
            "run_mesh.bat",
            "symbolic_link_creator.bat",
            "run_sim_all.bat",
            "run_postprocess_U_all.bat",
            "save_results_to_dataset.bat",
            "delete_processor_folders.bat"
        };

        var jobs = new ObservableCollection<BatchJob>();
        var refCores = GetReferenceCores(folderPath);
            
        foreach (var batchFile in batchFiles)
        {
            var fullPath = Path.Combine(folderPath, batchFile);
            if (File.Exists(fullPath))
            {
                var cores = refCores;
                if (cores <= 1)
                {
                    cores = BatchFileParser.GetRequiredCores(fullPath);
                }

                jobs.Add(new BatchJob
                {
                    Id = Guid.NewGuid(),
                    BatPath = fullPath,
                    Name = batchFile, 
                    RequiredCores = cores,
                    Status = JobStatus.Completed,
                    AddedAt = DateTimeOffset.Now,
                    StartedAt = DateTimeOffset.Now,
                    EndedAt = DateTimeOffset.Now,
                    ExitCode = 0
                });
            }
        }

        return new BatchFolder
        {
            Id = Guid.NewGuid(),
            Name = folderName,
            Path = folderPath,
            Jobs = jobs,
            Status = JobStatus.Completed,
            IsExpanded = true
        };
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

            // Use reference cores from the folder if run_mesh.bat exists
            var refCores = GetReferenceCores(dirPath);

            // If refCores is 1 (not found or actual 1), try individual file??
            // Actually, if run_mesh exists, we trust it. If not, maybe parse the file itself.
            // But to be safe and consistent with user request: if run_mesh exists, use it.
            // If NOT, then parse the individual file.
            
            var cores = refCores;
            if (cores == 1) // maybe run_mesh didn't exist
            {
                 cores = BatchFileParser.GetRequiredCores(path);
            }
            // Actually, user wants "24 processors for every one of the batch files"
            // implying they are in a folder that has a mesh file. 
            // If I just call GetReferenceCores, and it returns 1 (because no run_mesh), 
            // then we allow falling back to individual parsing. 
            // But if it finds run_mesh -> 24, we use 24.
            
            // Re-eval logic:
            // 1. Check for run_mesh in dir. 
            // 2. If valid (>1), use it.
            // 3. Else, parse file itself.

            if (cores == 1 && File.Exists(Path.Combine(dirPath, "run_mesh.bat")))
            {
                 // ensure we didn't miss it
                 cores = GetReferenceCores(dirPath);
            }
            
            var job = new BatchJob
            {
                Id = Guid.NewGuid(),
                BatPath = path,
                Name = Path.GetFileNameWithoutExtension(path),
                RequiredCores = cores,
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

            // Calculate reference cores once per folder
            var refCores = GetReferenceCores(folder.Path);

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

                // FIX: Use reference cores if available
                if (refCores > 1)
                {
                    job.RequiredCores = refCores;
                }
                else
                {
                    // Fallback to individual parsing if no run_mesh or run_mesh says 1
                    if (File.Exists(job.BatPath))
                    {
                        job.RequiredCores = BatchFileParser.GetRequiredCores(job.BatPath);
                    }
                    else if (job.RequiredCores < 1)
                    {
                        job.RequiredCores = 1;
                    }
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
        UpdateTaskbarState();
    }

    private static int GetReferenceCores(string folderPath)
    {
        try
        {
            var meshBat = Path.Combine(folderPath, "run_mesh.bat");
            if (File.Exists(meshBat))
            {
                return BatchFileParser.GetRequiredCores(meshBat);
            }
        }
        catch
        {
            // ignore
        }
        return 1;
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
        UpdateTaskbarState();
        SaveState();
        CommandManager.InvalidateRequerySuggested();
        OnPropertyChanged(nameof(HasFolders));
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
        UpdateTaskbarState();
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
        // Avoid redundant state-saving operations for high-frequency, calculated updates (e.g., from MonitorTimerOnTick)
        if (e.PropertyName == nameof(BatchJob.Duration))
        {
            return;
        }

        UpdateCoreCounts();
        UpdateTaskbarState();
        SaveState();
        CommandManager.InvalidateRequerySuggested();
    }

    private void SettingsOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        _jobManager.AutoRetryFailedJobs = Settings.AutoRetryFailedJobs;
        _jobManager.ShowConsoleWindow = Settings.ShowConsoleWindow;
        _jobManager.CompressCompletedCases = Settings.CompressCompletedCases;
        SaveState();
    }

    private void UpdateCoreCounts()
    {
        OnPropertyChanged(nameof(UsedCores));
        OnPropertyChanged(nameof(AvailableCores));
    }

    private void SaveState()
    {
        // ⚡ Bolt: Reset the timer to delay the actual save operation
        _saveStateTimer.Stop();
        _saveStateTimer.Start();
    }

    public void FlushState()
    {
        // ⚡ Bolt: Always stop the timer and save synchronously on exit.
        // This ensures any pending async saves complete (via SemaphoreSlim blocking)
        // and the final state is written before the process terminates.
        _saveStateTimer.Stop();
        var snapshot = CreateSnapshot();
        _stateStore.Save(snapshot);
    }

    private async void ExecuteSaveState()
    {
        _saveStateTimer.Stop();
        var snapshot = CreateSnapshot();

        try
        {
            await _stateStore.SaveAsync(snapshot);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to save state asynchronously: {ex.Message}");
        }
    }

    private RunnerState CreateSnapshot()
    {
        return new RunnerState
        {
            Folders = Folders.ToList(),
            Settings = new AppSettings
            {
                AutoRetryFailedJobs = Settings.AutoRetryFailedJobs,
                ShowConsoleWindow = Settings.ShowConsoleWindow,
                CompressCompletedCases = Settings.CompressCompletedCases
            }
        };
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

        bool hasRunningJobs = folder.Jobs.Any(j => j.Status == JobStatus.Running);
        if (hasRunningJobs)
        {
            var result = System.Windows.MessageBox.Show(
                $"Folder '{folder.Name}' has currently running jobs. Removing it will cancel them. Are you sure?",
                "Confirm Remove Folder",
                System.Windows.MessageBoxButton.YesNo,
                System.Windows.MessageBoxImage.Warning);

            if (result != System.Windows.MessageBoxResult.Yes)
            {
                return;
            }
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

        if (job.Status == JobStatus.Running)
        {
            var result = System.Windows.MessageBox.Show(
                $"Job '{job.Name}' is currently running. Are you sure you want to cancel it?",
                "Confirm Cancel Job",
                System.Windows.MessageBoxButton.YesNo,
                System.Windows.MessageBoxImage.Warning);

            if (result != System.Windows.MessageBoxResult.Yes)
            {
                return;
            }
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

             bool hasRunningJobs = folder.Jobs.Any(j => j.Status == JobStatus.Running);
             if (hasRunningJobs)
             {
                 var result = System.Windows.MessageBox.Show(
                     $"Folder '{folder.Name}' has currently running jobs. Restarting it will cancel them. Are you sure?",
                     "Confirm Restart Folder",
                     System.Windows.MessageBoxButton.YesNo,
                     System.Windows.MessageBoxImage.Warning);

                 if (result != System.Windows.MessageBoxResult.Yes)
                 {
                     return;
                 }
             }

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

        if (job.Status == JobStatus.Running)
        {
            var result = System.Windows.MessageBox.Show(
                $"Job '{job.Name}' is currently running. Are you sure you want to restart it?",
                "Confirm Restart Job",
                System.Windows.MessageBoxButton.YesNo,
                System.Windows.MessageBoxImage.Warning);

            if (result != System.Windows.MessageBoxResult.Yes)
            {
                return;
            }
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

    private void ExpandAll(object? parameter)
    {
        foreach (var folder in Folders)
        {
            folder.IsExpanded = true;
        }
    }

    private void CollapseAll(object? parameter)
    {
        foreach (var folder in Folders)
        {
            folder.IsExpanded = false;
        }
    }

    private void RemoveAll(object? parameter)
    {
        bool hasRunningJobs = Folders.Any(f => f.Jobs.Any(j => j.Status == JobStatus.Running));
        string warningText = hasRunningJobs
            ? "Are you sure you want to remove all folders? This will cancel all running jobs."
            : "Are you sure you want to remove all folders?";

        var result = System.Windows.MessageBox.Show(warningText, "Confirm Remove All", System.Windows.MessageBoxButton.YesNo, System.Windows.MessageBoxImage.Warning);
        if (result != System.Windows.MessageBoxResult.Yes)
        {
            return;
        }

        // Must convert to list because we are modifying collection
        var foldersCopy = Folders.ToList();
        
        foreach (var folder in foldersCopy)
        {
            // Cancel running jobs
            foreach(var job in folder.Jobs)
            {
                if (job.Status == JobStatus.Running)
                {
                    _jobManager.CancelJob(job);
                }
            }
        }
        
        Folders.Clear();
    }

    private bool CanRemoveAll(object? parameter)
    {
        return Folders.Any();
    }

    private void OpenLog(object? parameter)
    {
        if (parameter is string path && !string.IsNullOrWhiteSpace(path))
        {
            if (!File.Exists(path))
            {
                MessageBox.Show($"Log file not found: {path}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            try
            {
                Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Could not open log file: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private bool CanOpenLog(object? parameter)
    {
        return parameter is string path && !string.IsNullOrWhiteSpace(path);
    }
}
