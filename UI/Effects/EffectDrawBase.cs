using System;
using System.Diagnostics;
using Avalonia;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Rendering.Composition;
using Avalonia.Skia;
using Avalonia.Styling;
using SkiaSharp;

namespace PLDUOBAO.UI.Effects;

public abstract class EffectDrawBase : CompositionCustomVisualHandler
{
    public static readonly object StartAnimations = new(),
        StopAnimations = new();

    private readonly Stopwatch _animationTick = new();
    private readonly bool _invalidateRect;
    private bool _animationEnabled;
    private SkiaEffect? _effect;

    protected EffectDrawBase(bool invalidateRect = true)
    {
        _invalidateRect = invalidateRect;
    }

    public SkiaEffect? Effect
    {
        get => _effect;
        set
        {
            var old = _effect;
            if (Equals(old, value)) return;
            _effect = value;
            EffectChanged(old, _effect);
        }
    }

    public bool AnimationEnabled
    {
        get => _animationEnabled;
        set
        {
            if (value) _animationTick.Start();
            else _animationTick.Stop();
            _animationEnabled = value;
        }
    }

    protected float AnimationSpeedScale { get; set; } = 0.1f;
    protected float AnimationSeconds => (float)_animationTick.Elapsed.TotalSeconds;

    protected ThemeVariant ActiveVariant =>
        Application.Current?.ActualThemeVariant ?? ThemeVariant.Dark;

    public override void OnRender(ImmediateDrawingContext context)
    {
        var leaseFeature = context.TryGetFeature<ISkiaSharpApiLeaseFeature>()
            ?? throw new InvalidOperationException("Unable to lease Skia API");

        using var lease = leaseFeature.Lease();
        var rect = SKRect.Create((float)EffectiveSize.X, (float)EffectiveSize.Y);
        if (lease.GrContext is null)
            RenderSoftware(lease.SkCanvas, rect);
        else
            Render(lease.SkCanvas, rect);
    }

    public override void OnMessage(object message)
    {
        if (message == StartAnimations)
        {
            AnimationEnabled = true;
            RegisterForNextAnimationFrameUpdate();
        }
        else if (message == StopAnimations)
        {
            AnimationEnabled = false;
        }
        else if (message is SkiaEffect effect)
        {
            Effect = effect;
        }
    }

    public override void OnAnimationFrameUpdate()
    {
        if (!AnimationEnabled) return;

        if (!IsAttachedToVisualTree)
        {
            RegisterForNextAnimationFrameUpdate();
            return;
        }

        if (_invalidateRect)
            Invalidate(GetRenderBounds());
        else
            Invalidate();

        RegisterForNextAnimationFrameUpdate();
    }

    private bool IsAttachedToVisualTree =>
        EffectiveSize.X > 0 && EffectiveSize.Y > 0;

    protected abstract void Render(SKCanvas canvas, SKRect rect);

    protected abstract void RenderSoftware(SKCanvas canvas, SKRect rect);

    protected SKShader? EffectWithUniforms(float alpha = 1f) =>
        Effect?.ToShaderWithUniforms(AnimationSeconds, ActiveVariant, GetRenderBounds(), AnimationSpeedScale, alpha);

    protected virtual void EffectChanged(SkiaEffect? oldValue, SkiaEffect? newValue)
    {
    }
}
