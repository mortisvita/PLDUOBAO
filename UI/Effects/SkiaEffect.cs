using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Media;
using Avalonia.Styling;
using SkiaSharp;

namespace PLDUOBAO.UI.Effects;

public class SkiaEffect
{

    private static readonly string[] Uniforms =
    [
        "uniform float iTime;",
        "uniform float iDark;",
        "uniform float iAlpha;",
        "uniform vec3 iResolution;",
        "uniform vec3 iPrimary;",
        "uniform vec3 iAccent;",
        "uniform vec3 iBase;"
    ];

    private static bool _disposed;
    private static readonly List<SkiaEffect> LoadedEffects = [];
    private static float[] _backgroundAlloc = [0.08f, 0.08f, 0.12f];
    private static float[] _backgroundAccentAlloc = [1.00f, 0.27f, 0.00f];
    private static float[] _backgroundPrimaryAlloc = [0.00f, 0.82f, 0.82f];
    private readonly float[] _boundsAlloc = new float[3];
    private readonly string _rawShaderString;
    private readonly string _shaderString;

    static SkiaEffect()
    {
        if (Application.Current?.ApplicationLifetime is IControlledApplicationLifetime controlled)
            controlled.Exit += (_, _) => EnsureDisposed();
    }

    private SkiaEffect(string shaderString, string rawShaderString)
    {
        _shaderString = shaderString;
        _rawShaderString = rawShaderString;
        var compiledEffect = SKRuntimeEffect.Create(_shaderString, out var errors);
        Effect = compiledEffect ?? throw new ShaderCompilationException(errors);
        LoadedEffects.Add(this);
    }

    public SKRuntimeEffect Effect { get; }

    public static void UpdateColor()
    {
        var accent = App.FATheme?.CustomAccentColor ?? Color.FromRgb(0, 120, 212);
        var variant = Application.Current?.ActualThemeVariant ?? ThemeVariant.Dark;
        var isLight = variant == ThemeVariant.Light;

        if (!isLight)
        {
            _backgroundPrimaryAlloc = ToVec3(accent.Lighten(0.25f));
            _backgroundAccentAlloc = ToVec3(accent.Darken(0.55f));
            _backgroundAlloc = new[] { 0.42f, 0.42f, 0.45f };
        }
        else
        {
            _backgroundPrimaryAlloc = ToVec3(Colors.White);
            _backgroundAccentAlloc = ToVec3(accent.Lighten(0.05f));
            _backgroundAlloc = new[] { 0.82f, 0.82f, 0.85f };
        }
    }

    public static SkiaEffect FromString(string shaderString)
    {
        var sb = new StringBuilder();
        foreach (var uniform in Uniforms)
            sb.AppendLine(uniform);

        sb.Append(shaderString);
        return new SkiaEffect(sb.ToString(), shaderString);
    }

    internal static void EnsureDisposed()
    {
        if (_disposed)
            return;

        _disposed = true;
        foreach (var loaded in LoadedEffects)
            loaded.Effect.Dispose();

        LoadedEffects.Clear();
    }

    internal SKShader ToShaderWithUniforms(float timeSeconds, ThemeVariant activeVariant, Rect bounds,
        float animationScale, float alpha = 1f)
    {
        _boundsAlloc[0] = (float)bounds.Width;
        _boundsAlloc[1] = (float)bounds.Height;
        _boundsAlloc[2] = 0f;

        var isDark = activeVariant == ThemeVariant.Dark ||
                     (activeVariant == ThemeVariant.Default &&
                      Application.Current?.ActualThemeVariant == ThemeVariant.Dark);

        var inputs = new SKRuntimeEffectUniforms(Effect)
        {
            { "iResolution", _boundsAlloc },
            { "iTime", timeSeconds * animationScale },
            { "iBase", _backgroundAlloc },
            { "iAccent", _backgroundAccentAlloc },
            { "iPrimary", _backgroundPrimaryAlloc },
            { "iDark", isDark ? 1f : 0f },
            { "iAlpha", alpha }
        };

        return Effect.ToShader(true, inputs);
    }

    private static float[] ToVec3(Color color) =>
        [color.R / 255f, color.G / 255f, color.B / 255f];

    private class ShaderCompilationException(string message) : Exception(message);
}