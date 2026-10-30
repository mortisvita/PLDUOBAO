using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Controls.Notifications;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Elysium.Core.Utils;
using PLDUOBAO.SDK;
using PLDUOBAO.Util;

namespace PLDUOBAO;

public partial class UCDialogAccountExport : UserControl
{
    private class Row
    {
        public required CheckBox Box { get; init; }
        public required UserCookie User { get; init; }
    }

    private readonly List<Row> _rows = new();

    public IReadOnlyList<UserCookie> Result { get; private set; } = Array.Empty<UserCookie>();
    public bool WasExported { get; private set; }

    public event EventHandler? RequestClose;

    private bool _disposed;

    public UCDialogAccountExport()
    {
        InitializeComponent();
    }

    public void BuildList()
    {
        if (_disposed) return;

        listAccounts.Children.Clear();
        _rows.Clear();
        Result = Array.Empty<UserCookie>();
        WasExported = false;

        var users = LocalAccountManager.Users ?? new List<UserCookie>();
        tbStatus.Text = $"{users.Count} 个账户可导出";

        foreach (var u in users)
        {
            var box = new CheckBox
            {
                IsChecked = true,
                Classes = { "acct-check" }
            };
            box.Content = BuildLabel(u);

            box.IsCheckedChanged += OnBoxChecked;

            var row = new Row { Box = box, User = u };
            _rows.Add(row);

            var border = new Border
            {
                Background = (IBrush?)this.FindResource("CardBackgroundFillColorDefaultBrush"),
                BorderBrush = (IBrush?)this.FindResource("CardStrokeColorDefaultBrush"),
                BorderThickness = new Avalonia.Thickness(1),
                CornerRadius = new Avalonia.CornerRadius(6),
                Padding = new Avalonia.Thickness(14, 12),
                Child = box
            };

            listAccounts.Children.Add(border);
        }

        UpdateSelectionCount();
    }

    private void OnBoxChecked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        UpdateSelectionCount();
    }

    private static string BuildLabel(UserCookie u)
    {
        var nick = string.IsNullOrEmpty(u.Nickname) ? "" : $"  ·  {u.Nickname}";
        return $"{u.Name}{nick}";
    }

    private void UpdateSelectionCount()
    {
        if (_disposed) return;
        var n = _rows.Count(r => r.Box.IsChecked == true);
        tbSelectedCount.Text = $"已选 {n}";
    }

    private void onSelectAll(object? sender, RoutedEventArgs e)
    {
        foreach (var r in _rows) r.Box.IsChecked = true;
    }

    private void onSelectInverse(object? sender, RoutedEventArgs e)
    {
        foreach (var r in _rows) r.Box.IsChecked = r.Box.IsChecked != true;
    }

    private void onSelectSAuthOnly(object? sender, RoutedEventArgs e)
    {
        foreach (var r in _rows)
        {
            r.Box.IsChecked = !string.IsNullOrEmpty(r.User.SAuth);
        }
    }

    private async void onExport(object? sender, RoutedEventArgs e)
    {
        if (_disposed) return;

        var picks = _rows.Where(r => r.Box.IsChecked == true).Select(r => r.User).ToList();
        if (picks.Count == 0)
        {
            MainWindow.RaiseMessage("至少勾一个", "提示", NotificationType.Warning);
            return;
        }

        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel == null) return;

        var sp = topLevel.StorageProvider;
        if (sp == null) return;

        var defaultName = $"accountlist_{DateTime.Now:yyyyMMdd_HHmmss}.json";
        var file = await sp.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "选择导出位置",
            SuggestedFileName = defaultName,
            DefaultExtension = "json",
            FileTypeChoices = new[]
            {
                new FilePickerFileType("JSON 文件") { Patterns = new[] { "*.json" } },
                FilePickerFileTypes.All
            }
        });

        if (file == null) return;

        var path = file.TryGetLocalPath();
        if (string.IsNullOrEmpty(path)) return;

        try
        {
            if (LocalAccountManager.ExportUsers(picks, path))
            {
                Result = picks;
                WasExported = true;
                MainWindow.RaiseMessage($"已导出 {picks.Count} 个账户到\n{path}", "成功", NotificationType.Success);
                CloseWithAnimation();
            }
            else
            {
                MainWindow.RaiseMessage("导出失败，看日志", "错误", NotificationType.Error);
            }
        }
        catch (Exception ex)
        {
            LogSys.Err($"[UCDialogAccountExport] {ex}");
            MainWindow.RaiseMessage($"导出炸了：{ex.Message}", "错误", NotificationType.Error);
        }
    }

    private void onCancel(object? sender, RoutedEventArgs e)
    {
        CloseWithAnimation();
    }

    private void Close()
    {
        if (_disposed) return;
        Dispose();
        RequestClose?.Invoke(this, EventArgs.Empty);
    }

    private void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        foreach (var r in _rows)
        {
            r.Box.IsCheckedChanged -= OnBoxChecked;
        }
        _rows.Clear();
        listAccounts.Children.Clear();
    }

    /// <summary>
    /// 显示并模态弹窗。每次都返回一个新实例，避免 FluentAvalonia ContentDialog overlay 复用导致的"卡住"问题。
    /// </summary>
    public static UCDialogAccountExport? ShowModal(Grid container)
    {
        if (container == null) return null;
        var dlg = new UCDialogAccountExport();
        dlg.ZIndex = 50;
        dlg.RequestClose += (s, e) =>
        {
            container.Children.Remove(dlg);
        };
        container.Children.Add(dlg);
        dlg.PlayOpenAnimation();
        dlg.BuildList();
        return dlg;
    }

    /// <summary>
    /// 弹出动画 —— overlay 淡入 + 卡片缩放淡入。
    /// </summary>
    private void PlayOpenAnimation()
    {
        if (_disposed) return;

        var overlay = this.FindControl<Border>("exportOverlay");
        var card = this.FindControl<Border>("exportCard");
        if (overlay == null || card == null) return;

        // 居中缩放锚点
        card.RenderTransformOrigin = new RelativePoint(0.5, 0.5, RelativeUnit.Relative);
        card.RenderTransform = new ScaleTransform(0.94, 0.94);

        // overlay 淡入
        overlay.Opacity = 0;
        var fadeIn = AnimationHelper.FadeIn(180);
        _ = fadeIn.RunAsync(overlay);

        // 卡片缩放+淡入
        var scaleIn = AnimationHelper.ScaleIn(220);
        _ = scaleIn.RunAsync(card);
    }

    private async void CloseWithAnimation()
    {
        if (_disposed) { return; }

        var overlay = this.FindControl<Border>("exportOverlay");
        var card = this.FindControl<Border>("exportCard");
        if (overlay == null || card == null) { Close(); return; }

        var fadeOut = AnimationHelper.FadeOut(160);
        var scaleOut = AnimationHelper.ScaleOut(160);

        await Task.WhenAll(fadeOut.RunAsync(overlay), scaleOut.RunAsync(card));
        Close();
    }
}
