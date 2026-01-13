using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using BatchRunner.Models;

namespace BatchRunner.Services;

public class StateStore
{
    private readonly string _statePath;
    private readonly JsonSerializerOptions _options;

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
        var json = JsonSerializer.Serialize(state, _options);
        File.WriteAllText(_statePath, json);
    }
}
