using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using BatchRunner.Models;
using BatchRunner.ViewModels;
using Microsoft.Win32;

namespace BatchRunner;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;
    private Point _dragStart;
    private BatchJob? _draggedJob;

    public MainWindow()
    {
        InitializeComponent();
        _viewModel = new MainViewModel();
        DataContext = _viewModel;
    }

    private void AddJobs_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Filter = "Batch files (*.bat)|*.bat|All files (*.*)|*.*",
            Multiselect = true
        };

        if (dialog.ShowDialog() == true)
        {
            _viewModel.AddJobs(dialog.FileNames);
        }
    }

    private void JobsGrid_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _dragStart = e.GetPosition(null);
        _draggedJob = GetJobFromEvent(e.OriginalSource);
    }

    private void JobsGrid_MouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed || _draggedJob is null)
        {
            return;
        }

        var position = e.GetPosition(null);
        var dx = Math.Abs(position.X - _dragStart.X);
        var dy = Math.Abs(position.Y - _dragStart.Y);

        if (dx < SystemParameters.MinimumHorizontalDragDistance &&
            dy < SystemParameters.MinimumVerticalDragDistance)
        {
            return;
        }

        var data = new DataObject(typeof(BatchJob), _draggedJob);
        DragDrop.DoDragDrop(JobsGrid, data, DragDropEffects.Move);
    }

    private void JobsGrid_DragOver(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            e.Effects = DragDropEffects.Copy;
        }
        else if (e.Data.GetDataPresent(typeof(BatchJob)))
        {
            e.Effects = DragDropEffects.Move;
        }
        else
        {
            e.Effects = DragDropEffects.None;
        }

        e.Handled = true;
    }

    private void JobsGrid_Drop(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            var files = (string[])(e.Data.GetData(DataFormats.FileDrop) ?? Array.Empty<string>());
            var batFiles = files.Where(file => file.EndsWith(".bat", StringComparison.OrdinalIgnoreCase));
            _viewModel.AddJobs(batFiles);
            return;
        }

        if (e.Data.GetDataPresent(typeof(BatchJob)))
        {
            if (e.Data.GetData(typeof(BatchJob)) is not BatchJob droppedJob)
            {
                return;
            }

            var targetJob = GetJobFromEvent(e.OriginalSource);
            var fromIndex = _viewModel.Jobs.IndexOf(droppedJob);
            var toIndex = targetJob is null ? _viewModel.Jobs.Count - 1 : _viewModel.Jobs.IndexOf(targetJob);

            _viewModel.MoveJob(fromIndex, toIndex);
        }
    }

    private static BatchJob? GetJobFromEvent(object? source)
    {
        var row = FindAncestor<DataGridRow>(source as DependencyObject);
        return row?.Item as BatchJob;
    }

    private static T? FindAncestor<T>(DependencyObject? current) where T : DependencyObject
    {
        while (current is not null)
        {
            if (current is T match)
            {
                return match;
            }

            current = VisualTreeHelper.GetParent(current);
        }

        return null;
    }
}
