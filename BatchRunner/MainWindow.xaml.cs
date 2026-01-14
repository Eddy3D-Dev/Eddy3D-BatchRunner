using System.IO;
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
    private BatchFolder? _draggedFolder;

    public MainWindow()
    {
        InitializeComponent();
        _viewModel = new MainViewModel();
        DataContext = _viewModel;
    }

    private void AddFolder_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog
        {
            Multiselect = true,
            Title = "Select Folders containing batch files"
        };

        if (dialog.ShowDialog() == true)
        {
            _viewModel.AddFolders(dialog.FolderNames);
        }
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
            _viewModel.AddBatchFiles(dialog.FileNames);
        }
    }

    private void FoldersList_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _dragStart = e.GetPosition(null);
        _draggedFolder = GetFolderFromEvent(e.OriginalSource);
    }

    private void FoldersList_MouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed || _draggedFolder is null)
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

        var data = new DataObject(typeof(BatchFolder), _draggedFolder);
        DragDrop.DoDragDrop(FoldersListItemsControl, data, DragDropEffects.Move);
    }

    private void FoldersList_DragOver(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            e.Effects = DragDropEffects.Copy;
        }
        else if (e.Data.GetDataPresent(typeof(BatchFolder)))
        {
            e.Effects = DragDropEffects.Move;
        }
        else
        {
            e.Effects = DragDropEffects.None;
        }

        e.Handled = true;
    }

    private void FoldersList_Drop(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            var files = (string[])(e.Data.GetData(DataFormats.FileDrop) ?? Array.Empty<string>());
            // AddFolders handles both files and folders (if file, it takes dirname)
            _viewModel.AddFolders(files);
            return;
        }

        if (e.Data.GetDataPresent(typeof(BatchFolder)))
        {
            if (e.Data.GetData(typeof(BatchFolder)) is not BatchFolder droppedFolder)
            {
                return;
            }

            var targetFolder = GetFolderFromEvent(e.OriginalSource);
            var fromIndex = _viewModel.Folders.IndexOf(droppedFolder);
            var toIndex = targetFolder is null ? _viewModel.Folders.Count - 1 : _viewModel.Folders.IndexOf(targetFolder);

            if (fromIndex != -1 && toIndex != -1)
            {
                 _viewModel.Folders.Move(fromIndex, toIndex);
            }
        }
    }

    private static BatchFolder? GetFolderFromEvent(object? source)
    {
        // Source might be inside the Expander header or content.
        // We want the FrameworkElement that has DataContext as BatchFolder.
        // But likely we are using ItemsControl, so we look for ContentPresenter.
        
        var element = source as FrameworkElement;
        if (element?.DataContext is BatchFolder folder)
        {
            return folder;
        }
        
        // Walk up to find something with BatchFolder context
        var current = source as DependencyObject;
        while (current != null)
        {
            if (current is FrameworkElement fe && fe.DataContext is BatchFolder f)
            {
                return f;
            }
            current = VisualTreeHelper.GetParent(current);
        }
        return null;
    }
}
