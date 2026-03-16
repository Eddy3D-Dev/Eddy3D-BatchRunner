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
    private readonly SemaphoreSlim _saveSemaphore = new(1, 1);

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

    public void Save(RunnerState state)
    {
        _saveSemaphore.Wait();
        try
        {
            var json = JsonSerializer.Serialize(state, _options);
            File.WriteAllText(_statePath, json);
        }
        finally
        {
            _saveSemaphore.Release();
        }
    }

    public async Task SaveAsync(RunnerState state)
    {
        // ⚡ Bolt: Serialize on the calling thread (UI) to prevent concurrent modification exceptions
        // while safely offloading the disk I/O to a background thread.
        var json = JsonSerializer.Serialize(state, _options);

        // ⚡ Bolt: Synchronize writes to prevent file-locking race conditions during high-frequency saves
        // Use ConfigureAwait(false) to prevent UI thread deadlocks if synchronous Save() is called during shutdown.
        await _saveSemaphore.WaitAsync().ConfigureAwait(false);
        try
        {
            await File.WriteAllTextAsync(_statePath, json).ConfigureAwait(false);
        }
        finally
        {
            _saveSemaphore.Release();
        }
    }
}
