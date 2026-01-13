using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows.Threading;
using BatchRunner.Models;

namespace BatchRunner.Services;

public class JobManager : IDisposable
{
    private const int AutoRetryLimit = 1;
    private readonly ObservableCollection<BatchJob> _jobs;
    private readonly Dispatcher _dispatcher;
    private readonly Dictionary<Guid, Process> _running = new();
    private readonly HashSet<Guid> _cancelRequested = new();
    private readonly HashSet<Guid> _restartRequested = new();
    private readonly string _logRoot;
    private bool _isScheduling;

    public JobManager(ObservableCollection<BatchJob> jobs, Dispatcher dispatcher, int totalCores, string logRoot)
    {
        _jobs = jobs;
        _dispatcher = dispatcher;
        TotalCores = totalCores;
        _logRoot = logRoot;
    }

    public int TotalCores { get; }

    public bool AutoRetryFailedJobs { get; set; }

    public bool ShowConsoleWindow { get; set; } = true;

    public bool IsQueueRunning { get; private set; }

    public int UsedCores => _jobs.Where(job => job.Status == JobStatus.Running).Sum(job => job.RequiredCores);

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
            while (true)
            {
                var next = _jobs.FirstOrDefault(job => job.Status == JobStatus.Queued);
                if (next is null)
                {
                    break;
                }

                if (next.RequiredCores > AvailableCores)
                {
                    break;
                }

                StartJob(next);
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
        TryStartJobs();
    }

    private void StartJob(BatchJob job)
    {
        if (job.Status != JobStatus.Queued)
        {
            return;
        }

        var logPath = CreateLogPath(job);
        job.Status = JobStatus.Running;
        job.StartedAt = DateTimeOffset.Now;
        job.EndedAt = null;
        job.ExitCode = null;
        job.LogPath = logPath;

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
                _dispatcher.Invoke(() => HandleProcessExit(job, process));
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

    private void HandleProcessExit(BatchJob job, Process process)
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
            AppendLogFooter(job, "Cancelled");
            TryStartJobs();
            return;
        }

        if (exitCode == 0)
        {
            job.Status = JobStatus.Completed;
            AppendLogFooter(job, "Completed");
            TryStartJobs();
            return;
        }

        job.Status = JobStatus.Failed;

        if (AutoRetryFailedJobs && job.RetryCount < AutoRetryLimit)
        {
            job.RetryCount++;
            AppendLogFooter(job, "Failed (auto retry)");
            ResetJob(job);
            job.Status = JobStatus.Queued;
            TryStartJobs();
            return;
        }

        AppendLogFooter(job, "Failed");
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

    private string CreateLogPath(BatchJob job)
    {
        Directory.CreateDirectory(_logRoot);
        var stamp = DateTimeOffset.Now.ToString("yyyyMMdd_HHmmss");
        var fileName = $"{stamp}_{SanitizeFileName(job.Name)}_{job.Id:N}.log";
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
