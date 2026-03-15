using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using BatchRunner.Models;

namespace BatchRunner.Services;

public class StateStore
{
    private readonly string _statePath;
    private readonly JsonSerializerOptions _options;
    private readonly SemaphoreSlim _saveLock = new SemaphoreSlim(1, 1);

    public StateStore(string statePath)
    {
        _statePath = statePath;
        _options = new JsonSerializerOptions
        {
            WriteIndented = true,
            Converters = { new JsonStringEnumConverter() }
        };
    }

    public RunnerState Load()
    {
        if (!File.Exists(_statePath))
        {
            return new RunnerState();
        }

        try
        {
            var json = File.ReadAllText(_statePath);
            var state = JsonSerializer.Deserialize<RunnerState>(json, _options);
            return state ?? new RunnerState();
        }
        catch
        {
            return new RunnerState();
        }
    }

    public Task SaveAsync(RunnerState state)
    {
        // ⚡ Bolt: Serialize synchronously on the UI thread to prevent InvalidOperationException
        // from concurrent collection modifications during high-frequency property changes.
        var json = JsonSerializer.Serialize(state, _options);

        // Offload the disk I/O to a background thread to prevent blocking the UI
        return Task.Run(async () =>
        {
            await _saveLock.WaitAsync().ConfigureAwait(false);
            try
            {
                await File.WriteAllTextAsync(_statePath, json).ConfigureAwait(false);
            }
            finally
            {
                _saveLock.Release();
            }
        });
    }

    public void SaveSync(RunnerState state)
    {
        // Used for synchronous saves during application shutdown to ensure completion
        var json = JsonSerializer.Serialize(state, _options);

        _saveLock.Wait();
        try
        {
            File.WriteAllText(_statePath, json);
        }
        finally
        {
            _saveLock.Release();
        }
    }
}
