using System.Windows;
using System.Windows.Input;
using SmartVideoOptimizer.App.ViewModels;

namespace SmartVideoOptimizer.App;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    private MainViewModel? ViewModel => DataContext as MainViewModel;

    private void DropZone_DragOver(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            e.Effects = DragDropEffects.Copy;
            e.Handled = true;
        }
        else
        {
            e.Effects = DragDropEffects.None;
        }
    }

    private async void DropZone_Drop(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            var files = (string[])e.Data.GetData(DataFormats.FileDrop);
            if (files != null && files.Length > 0 && ViewModel != null)
            {
                var filePath = files[0];
                await ViewModel.LoadVideoAsync(filePath);
            }
        }
    }

    private void DropZone_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left && ViewModel != null)
        {
            if (ViewModel.BrowseFileCommand.CanExecute(null))
            {
                ViewModel.BrowseFileCommand.Execute(null);
            }
        }
    }
}