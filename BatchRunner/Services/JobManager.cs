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
    private bool _isScheduling;

    public JobManager(ObservableCollection<BatchFolder> folders, Dispatcher dispatcher, int totalCores, string logRoot)
    {
        _folders = folders;
        _dispatcher = dispatcher;
        TotalCores = totalCores;
        _logRoot = logRoot;
    }

    public int TotalCores { get; }

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
            // Sequential logic:
            // 1. Find the first folder that is not fully completed.
            // 2. In that folder, find the first job that is Queued.
            // 3. Ensure no other job is running in that folder (strict sequential per folder).
            // 4. Ensure we are not running a job from a previous folder (strict sequential folders).

            // Actually, simply: iterate folders in order.
            foreach (var folder in _folders)
            {
                // If folder has failed jobs and we are not retrying, or just generally if it's "blocked", maybe we stop?
                // For now, let's assume we proceed unless specific stop condition.
                // But user asked: "after completing one folder, it should go with the next one".
                
                // Check if this folder has any running jobs. If so, we can't start another one in this folder (sequential).
                if (folder.Jobs.Any(j => j.Status == JobStatus.Running))
                {
                    // A job is running in this folder. We wait.
                    // And since we do folders sequentially, we don't look at next folders.
                    return; 
                }

                // Check if this folder has incomplete jobs.
                var nextJob = folder.Jobs.FirstOrDefault(j => j.Status == JobStatus.Queued);
                
                if (nextJob != null)
                {
                    // Found a job to run in this folder.
                    // Check if previous jobs in this folder are all completed (or skipped/failed if that allows proceeding?)
                    // The requirement says "wait for the former step to be completed". 
                    // So we must ensure all prior jobs are Completed.
                    
                    var index = folder.Jobs.IndexOf(nextJob);
                    var previousJobs = folder.Jobs.Take(index);
                    if (previousJobs.Any(j => j.Status != JobStatus.Completed))
                    {
                        // Previous jobs not completed (maybe Failed or Cancelled). 
                        // If Failed/Cancelled, do we stop? 
                        // Usually in strict sequence, failure stops the chain.
                        // Let's assume we stop.
                        return;
                    }

                    // Check resources
                    if (nextJob.RequiredCores > AvailableCores)
                    {
                        // Not enough cores. We wait. 
                        // (Since we are strictly sequential, we don't try to skip to next folder)
                        return; 
                    }

                    StartJob(nextJob, folder);
                    return; // Started a job, exit.
                }

                // If no queued jobs, maybe the folder is done?
                // Check if any job failed in this folder? 
                if (folder.Jobs.Any(j => j.Status == JobStatus.Failed || j.Status == JobStatus.Cancelled))
                {
                    // Folder failed. Do we proceed to next folder? 
                    // User said "after completing one folder". Implies success. 
                    // But if it fails, maybe we should stop everything?
                    // Let's be safe: if a folder failed, we stop the queue.
                    IsQueueRunning = false; 
                    return;
                }
                
                // If all jobs completed in this folder, we loop to the next folder.
            }
        }
        finally
        {
            _isScheduling = false;
        }
    }

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
    }
}
