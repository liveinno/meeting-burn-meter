using System.Windows;
using System.Windows.Input;

namespace Kostyor.App.Views;

/// <summary>Тёмный диалог «восстановить сессию?» в стиле приложения (вместо native MessageBox).</summary>
public partial class RestoreDialog : Window
{
    public RestoreDialog(string message)
    {
        InitializeComponent();
        MessageText.Text = message;
    }

    private void Card_Drag(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
        {
            try { DragMove(); } catch { /* ignore */ }
        }
    }

    private void Yes_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }

    private void No_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
