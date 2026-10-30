using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Styling;
using Avalonia.Threading;

namespace PLDUOBAO.UI.Effects;

public class LineTextBlock : TemplatedControl
{
    public const string DarkNoTextSwapClass = "dark-no-text-swap";

    public static readonly StyledProperty<string> TextProperty =
        AvaloniaProperty.Register<LineTextBlock, string>(nameof(Text), string.Empty);

    public string Text
    {
        get => GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        UpdateDarkTextSwapClass();
        if (Application.Current != null)
            Application.Current.ActualThemeVariantChanged += OnAppThemeChanged;
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        if (Application.Current != null)
            Application.Current.ActualThemeVariantChanged -= OnAppThemeChanged;
        base.OnDetachedFromVisualTree(e);
    }

    protected override Size MeasureOverride(Size availableSize) =>
        base.MeasureOverride(new Size(double.PositiveInfinity, availableSize.Height));

    private void OnAppThemeChanged(object? sender, EventArgs e) =>
        Dispatcher.UIThread.Post(UpdateDarkTextSwapClass);

    private void UpdateDarkTextSwapClass()
    {
        Classes.Set(DarkNoTextSwapClass, IsEffectiveDarkTheme());
    }

    private bool IsEffectiveDarkTheme()
    {
        var theme = Application.Current?.ActualThemeVariant ?? ThemeVariant.Light;
        return theme == ThemeVariant.Dark;
    }
}
