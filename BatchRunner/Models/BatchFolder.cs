using System.Collections.ObjectModel;

namespace BatchRunner.Models;

public class BatchFolder : ObservableObject
{
    private Guid _id = Guid.NewGuid();
    private string _name = string.Empty;
    private string _path = string.Empty;
    private JobStatus _status = JobStatus.Queued;
    private ObservableCollection<BatchJob> _jobs = new();
    private bool _isExpanded;

    public Guid Id
    {
        get => _id;
        set => SetProperty(ref _id, value);
    }

    public string Name
    {
        get => _name;
        set => SetProperty(ref _name, value);
    }

    public string Path
    {
        get => _path;
        set => SetProperty(ref _path, value);
    }

    public JobStatus Status
    {
        get => _status;
        set => SetProperty(ref _status, value);
    }

    public ObservableCollection<BatchJob> Jobs
    {
        get => _jobs;
        set => SetProperty(ref _jobs, value);
    }
    
    public bool IsExpanded
    {
        get => _isExpanded;
        set => SetProperty(ref _isExpanded, value);
    }

    public int TotalCores => Jobs.Sum(j => j.RequiredCores);
}
