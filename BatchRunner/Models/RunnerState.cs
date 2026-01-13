namespace BatchRunner.Models;

public class RunnerState
{
    public List<BatchJob> Jobs { get; set; } = new();
    public AppSettings Settings { get; set; } = new();
}
