using System;
using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Media;

namespace PLDUOBAO.UI.Effects;

public static class ButtonHalo
{
    private const double RingSize = 160;

    public static readonly AttachedProperty<bool> IsEnabledProperty =
        AvaloniaProperty.RegisterAttached<Control, bool>("IsEnabled", typeof(ButtonHalo));

    public static void SetIsEnabled(Control element, bool value) => element.SetValue(IsEnabledProperty, value);
    public static bool GetIsEnabled(Control element) => element.GetValue(IsEnabledProperty);

    private sealed class HaloState
    {
        public Border? Overlay;
        public Ellipse? Ring;
    }

    private static readonly ConditionalWeakTable<Control, HaloState> States = new();

    static ButtonHalo()
    {
        IsEnabledProperty.Changed.AddClassHandler<Control>((control, e) =>
        {
            if (e.NewValue is true)
                Attach(control);
            else
                Detach(control);
        });
    }

    private static void Attach(Control control)
    {
        States.GetOrCreateValue(control);
        control.PointerEntered += OnPointerEntered;
        control.PointerExited += OnPointerExited;
        control.PointerMoved += OnPointerMoved;
        control.DetachedFromVisualTree += OnDetached;
    }

    private static void Detach(Control control)
    {
        control.PointerEntered -= OnPointerEntered;
        control.PointerExited -= OnPointerExited;
        control.PointerMoved -= OnPointerMoved;
        control.DetachedFromVisualTree -= OnDetached;
        RemoveOverlay(control);
    }

    private static void OnDetached(object? sender, VisualTreeAttachmentEventArgs e)
    {
        if (sender is Control control)
            RemoveOverlay(control);
    }

    private static void OnPointerEntered(object? sender, PointerEventArgs e)
    {
        if (sender is not Control control || !control.IsEffectivelyEnabled)
            return;

        var layer = AdornerLayer.GetAdornerLayer(control);
        if (layer is null || !States.TryGetValue(control, out var state))
            return;

        if (state.Overlay is null)
        {
            var ring = new Ellipse
            {
                Width = RingSize,
                Height = RingSize,
                IsHitTestVisible = false,
                Fill = new RadialGradientBrush
                {
                    Center = new RelativePoint(0.5, 0.5, RelativeUnit.Relative),
                    GradientOrigin = new RelativePoint(0.5, 0.5, RelativeUnit.Relative),
                    GradientStops =
                    {
                        new GradientStop(Color.FromArgb(0x99, 0x80, 0x80, 0x80), 0),
                        new GradientStop(Colors.Transparent, 0.8)
                    }
                }
            };

            var canvas = new Canvas { ClipToBounds = true, IsHitTestVisible = false };
            canvas.Children.Add(ring);

            var overlay = new Border
            {
                ClipToBounds = true,
                IsHitTestVisible = false,
                Opacity = 0,
                CornerRadius = GetCornerRadius(control),
                Child = canvas,
                Transitions = new Transitions
                {
                    new DoubleTransition
                    {
                        Property = Visual.OpacityProperty,
                        Duration = TimeSpan.FromMilliseconds(180),
                        Easing = new QuinticEaseOut()
                    }
                }
            };

            state.Overlay = overlay;
            state.Ring = ring;
        }

        state.Overlay.CornerRadius = GetCornerRadius(control);

        if (!layer.Children.Contains(state.Overlay))
        {
            layer.Children.Add(state.Overlay);
            AdornerLayer.SetAdornedElement(state.Overlay, control);
            AdornerLayer.SetIsClipEnabled(state.Overlay, true);
        }

        UpdateRing(state, e.GetPosition(control));
        state.Overlay.Opacity = 0.45;
    }

    private static void OnPointerMoved(object? sender, PointerEventArgs e)
    {
        if (sender is Control control && States.TryGetValue(control, out var state) && state.Overlay is not null)
            UpdateRing(state, e.GetPosition(control));
    }

    private static void OnPointerExited(object? sender, PointerEventArgs e)
    {
        if (sender is Control control && States.TryGetValue(control, out var state) && state.Overlay is not null)
            state.Overlay.Opacity = 0;
    }

    private static void UpdateRing(HaloState state, Point position)
    {
        if (state.Ring is null)
            return;
        Canvas.SetLeft(state.Ring, position.X - RingSize / 2);
        Canvas.SetTop(state.Ring, position.Y - RingSize / 2);
    }

    private static void RemoveOverlay(Control control)
    {
        if (!States.TryGetValue(control, out var state) || state.Overlay is null)
            return;

        var layer = AdornerLayer.GetAdornerLayer(control);
        if (layer is not null && layer.Children.Contains(state.Overlay))
            layer.Children.Remove(state.Overlay);
    }

    private static CornerRadius GetCornerRadius(Control control)
        => control is TemplatedControl tc ? tc.CornerRadius : new CornerRadius(6);
}
