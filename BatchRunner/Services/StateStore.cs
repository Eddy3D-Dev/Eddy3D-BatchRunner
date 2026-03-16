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

    public void Save(RunnerState state, bool sync = false)
    {
        // ⚡ Bolt: Serialize synchronously on the UI thread to prevent InvalidOperationException
        // when accessing UI-bound ObservableCollections, but offload the File I/O.
        var json = JsonSerializer.Serialize(state, _options);

        if (sync)
        {
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
        else
        {
            _ = SaveAsync(json);
        }
    }

    private async Task SaveAsync(string json)
    {
        await _saveLock.WaitAsync().ConfigureAwait(false);
        try
        {
            // ⚡ Bolt: Offload file writing to a background task to prevent blocking the UI thread
            await File.WriteAllTextAsync(_statePath, json).ConfigureAwait(false);
        }
        catch
        {
            // Ignore I/O errors during background save
        }
        finally
        {
            _saveLock.Release();
        }
    }
}
