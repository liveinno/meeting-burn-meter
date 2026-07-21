using CommunityToolkit.Mvvm.ComponentModel;

namespace Kostyor.App.ViewModels;

/// <summary>Веха-тост внутри окна (ТЗ §3): иконка + текст, автоскрытие ~1.9 с.</summary>
public partial class ToastViewModel : ObservableObject
{
    public string Id { get; }

    [ObservableProperty] private string _icon = "";
    [ObservableProperty] private string _text = "";

    public ToastViewModel(string id, string icon, string text)
    {
        Id = id;
        _icon = icon;
        _text = text;
    }
}
