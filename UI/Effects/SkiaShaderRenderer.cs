using System;
using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Rendering.SceneGraph;
using Avalonia.Skia;
using Avalonia.Styling;
using Avalonia.Threading;
using SkiaSharp;

namespace PLDUOBAO.UI.Effects;

public sealed class SkiaShaderRenderer : Control
{
    private SkiaEffect? _effect;
    private readonly Stopwatch _animationTick = new();
    private DispatcherTimer? _timer;
    private bool _isAttached;
    private const float AnimationSpeedScale = 2f;

    protected override Size MeasureOverride(Size availableSize) => availableSize;

    protected override Size ArrangeOverride(Size finalSize)
    {
        var size = base.ArrangeOverride(finalSize);
        InvalidateVisual();
        return size;
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _isAttached = true;
        _animationTick.Restart();
        StartAnimationTimer();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _isAttached = false;
        StopAnimationTimer();
        base.OnDetachedFromVisualTree(e);
    }

    public void Stop() => StopAnimationTimer();

    public void Start()
    {
        if (_isAttached)
            StartAnimationTimer();
    }

    public void SetEffect(SkiaEffect effect)
    {
        _effect = effect;
        InvalidateVisual();
    }

    public void RefreshColors()
    {
        SkiaEffect.UpdateColor();
        InvalidateVisual();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == IsVisibleProperty)
        {
            if (IsVisible) Start();
            else Stop();
        }
        else if (change.Property == BoundsProperty)
        {
            InvalidateVisual();
        }
    }

    public override void Render(DrawingContext context)
    {
        if (_effect is null || Bounds.Width < 1 || Bounds.Height < 1)
            return;

        var bounds = new Rect(Bounds.Size);
        var variant = Application.Current?.ActualThemeVariant ?? ThemeVariant.Dark;
        var time = (float)_animationTick.Elapsed.TotalSeconds;

        context.Custom(new ShaderDrawOperation(_effect, bounds, time, variant, AnimationSpeedScale));
    }

    private void StartAnimationTimer()
    {
        if (_timer != null) return;

        _timer = new DispatcherTimer(TimeSpan.FromMilliseconds(16), DispatcherPriority.Render, (_, _) =>
        {
            if (!IsVisible || _effect is null)
                return;

            InvalidateVisual();
        });

        _timer.Start();
        InvalidateVisual();
    }

    private void StopAnimationTimer()
    {
        _timer?.Stop();
        _timer = null;
    }

    private sealed class ShaderDrawOperation : ICustomDrawOperation
    {
        private readonly SkiaEffect _effect;
        private readonly Rect _bounds;
        private readonly float _timeSeconds;
        private readonly ThemeVariant _variant;
        private readonly float _animationScale;

        public ShaderDrawOperation(
            SkiaEffect effect,
            Rect bounds,
            float timeSeconds,
            ThemeVariant variant,
            float animationScale)
        {
            _effect = effect;
            _bounds = bounds;
            _timeSeconds = timeSeconds;
            _variant = variant;
            _animationScale = animationScale;
        }

        public Rect Bounds => _bounds;

        public void Render(ImmediateDrawingContext context)
        {
            var feature = context.TryGetFeature<ISkiaSharpApiLeaseFeature>();
            if (feature is null) return;

            using var lease = feature.Lease();
            var canvas = lease.SkCanvas;
            var rect = SKRect.Create((float)_bounds.Width, (float)_bounds.Height);

            using var shader = _effect.ToShaderWithUniforms(
                _timeSeconds, _variant, _bounds, _animationScale);
            using var paint = new SKPaint { Shader = shader };
            canvas.DrawRect(rect, paint);
        }

        public bool HitTest(Point p) => false;

        public bool Equals(ICustomDrawOperation? other) => false;

        public void Dispose()
        {
        }
    }
}
