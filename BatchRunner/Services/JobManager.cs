using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows.Threading;
using BatchRunner.Models;

namespace BatchRunner.Services;

public class JobManager : IDisposable
{
    private const int AutoRetryLimit = 1;
    private readonly ObservableCollection<BatchFolder> _folders;
    private readonly Dispatcher _dispatcher;
    private readonly Dictionary<Guid, Process> _running = new();
    private readonly HashSet<Guid> _cancelRequested = new();
    private readonly HashSet<Guid> _restartRequested = new();
    private readonly string _logRoot;
    private int _watchdogTicks = 0;
    private readonly DispatcherTimer _monitorTimer;
    private bool _isScheduling;

    public JobManager(ObservableCollection<BatchFolder> folders, Dispatcher dispatcher, int totalCores, string logRoot)
    {
        _folders = folders;
        _dispatcher = dispatcher;
        TotalCores = totalCores;
        _logRoot = logRoot;

        _monitorTimer = new DispatcherTimer(DispatcherPriority.Background, dispatcher)
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _monitorTimer.Tick += MonitorTimerOnTick;
        _monitorTimer.Start();
    }

    private void MonitorTimerOnTick(object? sender, EventArgs e)
    {
        // 1. Update Duration for running jobs
        foreach (var folder in _folders)
        {
            foreach (var job in folder.Jobs)
            {
                if (job.Status == JobStatus.Running)
                {
                    job.RefreshDuration();
                }
            }
        }

        // 2. Process Watchdog (every 5 seconds)
        _watchdogTicks++;
        if (_watchdogTicks >= 5)
        {
            _watchdogTicks = 0;
            EnforceHighPriority();
        }
    }

    private void EnforceHighPriority()
    {
        // Target specific OpenFOAM/CFD processes
        var targetNames = new[] 
        { 
            "simpleFoam", 
            "blockMesh", 
            "snappyHexMesh", 
            "decomposePar", 
            "reconstructPar",
            "mpiexec", // MS-MPI
            "mpirun"   // OpenMPI
        };

        foreach (var name in targetNames)
        {
            try
            {
                var processes = Process.GetProcessesByName(name);
                foreach (var p in processes)
                {
                    try
                    {
                        if (!p.HasExited && p.PriorityClass != ProcessPriorityClass.High)
                        {
                            p.PriorityClass = ProcessPriorityClass.High;
                        }
                    }
                    catch
                    {
                        // Ignore access denied etc.
                    }
                    finally
                    {
                        p.Dispose();
                    }
                }
            }
            catch
            {
                // Ignore general errors
            }
        }
    }

    public int TotalCores { get; set; }

    public bool AutoRetryFailedJobs { get; set; }

    public bool ShowConsoleWindow { get; set; } = true;

    public bool IsQueueRunning { get; private set; }

    public int UsedCores => _folders.SelectMany(f => f.Jobs).Where(job => job.Status == JobStatus.Running).Sum(job => job.RequiredCores);

    public int AvailableCores => Math.Max(0, TotalCores - UsedCores);

    public void StartQueue()
    {
        IsQueueRunning = true;
        TryStartJobs();
    }

    public void PauseQueue()
    {
        IsQueueRunning = false;
    }

    public void TryStartJobs()
    {
        if (!IsQueueRunning)
        {
            return;
        }

        if (_isScheduling)
        {
            return;
        }

        _isScheduling = true;
        try
        {
            // Parallel logic:
            // Iterate all folders. If a folder can start a job (has queued jobs, no running jobs, previous jobs done),
            // and we have enough cores, start it.
            // Do NOT return after starting a job; success/failure in one folder shouldn't block others (unless resources full).

            foreach (var folder in _folders)
            {
                // 1. Strict sequential per folder: If any job is running in this folder, skip it.
                if (folder.Jobs.Any(j => j.Status == JobStatus.Running))
                {
                    continue; 
                }

                // 2. Check if this folder has failed/cancelled jobs -> Effectively "Done" (Stopped).
                if (folder.Jobs.Any(j => j.Status == JobStatus.Failed || j.Status == JobStatus.Cancelled))
                {
                    // This folder is dead. Move to next folder.
                    continue;
                }

                // 3. Find next queued job
                var nextJob = folder.Jobs.FirstOrDefault(j => j.Status == JobStatus.Queued);
                
                if (nextJob != null)
                {
                    // 4. Ensure all prior jobs in this folder are Completed
                    var index = folder.Jobs.IndexOf(nextJob);
                    var previousJobs = folder.Jobs.Take(index);
                    if (previousJobs.Any(j => j.Status != JobStatus.Completed))
                    {
                         // Blocked by a previous job that's not 'Completed' (though we checked Failed above)
                         // e.g. maybe some weird state. Just skip.
                        continue;
                    }

                    // 5. Check resources
                    if (nextJob.RequiredCores > AvailableCores)
                    {
                        // Not enough cores for this specific job right now.
                        // Skip this folder, try next folder (maybe it has a smaller job).
                        continue; 
                    }

                    // Start the job
                    StartJob(nextJob, folder);
                    
                    // Continue loop to see if we can start jobs in OTHER folders with remaining cores
                }
            }

            // After checking all folders:
            // If no jobs are running anywhere, AND no jobs are queued that could run...
            // Actually simpler: if no jobs are running, we are either done or stuck.
            
            var anyRunning = _folders.SelectMany(f => f.Jobs).Any(j => j.Status == JobStatus.Running);
            
            // If nothing is running, and we have queued jobs, it might be that they don't fit in TotalCores?
            // Or just that we finished everything.

            var anyQueued = _folders.SelectMany(f => f.Jobs).Any(j => j.Status == JobStatus.Queued);

            if (!anyRunning && !anyQueued)
            {
                // All done
                if (IsQueueRunning)
                {
                    IsQueueRunning = false;
                    _dispatcher.Invoke(() => QueueFinished?.Invoke());
                }
            }
        }
        finally
        {
            _isScheduling = false;
        }
    }

    public event Action? QueueFinished;

    public void CancelJob(BatchJob job)
    {
        if (job.Status == JobStatus.Running)
        {
            _cancelRequested.Add(job.Id);
            if (_running.TryGetValue(job.Id, out var process))
            {
                TryKillProcess(process);
            }
        }
        else if (job.Status == JobStatus.Queued)
        {
            job.Status = JobStatus.Cancelled;
            job.EndedAt = DateTimeOffset.Now;
        }
    }

    public void RestartJob(BatchJob job)
    {
        if (job.Status == JobStatus.Running)
        {
            _restartRequested.Add(job.Id);
            CancelJob(job);
            return;
        }

        ResetJob(job);
        job.Status = JobStatus.Queued;
        
        // Also reset subsequent jobs in the same folder?
        // If we restart a job in the middle, we probably want to re-run subsequent ones too if they were finished?
        // But for now, let's just reset this one. User can manually restart others if needed.
        
        TryStartJobs();
    }

    private void StartJob(BatchJob job, BatchFolder folder)
    {
        if (job.Status != JobStatus.Queued)
        {
            return;
        }

        var logPath = CreateLogPath(job, folder);
        job.Status = JobStatus.Running;
        job.StartedAt = DateTimeOffset.Now;
        job.EndedAt = null;
        job.ExitCode = null;
        job.LogPath = logPath;
        
        // Update folder status? 
        folder.Status = JobStatus.Running;

        WriteLogHeader(logPath, job);

        var startInfo = new ProcessStartInfo
        {
            FileName = "powershell.exe",
            Arguments = BuildPowerShellArguments(job.BatPath, logPath),
            WorkingDirectory = Path.GetDirectoryName(job.BatPath) ?? Directory.GetCurrentDirectory(),
            UseShellExecute = false,
            CreateNoWindow = !ShowConsoleWindow,
            WindowStyle = ShowConsoleWindow ? ProcessWindowStyle.Normal : ProcessWindowStyle.Hidden
        };

        var process = new Process
        {
            StartInfo = startInfo,
            EnableRaisingEvents = true
        };

        process.Exited += (_, _) =>
        {
            _ = Task.Run(async () =>
            {
                await WaitForDescendantsToExitAsync(process.Id);
                _dispatcher.Invoke(() => HandleProcessExit(job, folder, process));
            });
        };

        try
        {
            process.Start();
            try
            {
                process.PriorityClass = ProcessPriorityClass.High;
            }
            catch
            {
                // Ignore if unable to set priority (e.g. permissions)
            }
            _running[job.Id] = process;
        }
        catch (Exception ex)
        {
            AppendLogLine(logPath, $"Failed to start process: {ex.Message}");
            job.Status = JobStatus.Failed;
            folder.Status = JobStatus.Failed;
            job.EndedAt = DateTimeOffset.Now;
            TryStartJobs();
        }
    }

    private async Task WaitForDescendantsToExitAsync(int rootProcessId)
    {
        while (true)
        {
            var descendants = ProcessTree.GetDescendantProcessIds(rootProcessId);
            if (descendants.Count == 0)
            {
                break;
            }

            await Task.Delay(TimeSpan.FromSeconds(2));
        }
    }

    private void HandleProcessExit(BatchJob job, BatchFolder folder, Process process)
    {
        var exitCode = SafeGetExitCode(process);
        var wasCancelled = _cancelRequested.Remove(job.Id);
        var shouldRestart = _restartRequested.Remove(job.Id);

        _running.Remove(job.Id);

        job.EndedAt = DateTimeOffset.Now;
        job.ExitCode = exitCode;

        if (shouldRestart)
        {
            AppendLogFooter(job, "Restarted");
            ResetJob(job);
            job.Status = JobStatus.Queued;
            TryStartJobs();
            return;
        }

        if (wasCancelled)
        {
            job.Status = JobStatus.Cancelled;
            // Folder status? maybe Cancelled?
            folder.Status = JobStatus.Cancelled;
            AppendLogFooter(job, "Cancelled");
            TryStartJobs();
            return;
        }

        if (exitCode == 0)
        {
            job.Status = JobStatus.Completed;
            AppendLogFooter(job, "Completed");
            
            // Check if folder is completed
            if (folder.Jobs.All(j => j.Status == JobStatus.Completed))
            {
                folder.Status = JobStatus.Completed;
                WriteFolderSummaryLog(folder);
            }
            
            TryStartJobs();
            return;
        }

        job.Status = JobStatus.Failed;
        folder.Status = JobStatus.Failed;

        if (AutoRetryFailedJobs && job.RetryCount < AutoRetryLimit)
        {
            job.RetryCount++;
            AppendLogFooter(job, "Failed (auto retry)");
            ResetJob(job);
            job.Status = JobStatus.Queued;
            folder.Status = JobStatus.Running; // Back to running
            TryStartJobs();
            return;
        }

        AppendLogFooter(job, "Failed");
        // Don't auto-stop queue here, TryStartJobs will handle the stop logic if it sees a failed job.
        TryStartJobs();
    }

    private static int? SafeGetExitCode(Process process)
    {
        try
        {
            return process.ExitCode;
        }
        catch
        {
            return null;
        }
    }

    private string CreateLogPath(BatchJob job, BatchFolder folder)
    {
        Directory.CreateDirectory(_logRoot);
        var stamp = DateTimeOffset.Now.ToString("yyyyMMdd_HHmmss");
        var fileName = $"{stamp}_{SanitizeFileName(folder.Name)}_{SanitizeFileName(job.Name)}_{job.Id:N}.log";
        return Path.Combine(_logRoot, fileName);
    }

    private static string SanitizeFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var clean = new char[name.Length];
        var index = 0;

        foreach (var ch in name)
        {
            clean[index++] = invalid.Contains(ch) ? '_' : ch;
        }

        return new string(clean, 0, index);
    }

    private static void WriteLogHeader(string logPath, BatchJob job)
    {
        var lines = new[]
        {
            $"Started: {job.StartedAt:O}",
            $"Job: {job.Name}",
            $"Batch: {job.BatPath}",
            $"Cores: {job.RequiredCores}",
            "----------------------------------------"
        };

        File.WriteAllText(logPath, string.Join(Environment.NewLine, lines) + Environment.NewLine);
    }

    private static void AppendLogFooter(BatchJob job, string status)
    {
        if (string.IsNullOrWhiteSpace(job.LogPath))
        {
            return;
        }

        var exitCode = job.ExitCode?.ToString() ?? "unknown";
        var lines = new[]
        {
            string.Empty,
            $"Ended: {job.EndedAt:O}",
            $"Status: {status}",
            $"ExitCode: {exitCode}"
        };

        File.AppendAllText(job.LogPath, string.Join(Environment.NewLine, lines) + Environment.NewLine);
    }

    private static void AppendLogLine(string logPath, string message)
    {
        var lines = new[]
        {
            string.Empty,
            $"[{DateTimeOffset.Now:O}] {message}"
        };

        File.AppendAllText(logPath, string.Join(Environment.NewLine, lines) + Environment.NewLine);
    }

    private static string BuildPowerShellArguments(string batPath, string logPath)
    {
        var bat = QuoteForPowerShell(batPath);
        var log = QuoteForPowerShell(logPath);
        var command = $"& {bat} 2>&1 | Tee-Object -FilePath {log} -Append; exit $LASTEXITCODE";
        return $"-NoProfile -ExecutionPolicy Bypass -Command \"{command}\"";
    }

    private static string QuoteForPowerShell(string value)
    {
        var escaped = value.Replace("'", "''");
        return $"'{escaped}'";
    }

    private static void WriteFolderSummaryLog(BatchFolder folder)
    {
        try
        {
            var logPath = Path.Combine(folder.Path, "batch_runner_summary.log");
            var lines = new List<string>
            {
                "========================================",
                "       EDDY3D BATCH RUNNER SUMMARY      ",
                "========================================",
                "",
                $"Folder: {folder.Name}",
                $"Path:   {folder.Path}",
                $"Completed: {DateTimeOffset.Now:g}",
                "",
                "JOBS:",
                "-----"
            };

            foreach (var job in folder.Jobs)
            {
                var duration = job.EndedAt.HasValue && job.StartedAt.HasValue 
                    ? (job.EndedAt.Value - job.StartedAt.Value).ToString(@"hh\:mm\:ss") 
                    : "--:--:--";
                
                var status = job.Status.ToString();
                var exit = job.ExitCode?.ToString() ?? "-";
                
                // Format timestamps ISO 8601 for parsing
                var startStr = job.StartedAt?.ToString("O") ?? "-";
                var endStr = job.EndedAt?.ToString("O") ?? "-";

                lines.Add($"[{status}] {job.Name}");
                lines.Add($"    Start: {startStr} | End: {endStr}");
                lines.Add($"    Time: {duration} | Exit: {exit}");
                lines.Add("");
            }

            File.WriteAllText(logPath, string.Join(Environment.NewLine, lines));
        }
        catch
        {
            // Ignore failure to write log
        }
    }

    private static void TryKillProcess(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(true);
            }
        }
        catch
        {
        }
    }

    private static void ResetJob(BatchJob job)
    {
        job.StartedAt = null;
        job.EndedAt = null;
        job.ExitCode = null;
        job.LogPath = null;
    }

    public void Dispose()
    {
        foreach (var process in _running.Values)
        {
            TryKillProcess(process);
        }
        _monitorTimer.Stop();
    }
}
