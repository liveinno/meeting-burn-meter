using System.Windows;

namespace Kostyor.App.Coaching;

/// <summary>
/// Шаг обучающего тура (ТЗ часть B). <see cref="Target"/> вычисляется в момент показа
/// (контрол мог быть скрыт/на другой вкладке); <see cref="Prepare"/> — подготовка перед подсветкой.
/// </summary>
public sealed class CoachStep
{
    public required string Title { get; init; }
    public required string Body { get; init; }

    /// <summary>Реальный контрол для подсветки (или null — карточка по центру без рамки).</summary>
    public Func<FrameworkElement?>? Target { get; init; }

    /// <summary>Необязательная подготовка перед показом шага (открыть панель, режим и т.п.).</summary>
    public Action? Prepare { get; init; }

    /// <summary>Действие при клике по подсвеченной цели. Если задано — шаг обязательный:
    /// его нельзя пропустить/закрыть, продвигается только этим кликом (обучение сворачиванию).</summary>
    public Action? OnHotspotClick { get; init; }

    /// <summary>true — шаг требует действия пользователя (клик по цели), а не кнопки «Далее».</summary>
    public bool RequireAction => OnHotspotClick is not null;
}
