namespace BatchRunner.Models;

public class RunnerState
{
    public List<BatchFolder>? Folders { get; set; } = new();
    public AppSettings Settings { get; set; } = new();
}
