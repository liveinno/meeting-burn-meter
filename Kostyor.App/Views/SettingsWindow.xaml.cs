using System.Windows;
using System.Windows.Input;
using Kostyor.App.ViewModels;

namespace Kostyor.App.Views;

public partial class SettingsWindow : Window
{
    public SettingsWindow()
    {
        InitializeComponent();
    }

    private void Header_Drag(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
        {
            try { DragMove(); } catch { /* ignore */ }
        }
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        (DataContext as SettingsViewModel)?.SaveCommand.Execute(null);
        Close();
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
