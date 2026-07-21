using System.Globalization;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Kostyor.Core.Formatting;
using Kostyor.App.Services;

namespace Kostyor.App.ViewModels;

/// <summary>
/// Строка роли в панели участников (1:1 с дизайном): редактируемые имя и ставка,
/// степпер «− N +», подсветка активной роли. Изменения дёргают <see cref="Changed"/> —
/// родитель пересчитывает ставку встречи (интегрально).
/// </summary>
public partial class RoleRowViewModel : ObservableObject
{
    public string Id { get; }
    public bool IsPreset { get; }

    /// <summary>Дёргается при смене состава/ставки/имени — родитель пересчитывает и сохраняет.</summary>
    public event Action? Changed;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsActive), nameof(CountBrush), nameof(RowBackground))]
    private int _count;

    [ObservableProperty] private string _name;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RateStr))]
    private decimal _ratePerHour;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(NotEditingName))]
    private bool _editingName;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(NotEditingRate))]
    private bool _editingRate;

    [ObservableProperty] private string _editBuffer = "";

    public RoleRowViewModel(string id, string name, decimal ratePerHour, int count, bool isPreset)
    {
        Id = id;
        _name = name;
        _ratePerHour = ratePerHour;
        _count = count;
        IsPreset = isPreset;
    }

    public bool IsActive => Count > 0;
    public bool NotEditingName => !EditingName;
    public bool NotEditingRate => !EditingRate;

    public string RateStr => RuFormat.Number(RatePerHour);

    // Цвета из дизайна.
    public Brush CountBrush => BrushCache.FromHex(Count > 0 ? "#6ee7b7" : "#64748b");
    public Brush RowBackground => Count > 0
        ? new SolidColorBrush(Color.FromArgb(0x17, 0x34, 0xd3, 0x99))   // rgba(52,211,153,.09)
        : new SolidColorBrush(Color.FromArgb(0x0a, 0xff, 0xff, 0xff));  // rgba(255,255,255,.04)

    [RelayCommand]
    private void Inc()
    {
        Count++;
        Changed?.Invoke();
    }

    [RelayCommand]
    private void Dec()
    {
        if (Count > 0) Count--;
        Changed?.Invoke();
    }

    [RelayCommand]
    private void StartEditName()
    {
        EditBuffer = Name;
        EditingRate = false;
        EditingName = true;
    }

    [RelayCommand]
    private void StartEditRate()
    {
        EditBuffer = RatePerHour.ToString(CultureInfo.InvariantCulture);
        EditingName = false;
        EditingRate = true;
    }

    [RelayCommand]
    private void Commit()
    {
        if (EditingName)
        {
            var nm = EditBuffer.Trim();
            if (!string.IsNullOrEmpty(nm)) Name = nm;
        }
        else if (EditingRate)
        {
            if (int.TryParse(EditBuffer.Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out var v) && v > 0)
                RatePerHour = v;
        }

        EditingName = false;
        EditingRate = false;
        Changed?.Invoke();
    }

    public void CancelEdit()
    {
        EditingName = false;
        EditingRate = false;
    }
}
