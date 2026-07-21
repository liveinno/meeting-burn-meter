using System.Windows;
using System.Windows.Input;
using Kostyor.App.ViewModels;

namespace Kostyor.App.Views;

public partial class HistoryWindow : Window
{
    public HistoryWindow()
    {
        InitializeComponent();
        DataContextChanged += (_, _) =>
        {
            if (DataContext is HistoryViewModel vm)
                vm.RequestSavePath = AskSavePath;
        };
    }

    private static string? AskSavePath()
    {
        var dlg = new Microsoft.Win32.SaveFileDialog
        {
            Filter = "CSV (*.csv)|*.csv",
            FileName = $"kostyor_history_{DateTime.Now:yyyyMMdd}.csv",
            Title = "Экспорт истории встреч",
        };
        return dlg.ShowDialog() == true ? dlg.FileName : null;
    }

    private void Header_Drag(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
        {
            try { DragMove(); } catch { /* ignore */ }
        }
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
