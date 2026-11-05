using System;
using System.Net.Http;
using System.Runtime.Versioning;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Styling;
using FluentAvalonia.Styling;
using PLDUOBAO.SDK;
using Microsoft.Win32;
using PLDUOBAO.Util;

namespace PLDUOBAO
{
    public partial class App : Application
    {
        public static FluentAvaloniaTheme? FATheme { get; private set; }

        public static event Action? ThemeAppearanceChanged;

        private static void NotifyThemeAppearanceChanged() =>
            ThemeAppearanceChanged?.Invoke();
        
        public override void Initialize()
        {
            // 忽略系统代理：抓包/调试工具退出后残留的死代理会让所有 HttpClient
            // 抛 10061。启动器自身所有网络请求一律直连。
            HttpClient.DefaultProxy = new System.Net.WebProxy();
            AvaloniaXamlLoader.Load(this);
        }

        public override void OnFrameworkInitializationCompleted()
        {
            if (Styles[0] is FluentAvaloniaTheme faTheme)
            {
                FATheme = faTheme;
                
                try
                {
                    var config = PathUtils.LoadConfig();
                    if (config != null && config.useCustomAccentColor && config.customAccentColor.HasValue)
                    {
                        faTheme.CustomAccentColor = Color.FromUInt32(config.customAccentColor.Value);
                    }
                    else
                    {
                        var accentColor = GetWindowsAccentColor();
                        if (accentColor.HasValue)
                            faTheme.CustomAccentColor = accentColor.Value;
                    }
                    
                    if (config != null)
                        SetTheme(config.theme);
                }
                catch
                {
                }
            }
            
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                desktop.MainWindow = new MainWindow();
            }

            if (Current != null)
            {
                Current.PropertyChanged += (_, e) =>
                {
                    if (e.Property == ActualThemeVariantProperty)
                        NotifyThemeAppearanceChanged();
                };
            }

            base.OnFrameworkInitializationCompleted();
        }
        
        [SupportedOSPlatform("windows")]
        private static Color? GetWindowsAccentColor()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\DWM");
                if (key != null)
                {
                    var value = key.GetValue("AccentColor");
                    if (value is int colorValue)
                    {
                        byte b = (byte)((colorValue >> 16) & 0xFF);
                        byte g = (byte)((colorValue >> 8) & 0xFF);
                        byte r = (byte)(colorValue & 0xFF);
                        
                        return Color.FromArgb(255, r, g, b);
                    }
                }
            }
            catch
            {
            }
            return null;
        }
        
        public static void UpdateAccentColor(Color color)
        {
            if (FATheme != null)
            {
                FATheme.CustomAccentColor = color;
                NotifyThemeAppearanceChanged();
            }
        }
        
        public static void RestoreSystemAccentColor()
        {
            if (FATheme != null)
            {
                try
                {
                    var systemColor = GetWindowsAccentColor();
                    FATheme.CustomAccentColor = systemColor ?? Color.FromRgb(0, 120, 212);
                }
                catch
                {
                    FATheme.CustomAccentColor = Color.FromRgb(0, 120, 212);
                }

                NotifyThemeAppearanceChanged();
            }
        }
        
        public static void SetTheme(AppTheme theme)
        {
            if (Current == null) return;
            
            switch (theme)
            {
                case AppTheme.Light:
                    Current.RequestedThemeVariant = ThemeVariant.Light;
                    break;
                case AppTheme.Dark:
                    Current.RequestedThemeVariant = ThemeVariant.Dark;
                    break;
                case AppTheme.System:
                    Current.RequestedThemeVariant = ThemeVariant.Default;
                    break;
            }

            NotifyThemeAppearanceChanged();
        }
        
        public static AppTheme GetCurrentTheme()
        {
            if (Current?.RequestedThemeVariant == ThemeVariant.Light)
                return AppTheme.Light;
            if (Current?.RequestedThemeVariant == ThemeVariant.Dark)
                return AppTheme.Dark;
            return AppTheme.System;
        }
    }
}
