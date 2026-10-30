using System.Threading.Tasks;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Styling;
using FluentAvalonia.Styling;
using PLDUOBAO.SDK;
using PLDUOBAO.Util;

namespace PLDUOBAO.UI
{
    public partial class UCFirstLaunchTheme : UserControl
    {
        private AppTheme _selectedTheme = AppTheme.System;
        
        public UCFirstLaunchTheme()
        {
            InitializeComponent();
            lightCard.PointerPressed += (_, _) => SelectTheme(AppTheme.Light);
            darkCard.PointerPressed += (_, _) => SelectTheme(AppTheme.Dark);
            Loaded += OnLoaded;
        }

        private void OnLoaded(object? sender, RoutedEventArgs e)
        {
            // 读取当前实际主题（可能已由 App.cs 或 config 设置好）
            var config = PathUtils.LoadConfig();
            _selectedTheme = config?.theme ?? AppTheme.System;

            if (_selectedTheme == AppTheme.System)
            {
                // 默认跟随系统：toggle 置为 checked
                systemThemeToggle.IsChecked = true;
            }
            else
            {
                systemThemeToggle.IsChecked = false;
            }

            UpdateCardSelection();
        }

        /// <summary>
        /// 播放入场动画：标题 → 卡片 → 系统主题选项 → 底部按钮，依次错落淡入
        /// </summary>
        public async Task PlayEntranceAsync()
        {
            var slideUp = new Animation
            {
                Duration = System.TimeSpan.FromMilliseconds(550),
                Easing = new CubicEaseOut(),
                FillMode = FillMode.Forward,
                Children =
                {
                    new KeyFrame { Cue = new Cue(0), Setters = { new Setter(OpacityProperty, 0.0), new Setter(Avalonia.Media.TranslateTransform.YProperty, 24.0) } },
                    new KeyFrame { Cue = new Cue(1), Setters = { new Setter(OpacityProperty, 1.0), new Setter(Avalonia.Media.TranslateTransform.YProperty, 0.0) } }
                }
            };

            var fadeIn = new Animation
            {
                Duration = System.TimeSpan.FromMilliseconds(400),
                Easing = new CubicEaseOut(),
                FillMode = FillMode.Forward,
                Children =
                {
                    new KeyFrame { Cue = new Cue(0), Setters = { new Setter(OpacityProperty, 0.0), new Setter(Avalonia.Media.TranslateTransform.YProperty, 16.0) } },
                    new KeyFrame { Cue = new Cue(1), Setters = { new Setter(OpacityProperty, 1.0), new Setter(Avalonia.Media.TranslateTransform.YProperty, 0.0) } }
                }
            };

            // 标题
            await slideUp.RunAsync(titleSection);
            await Task.Delay(60);

            // 卡片
            await slideUp.RunAsync(cardsSection);
            await Task.Delay(60);

            // 跟随系统选项
            await fadeIn.RunAsync(systemThemeSection);
            await Task.Delay(40);

            // 底部按钮
            await fadeIn.RunAsync(bottomBar);
        }

        /// <summary>
        /// 选择主题，更新卡片选中状态和 checkmark
        /// </summary>
        private void SelectTheme(AppTheme theme)
        {
            if (systemThemeToggle.IsChecked == true)
            {
                systemThemeToggle.IsChecked = false;
            }

            _selectedTheme = theme;
            UpdateCardSelection();
            ApplyThemePreview(theme);
        }

        private void UpdateCardSelection()
        {
            if (_selectedTheme == AppTheme.Light)
            {
                lightCard.Classes.Remove("unselected");
                lightCard.Classes.Add("selected");
                darkCard.Classes.Remove("selected");
                darkCard.Classes.Add("unselected");
                lightCheckmark.Opacity = 1;
                darkCheckmark.Opacity = 0;
            }
            else if (_selectedTheme == AppTheme.Dark)
            {
                darkCard.Classes.Remove("unselected");
                darkCard.Classes.Add("selected");
                lightCard.Classes.Remove("selected");
                lightCard.Classes.Add("unselected");
                darkCheckmark.Opacity = 1;
                lightCheckmark.Opacity = 0;
            }
            else // System
            {
                lightCard.Classes.Remove("selected");
                lightCard.Classes.Add("unselected");
                darkCard.Classes.Remove("selected");
                darkCard.Classes.Add("unselected");
                lightCheckmark.Opacity = 0;
                darkCheckmark.Opacity = 0;
            }
        }

        private static void ApplyThemePreview(AppTheme theme)
        {
            // 只切明暗，不碰主题色（主题色由 App.cs 统一管理）
            App.SetTheme(theme);

            // System 之外还需要关掉 PreferSystemTheme，否则 FluentAvalonia 不响应
            if (App.FATheme != null && theme != AppTheme.System)
            {
                App.FATheme.PreferSystemTheme = false;
            }
        }

        private void SystemThemeToggle_Changed(object? sender, RoutedEventArgs e)
        {
            if (systemThemeToggle.IsChecked == true)
            {
                _selectedTheme = AppTheme.System;
                UpdateCardSelection();
                if (App.FATheme != null) App.FATheme.PreferSystemTheme = true;
                App.Current!.RequestedThemeVariant = ThemeVariant.Default;
            }
            else if (_selectedTheme == AppTheme.System)
            {
                // toggle 取消时，默认退回到当前系统实际明暗
                var isDark = ActualThemeVariant == ThemeVariant.Dark;
                _selectedTheme = isDark ? AppTheme.Dark : AppTheme.Light;
                UpdateCardSelection();
                ApplyThemePreview(_selectedTheme);
            }
        }

        private async void NextButton_Click(object? sender, RoutedEventArgs e)
        {
            // 保存主题设置
            var config = PathUtils.LoadConfig() ?? new UserConfig();
            config.theme = _selectedTheme;
            config.firstLaunchDone = true;
            PathUtils.SaveConfig(config);

            // 退场动画：整体上滑淡出
            var exit = new Animation
            {
                Duration = System.TimeSpan.FromMilliseconds(380),
                Easing = new CubicEaseIn(),
                FillMode = FillMode.Forward,
                Children =
                {
                    new KeyFrame { Cue = new Cue(0), Setters = { new Setter(OpacityProperty, 1.0), new Setter(Avalonia.Media.TranslateTransform.YProperty, 0.0) } },
                    new KeyFrame { Cue = new Cue(1), Setters = { new Setter(OpacityProperty, 0.0), new Setter(Avalonia.Media.TranslateTransform.YProperty, -20.0) } }
                }
            };
            await exit.RunAsync(this);

            // 通知主窗口继续
            ThemeSelected?.Invoke(this, _selectedTheme);
        }

        public event System.EventHandler<AppTheme>? ThemeSelected;
    }
}
