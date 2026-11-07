using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Elysium.Core.Utils;
using Avalonia.Platform;
using FluentAvalonia.UI.Controls;
using PLDUOBAO.Network;
using PLDUOBAO.Network.Netease;
using PLDUOBAO.Network.Netease.NetworkSDK.PECommon;
using PLDUOBAO.Network.Netease.NetworkSDK;
using PLDUOBAO.Network.LocalAuthServer;
using PLDUOBAO.Network.NewVerify;
using PLDUOBAO.SDK;
using PLDUOBAO.Services;
using PLDUOBAO.Util;
using static PLDUOBAO.Network.Netease.NetworkLocalAPI;

namespace PLDUOBAO;

public class LocalWorld
{
    public string? WorldName { get; set; }
    public string? FolderName { get; set; }
    public Bitmap? WorldIcon { get; set; }
}

public partial class UCMainMenu : UserControl
{
    private static readonly string[] PreferredGameDownloadVersions =
    [
        "3.8.10.292963",
        "3.7.15.289120"
    ];

    public string? SkinPath => tbSkinPath.Text;
    public string? GamePath => tbGamePath.Text;
    public bool IsSlimSkin => tsIsSlimSkin.IsChecked ?? false;

    private bool _isDraggingSkin = false;
    private Point _lastDragPos;
    private DispatcherTimer? _skinAnimTimer;
    private double _skinAnimPhase = 0;

    private List<NetGameItem> netGameItems = new List<NetGameItem>();

    private List<RentalGameItem> rentalGameItems = new List<RentalGameItem>();

    private readonly PEGameDownloadService _gameDownloadService = new();
    private IReadOnlyList<GameDownloadVersionInfo> _gameDownloadVersions = Array.Empty<GameDownloadVersionInfo>();
    private CancellationTokenSource? _gameDownloadCts;
    private bool _gameDownloadVersionsLoaded;
    private bool _userPickedGameDownloadPath;
    private bool _isApplyingGameDownloadPath;
    private bool _showOnlyPreferredGameDownloadVersions;

    private int _netGameLoadGen;
    private int _rentalGameLoadGen;
    private bool _netGameCached;
    private bool _rentalGameCached;

    public UCMainMenu()
    {
        InitializeComponent();
    }

    public UCMainMenu(UCCheckName cn)
    {
        InitializeComponent();
        MainWindow.UCMainmenu = this;
    }

    private void onLoaded(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (NetworkLocalAPI.CurrentAvaliableUser == null ||
            string.IsNullOrEmpty(CurrentUserName) ||
            string.IsNullOrWhiteSpace(CurrentUserName)
            )
            return;
    
        tbUserName.Text = $"{CurrentUserName} (Lv.{CurrentLevel})";
    
        try
        {
            LocalAccountManager.SaveCurrentUser();
        }
        catch
        {
            MainWindow.RaiseMessage("本次账户信息不会被保存,因为您的登录方式为Null类型！！",
                                    "警告",
                                    Avalonia.Controls.Notifications.NotificationType.Error);
        }
    
        LoadUserConfig();
        
        // 加载游戏时长统计
        LoadGamePlayStats();
        
        
        // 初始化皮肤预览动画定时器
        if (_skinAnimTimer == null)
        {
            _skinAnimTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(20)
            };
            _skinAnimTimer.Tick += (s, e) =>
            {
                _skinAnimPhase += 0.2; // 降低步进值以减慢走路速度
                UpdateSkinPreview();
            };
        }
        UpdateAnimTimerState();



        pageFrame.PagePool = pagePool;
        pageFrame.NavigateTo(pageHome);
    }

    private void OnNavigationSelectionChanged(object? sender, NavigationViewSelectionChangedEventArgs e)
    {
        if (e.SelectedItem is not NavigationViewItem item)
            return;

        string? tag = item.Tag?.ToString();

        Grid? targetPage = tag switch
        {
            "Home" => pageHome,
            "NetGame" => pageNetGame,
            "Settings" => pageSettings,
            _ => null
        };

        if (targetPage == null)
            return;

        if (tag != "NetGame")
            _netGameLoadGen++;

        if (tag == "Home")
            LoadGamePlayStats();
        if (tag == "NetGame")
            _ = LoadNetGameAsync();
        if (tag == "Settings")
            _ = LoadGameDownloadVersionsAsync();

        pageFrame.NavigateTo(targetPage);

        UpdateAnimTimerState();
    }

    private void UpdateAnimTimerState()
    {
        bool shouldRun = pageFrame.CurrentPage == pageSettings && rbPreview3D?.IsChecked == true;
        if (shouldRun) _skinAnimTimer?.Start();
        else _skinAnimTimer?.Stop();
    }

    private async Task LoadNetGameAsync(bool forceRefresh = false)
    {
        if (CurrentAvaliableUser == null)
            return;

        if (!forceRefresh && _netGameCached)
        {
            ApplyNetGameList(_netGameLoadGen);
            return;
        }

        var gen = ++_netGameLoadGen;
        netGameItems.Clear();
        SetNetGameLoading(true);

        try
        {
            var result = await NeteasePosts.GetAvailableNetGamesAsync();
            if (gen != _netGameLoadGen)
                return;

            var netgame = result?.entity;
            if (netgame?.Res == null || netgame.Res.Count == 0)
            {
                _netGameCached = true;
                ApplyNetGameList(gen, empty: true);
                return;
            }

            netGameItems = netgame.Res
                .OrderByDescending(item => int.TryParse(item.OnlineNum, out var num) ? num : 0)
                .ToList();

            _netGameCached = true;
            ApplyNetGameList(gen);
        }
        catch (Exception ex)
        {
            if (gen != _netGameLoadGen)
                return;

            MainWindow.RaiseMessage($"获取网络游戏列表错误：{ex.Message}",
                "错误",
                Avalonia.Controls.Notifications.NotificationType.Error);
            LogSys.Err(ex);
        }
        finally
        {
            if (gen == _netGameLoadGen)
                SetNetGameLoading(false);
        }
    }

    private void SetNetGameLoading(bool loading)
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (tbNetGameLoading != null)
                tbNetGameLoading.IsVisible = loading;
            if (listNetGame != null)
                listNetGame.IsVisible = !loading;
        });
    }

    private void ApplyNetGameList(int gen, bool empty = false)
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (gen != _netGameLoadGen)
                return;
            if (!ReferenceEquals(pageFrame.CurrentPage, pageNetGame))
                return;

            listNetGame.ItemsSource = empty ? null : netGameItems;
            FilterNetGameList();
        });
    }

    private async void onLoadNetGame(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        await LoadNetGameAsync();
    }
    
    private void OnNetGameSearchTextChanged(object? sender, TextChangedEventArgs e)
    {
        FilterNetGameList();
    }
    
    private void FilterNetGameList()
    {
        if (!ReferenceEquals(pageFrame.CurrentPage, pageNetGame))
            return;

        string searchText = tboxNetGameSearch.Text?.Trim() ?? "";

        if (string.IsNullOrEmpty(searchText))
            listNetGame.ItemsSource = netGameItems;
        else
            listNetGame.ItemsSource = netGameItems
                .Where(item => item.ResName?.Contains(searchText, StringComparison.OrdinalIgnoreCase) == true)
                .ToList();
    }

    private async void onRefreshNetGame(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        await LoadNetGameAsync(forceRefresh: true);
    }

    private async void onRenameNickName(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (CurrentAvaliableUser == null)
        {
            MainWindow.RaiseMessage("请先登录游戏账号", "提示",
                Avalonia.Controls.Notifications.NotificationType.Warning);
            return;
        }

        var dialog = new ContentDialog
        {
            Title = "修改游戏昵称",
            PrimaryButtonText = "保存",
            CloseButtonText = "取消"
        };

        var textBox = new TextBox
        {
            Text = CurrentUserName,
            Watermark = "长度不超过8个中文或16个英文/数字",
            MaxLength = 16
        };

        dialog.Content = new StackPanel
        {
            Spacing = 12,
            Width = 320,
            Children =
            {
                new TextBlock
                {
                    Text = "修改后将同步到网易我的世界账号昵称",
                    Foreground = Avalonia.Media.Brushes.Gray,
                    TextWrapping = Avalonia.Media.TextWrapping.Wrap
                },
                textBox
            }
        };

        if (await dialog.ShowAsync() != ContentDialogResult.Primary)
            return;

        var newName = textBox.Text?.Trim() ?? "";
        if (string.IsNullOrEmpty(newName))
        {
            MainWindow.RaiseMessage("昵称不能为空", "错误",
                Avalonia.Controls.Notifications.NotificationType.Error);
            return;
        }

        if (string.Equals(newName, CurrentUserName, StringComparison.Ordinal))
            return;

        var oldName = CurrentUserName;

        try
        {
            var result = await NeteasePosts.UpdatePcNickNameAsync(newName);
            if (result == null)
                return;

            if (result.code != 0)
            {
                MainWindow.RaiseMessage($"改名失败：{result.message}（{result.code}）", "错误",
                    Avalonia.Controls.Notifications.NotificationType.Error);
                return;
            }

            await NeteasePosts.RefreshCurrentUserProfileAsync();
            if (string.IsNullOrEmpty(CurrentUserName))
                CurrentUserName = result.entity?.Name ?? newName;

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                tbUserName.Text = $"{CurrentUserName} (Lv.{CurrentLevel})";
            });

            if (!LocalAccountManager.UpdateCurrentUserDisplayName(oldName, CurrentUserName))
                LocalAccountManager.SaveCurrentUser();

            var remain = result.entity?.RemainReviseNameCnt ?? -1;
            var hint = remain >= 0 ? $"，剩余改名次数：{remain}" : "";
            MainWindow.RaiseMessage($"昵称已更新为 {CurrentUserName}{hint}", "成功",
                Avalonia.Controls.Notifications.NotificationType.Success);
        }
        catch (Exception ex)
        {
            LogSys.Err($"UpdatePcNickName Error: {ex}");
            MainWindow.RaiseMessage($"改名失败：{ex.Message}", "错误",
                Avalonia.Controls.Notifications.NotificationType.Error);
        }
    }

    private void onClickExit(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        ResetUser();
        MainWindow.GridMainMenu.Children.Add(MainWindow.UCCookieSelector!);
        MainWindow.GridMainMenu.Children.Remove(this);
    }

    private async void onClickGameFoldler(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var folders = await MainWindow.Instance.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "选择你的MC游戏文件夹",
            AllowMultiple = false
        });

        if (folders.Count > 0)
        {
            var folder = folders[0];
            var path = folder.TryGetLocalPath() ?? string.Empty;

            if (!string.IsNullOrEmpty(path) && CJKPathGuard.ContainsCJK(path))
            {
                MainWindow.RaiseMessage(
                    CJKPathGuard.RejectMessage,
                    "路径包含中文",
                    Avalonia.Controls.Notifications.NotificationType.Error);
                return;
            }

            tbGamePath.Text = path;
        }
    }

    private async Task LoadGameDownloadVersionsAsync(bool forceRefresh = false)
    {
        if (_gameDownloadCts != null)
            return;

        if (_gameDownloadVersionsLoaded && !forceRefresh)
            return;

        string? selectedVersion = (cbGameDownloadVersion.SelectedItem as GameDownloadVersionInfo)?.Version;
        SetGameDownloadControlsBusy(true);
        tbGameDownloadStatus.Text = "正在获取版本列表...";

        try
        {
            _gameDownloadVersions = await _gameDownloadService.GetAvailableVersionsAsync();
            _gameDownloadVersionsLoaded = true;

            ApplyGameDownloadVersionFilter(selectedVersion);
            tbGameDownloadStatus.Text = BuildGameDownloadVersionStatus();
        }
        catch (Exception ex)
        {
            tbGameDownloadStatus.Text = "版本加载失败";
            MainWindow.RaiseMessage($"获取游戏版本失败：{ex.Message}",
                "错误",
                Avalonia.Controls.Notifications.NotificationType.Error);
            LogSys.Err(ex);
        }
        finally
        {
            SetGameDownloadControlsBusy(false);
        }
    }

    private async void OnRefreshGameDownloadVersionsClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        await LoadGameDownloadVersionsAsync(forceRefresh: true);
    }

    private void OnToggleGameDownloadVersionFilterClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        _showOnlyPreferredGameDownloadVersions = !_showOnlyPreferredGameDownloadVersions;
        string? selectedVersion = (cbGameDownloadVersion.SelectedItem as GameDownloadVersionInfo)?.Version;

        ApplyGameDownloadVersionFilter(selectedVersion);
        SaveGameDownloadVersionFilterSetting();
        tbGameDownloadStatus.Text = BuildGameDownloadVersionStatus();
    }

    private async void OnChooseGameDownloadPathClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var folders = await MainWindow.Instance.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "选择游戏下载路径（可存放多个版本）",
            AllowMultiple = false
        });

        if (folders.Count <= 0)
            return;

        string? path = folders[0].TryGetLocalPath();
        if (string.IsNullOrWhiteSpace(path))
            return;

        if (CJKPathGuard.ContainsCJK(path))
        {
            MainWindow.RaiseMessage(
                CJKPathGuard.RejectMessage,
                "路径包含中文",
                Avalonia.Controls.Notifications.NotificationType.Error);
            return;
        }

        SetGameDownloadPath(path, userPicked: true);
        SaveGameDownloadPathSetting(path);
    }

    private void OnGameDownloadVersionChanged(object? sender, SelectionChangedEventArgs e)
    {
        SetDefaultGameDownloadPath();
    }

    private async void OnStartGameDownloadClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_gameDownloadCts != null)
            return;

        GameDownloadVersionInfo? selectedVersion = cbGameDownloadVersion.SelectedItem as GameDownloadVersionInfo;
        if (selectedVersion == null)
        {
            await LoadGameDownloadVersionsAsync();
            selectedVersion = cbGameDownloadVersion.SelectedItem as GameDownloadVersionInfo;
        }

        if (selectedVersion == null)
        {
            MainWindow.RaiseMessage("请先刷新并选择一个游戏版本",
                "提示",
                Avalonia.Controls.Notifications.NotificationType.Warning);
            return;
        }

        GameDownloadVersionInfo version = selectedVersion;

        if (string.IsNullOrWhiteSpace(tbGameDownloadPath.Text))
            SetDefaultGameDownloadPath(force: true);

        string installDirectory = tbGameDownloadPath.Text?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(installDirectory))
        {
            MainWindow.RaiseMessage("请先选择游戏安装目录",
                "提示",
                Avalonia.Controls.Notifications.NotificationType.Warning);
            return;
        }

        _userPickedGameDownloadPath = true;
        SaveGameDownloadPathSetting(installDirectory);

        _gameDownloadCts = new CancellationTokenSource();
        CancellationTokenSource currentDownload = _gameDownloadCts;
        SetGameDownloadControlsBusy(false);
        pbGameDownloadProgress.IsVisible = true;
        pbGameDownloadProgress.IsIndeterminate = true;
        tbGameDownloadStatus.Text = "正在准备下载...";

        try
        {
            var progress = new Progress<GameInstallProgress>(UpdateGameDownloadProgress);
            GameDownloadResult result = await _gameDownloadService.DownloadAndInstallAsync(
                version,
                installDirectory,
                progress,
                currentDownload.Token);

            bool defaultSkinApplied = ApplyDownloadedGameDefaults(result.GameDirectory);
            pbGameDownloadProgress.IsIndeterminate = false;
            pbGameDownloadProgress.Value = 100;

            string downloadTime = FormatElapsed(result.DownloadElapsed);
            string extractTime = FormatElapsed(result.ExtractElapsed);
            string totalTime = FormatElapsed(result.DownloadElapsed + result.ExtractElapsed);

            tbGameDownloadStatus.Text = defaultSkinApplied
                ? $"安装完成，已写入游戏路径和默认皮肤 | 下载: {downloadTime} 解压: {extractTime} 总计: {totalTime}"
                : $"安装完成，已写入游戏路径，未找到默认皮肤 | 下载: {downloadTime} 解压: {extractTime} 总计: {totalTime}";

            MainWindow.RaiseMessage(defaultSkinApplied
                    ? $"游戏下载并安装完成\n下载用时: {downloadTime}，解压用时: {extractTime}，总计: {totalTime}"
                    : $"游戏下载并安装完成，默认皮肤未找到\n下载用时: {downloadTime}，解压用时: {extractTime}，总计: {totalTime}",
                "成功",
                Avalonia.Controls.Notifications.NotificationType.Success);
        }
        catch (OperationCanceledException)
        {
            tbGameDownloadStatus.Text = "已取消下载";
            MainWindow.RaiseMessage("游戏下载已取消",
                "提示",
                Avalonia.Controls.Notifications.NotificationType.Warning);
        }
        catch (Exception ex)
        {
            tbGameDownloadStatus.Text = "下载失败";
            MainWindow.RaiseMessage($"游戏下载失败：{ex.Message}",
                "错误",
                Avalonia.Controls.Notifications.NotificationType.Error);
            LogSys.Err(ex);
        }
        finally
        {
            if (ReferenceEquals(_gameDownloadCts, currentDownload))
            {
                _gameDownloadCts.Dispose();
                _gameDownloadCts = null;
            }

            SetGameDownloadControlsBusy(false);
        }
    }

    private static string FormatElapsed(TimeSpan ts)
    {
        if (ts.TotalHours >= 1)
            return $"{(int)ts.TotalHours}h {ts.Minutes}m {ts.Seconds}s";
        if (ts.TotalMinutes >= 1)
            return $"{(int)ts.TotalMinutes}m {ts.Seconds}s";
        return $"{ts.TotalSeconds:F1}s";
    }

    private void OnCancelGameDownloadClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        _gameDownloadCts?.Cancel();
        tbGameDownloadStatus.Text = "正在取消...";
    }

    private void UpdateGameDownloadProgress(GameInstallProgress progress)
    {
        if (!Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.Post(() => UpdateGameDownloadProgress(progress));
            return;
        }

        pbGameDownloadProgress.IsVisible = true;
        if (progress.ProgressPercentage < 0)
        {
            pbGameDownloadProgress.IsIndeterminate = true;
        }
        else
        {
            pbGameDownloadProgress.IsIndeterminate = false;
            pbGameDownloadProgress.Value = Math.Clamp(progress.ProgressPercentage, 0, 100);
        }

        tbGameDownloadStatus.Text = progress.StatusMessage;
    }

    private void SetGameDownloadControlsBusy(bool busy)
    {
        bool downloading = _gameDownloadCts != null;

        btnRefreshGameDownloadVersions.IsEnabled = !busy && !downloading;
        btnToggleGameDownloadVersionFilter.IsEnabled = !busy && !downloading;
        btnStartGameDownload.IsEnabled = !busy && !downloading;
        btnChooseGameDownloadPath.IsEnabled = !busy && !downloading;
        cbGameDownloadVersion.IsEnabled = !busy && !downloading;
        tbGameDownloadPath.IsEnabled = !busy && !downloading;
        btnCancelGameDownload.IsEnabled = downloading;
    }

    private void ApplyGameDownloadVersionFilter(string? selectedVersion = null)
    {
        List<GameDownloadVersionInfo> visibleVersions = GetVisibleGameDownloadVersions();

        cbGameDownloadVersion.ItemsSource = visibleVersions;
        cbGameDownloadVersion.SelectedItem =
            visibleVersions.FirstOrDefault(version =>
                string.Equals(version.Version, selectedVersion, StringComparison.OrdinalIgnoreCase)) ??
            visibleVersions.FirstOrDefault();

        UpdateGameDownloadVersionFilterButton();
        SetDefaultGameDownloadPath();
    }

    private List<GameDownloadVersionInfo> GetVisibleGameDownloadVersions()
    {
        if (!_showOnlyPreferredGameDownloadVersions)
            return _gameDownloadVersions.ToList();

        return _gameDownloadVersions
            .Where(version => PreferredGameDownloadVersions.Contains(
                version.Version,
                StringComparer.OrdinalIgnoreCase))
            .ToList();
    }

    private string BuildGameDownloadVersionStatus()
    {
        if (!_gameDownloadVersionsLoaded)
            return _showOnlyPreferredGameDownloadVersions ? "已切换到指定版本" : "未加载版本";

        int visibleCount = GetVisibleGameDownloadVersions().Count;
        if (!_showOnlyPreferredGameDownloadVersions)
            return $"已加载 {_gameDownloadVersions.Count} 个版本";

        return visibleCount > 0
            ? $"已加载NOL需要版本：{visibleCount}/{PreferredGameDownloadVersions.Length}"
            : "未找到NOL需要版本";
    }

    private void UpdateGameDownloadVersionFilterButton()
    {
        btnToggleGameDownloadVersionFilter.Content = _showOnlyPreferredGameDownloadVersions
            ? "仅NOL需要版本"
            : "显示全部版本";
    }

    private void SaveGameDownloadVersionFilterSetting()
    {
        var config = PathUtils.LoadConfig() ?? new UserConfig();
        config.usePreferredGameDownloadVersions = _showOnlyPreferredGameDownloadVersions;
        PathUtils.SaveConfig(config);
    }

    private void SetDefaultGameDownloadPath(bool force = false)
    {
        if (!force && _userPickedGameDownloadPath)
            return;

        var config = PathUtils.LoadConfig() ?? new UserConfig();
        string? savedPath = config.gameDownloadPath;
        if (!string.IsNullOrWhiteSpace(savedPath))
        {
            SetGameDownloadPath(savedPath, userPicked: true);
            return;
        }

        SetGameDownloadPath(Path.Combine(PathUtils.ConfigPath, "games"), userPicked: false);
    }

    private void SetGameDownloadPath(string? path, bool userPicked)
    {
        _isApplyingGameDownloadPath = true;
        try
        {
            tbGameDownloadPath.Text = path;
        }
        finally
        {
            _isApplyingGameDownloadPath = false;
        }

        if (userPicked)
            _userPickedGameDownloadPath = true;
    }

    private void SaveGameDownloadPathSetting(string? path = null)
    {
        var config = PathUtils.LoadConfig() ?? new UserConfig();
        string? savePath = (path ?? tbGameDownloadPath.Text)?.Trim();
        config.gameDownloadPath = string.IsNullOrWhiteSpace(savePath) ? null : savePath;
        PathUtils.SaveConfig(config);
    }

    private void OnGameDownloadPathChanged(object? sender, TextChangedEventArgs e)
    {
        if (_isApplyingGameDownloadPath)
            return;

        _userPickedGameDownloadPath = !string.IsNullOrWhiteSpace(tbGameDownloadPath.Text);
    }

    private void OnGameDownloadPathLostFocus(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        SaveGameDownloadPathSetting();
    }

    private static string SanitizePathSegment(string value)
    {
        var invalidChars = Path.GetInvalidFileNameChars();
        var chars = value.Select(ch => invalidChars.Contains(ch) ? '_' : ch).ToArray();
        string sanitized = new string(chars).Trim();
        return string.IsNullOrWhiteSpace(sanitized) ? "package" : sanitized;
    }

    private bool ApplyDownloadedGameDefaults(string gameDirectory)
    {
        if (CJKPathGuard.ContainsCJK(gameDirectory))
        {
            LogSys.Err($"[UCMainMenu] ApplyDownloadedGameDefaults: 拒绝写入 CJK 游戏目录 -> {gameDirectory}");
            MainWindow.RaiseMessage(
                CJKPathGuard.RejectMessage,
                "路径包含中文",
                Avalonia.Controls.Notifications.NotificationType.Error);
            return false;
        }

        var config = PathUtils.LoadConfig() ?? new UserConfig();
        config.userGamePath = gameDirectory;
        config.usePreferredGameDownloadVersions = _showOnlyPreferredGameDownloadVersions;

        tbGamePath.Text = gameDirectory;

        string defaultSkinPath = Path.Combine(
            gameDirectory,
            "data",
            "skin_packs",
            "vanilla",
            "steve.png");
        bool defaultSkinExists = File.Exists(defaultSkinPath) && ValidateSkinSize(defaultSkinPath);
        if (defaultSkinExists)
        {
            tbSkinPath.Text = defaultSkinPath;
            tsIsSlimSkin.IsChecked = false;
            config.userSkinPath = defaultSkinPath;
            config.isSkinSlim = false;
        }

        PathUtils.SaveConfig(config);
        if (defaultSkinExists)
        {
            UpdateSkinPreview();
            SaveCurrentSkinToHistory();
        }

        return defaultSkinExists;
    }

    private async void onClickSkinFile(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var files = await MainWindow.Instance.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "选择一个图片",
            AllowMultiple = false,
            FileTypeFilter = new[]
            {
                new FilePickerFileType("图片文件") { Patterns = new[] { "*.png" } },
                FilePickerFileTypes.All
            }
        });

        if (files.Count > 0)
        {
            var file = files[0];
            string? path = file.TryGetLocalPath();

            if (string.IsNullOrEmpty(path))
                return;

            if (CJKPathGuard.ContainsCJK(path))
            {
                MainWindow.RaiseMessage(
                    CJKPathGuard.RejectMessage,
                    "路径包含中文",
                    Avalonia.Controls.Notifications.NotificationType.Error);
                return;
            }

            // 验证皮肤尺寸
            if (!ValidateSkinSize(path))
            {
                MainWindow.RaiseMessage(
                    "不合法的皮肤文件\n\n支持的尺寸: 64x32, 64x64, 128x64, 128x128, 256x256",
                    "错误",
                    Avalonia.Controls.Notifications.NotificationType.Error);
                return;
            }

            tbSkinPath.Text = path;
            SaveCurrentSkinToHistory();
        }
    }
    
    // 验证皮肤图片尺寸是否合法
    private bool ValidateSkinSize(string path)
    {
        try
        {
            using var stream = File.OpenRead(path);
            using var bitmap = new Bitmap(stream);
            
            int width = bitmap.PixelSize.Width;
            int height = bitmap.PixelSize.Height;
            
            // Standard and scaled Minecraft skin sizes.
            return (width == 64 && height == 32) ||
                   (width == 64 && height == 64) ||
                   (width == 128 && height == 64) ||
                   (width == 128 && height == 128) ||
                   (width == 256 && height == 256);
        }
        catch (Exception ex)
        {
            LogSys.Err($"ValidateSkinSize Error: {ex.Message}");
            return false;
        }
    }

    // 提取为独立方法,在主菜单加载时调用
    private void LoadUserConfig()
    {
        if (!File.Exists(PathUtils.UserConfigPath))
            return;

        string ucJson = File.ReadAllText(PathUtils.UserConfigPath);

        if (string.IsNullOrEmpty(ucJson))
            return;

        try
        {
            UserConfig? uc = JsonSerializer.Deserialize(ucJson, UserConfigContext.Default.UserConfig);

            if (uc == null)
                return;

            tbGamePath.Text = uc.userGamePath;
            if (!string.IsNullOrWhiteSpace(uc.userSkinPath) && ValidateSkinSize(uc.userSkinPath))
            {
                tbSkinPath.Text = uc.userSkinPath;
            }
            else if (!string.IsNullOrWhiteSpace(uc.userSkinPath))
            {
                string invalidSkinPath = uc.userSkinPath;
                uc.userSkinPath = null;
                uc.skinHistory?.RemoveAll(item => string.Equals(item.Path, invalidSkinPath, StringComparison.OrdinalIgnoreCase));
                PathUtils.SaveConfig(uc);
                tbSkinPath.Text = string.Empty;
            }
            tsIsSlimSkin.IsChecked = uc.isSkinSlim;
            
            // 加载游戏设置
            tsShowGameConsole.IsChecked = uc.showGameConsole;
            tsEnableDeviceSpoof.IsChecked = uc.enableDeviceSpoof;

            // 设备选择：恢复上次的品牌 → 型号，并同步当前档案
            PopulateDeviceBrands(uc.deviceBrand);
            PopulateDeviceModelsOfBrand(uc.deviceBrand, uc.deviceModelKey);
            DeviceIdentity.Ensure();
            DeviceProfile.SelectPersistent(uc.deviceModelKey, uc.deviceBrand,
                                           DeviceIdentity.Udid, DeviceIdentity.Mac);
            _showOnlyPreferredGameDownloadVersions = uc.usePreferredGameDownloadVersions;
            UpdateGameDownloadVersionFilterButton();
            if (!string.IsNullOrWhiteSpace(uc.gameDownloadPath))
                SetGameDownloadPath(uc.gameDownloadPath, userPicked: true);
            else
                SetDefaultGameDownloadPath(force: true);

            // 皮肤预览默认开启 3D (不保存配置)
            rbPreview3D.IsChecked = true;

            // 更新皮肤预览
            UpdateSkinPreview();
            
            // 加载强调色设置
            if (uc.useCustomAccentColor)
            {
                rbAccentCustom.IsChecked = true;
                siAccentColorPicker.IsVisible = true;
                if (uc.customAccentColor.HasValue)
                {
                    var color = Avalonia.Media.Color.FromUInt32(uc.customAccentColor.Value);
                    App.UpdateAccentColor(color);
                    cpThemeColor.Color = FluentAvalonia.UI.Media.Color2.FromRGB(color.R, color.G, color.B);
                }
            }
            else
            {
                rbAccentSystem.IsChecked = true;
                siAccentColorPicker.IsVisible = false;
            }

            // 加载主题设置
            switch (uc.theme)
            {
                case AppTheme.Light:
                    rbThemeLight.IsChecked = true;
                    break;
                case AppTheme.Dark:
                    rbThemeDark.IsChecked = true;
                    break;
                case AppTheme.System:
                default:
                    rbThemeSystem.IsChecked = true;
                    break;
            }
            
            // 加载皮肤历史
            LoadSkinHistory();

            LoadRenderEngineSettings();
        }
        catch (Exception ex)
        {
            MainWindow.RaiseMessage($"加载用户配置出现错误：{ex.Message}",
                                     "错误",
                                     Avalonia.Controls.Notifications.NotificationType.Error);
            LogSys.Err($"[UCMainMenu] LoadUserConfig Exception: {ex}");
        }
    }

    private void onLoadedOther(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        // 设置页面显示时不需要重复加载,因为已在主菜单加载时加载过了
    }
    private void onUnLoadedOther(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        // 加载现有配置，避免覆盖 skinHistory/customAccentColor 等字段
        var uc = PathUtils.LoadConfig() ?? new UserConfig();
        
        // 只更新需要更新的字段
        uc.isSkinSlim = IsSlimSkin;
        uc.userGamePath = GamePath;
        uc.userSkinPath = SkinPath;
        uc.showGameConsole = tsShowGameConsole.IsChecked ?? false;
        uc.enableDeviceSpoof = tsEnableDeviceSpoof.IsChecked ?? true;
        uc.usePreferredGameDownloadVersions = _showOnlyPreferredGameDownloadVersions;
        string? gameDownloadPath = tbGameDownloadPath.Text?.Trim();
        uc.gameDownloadPath = string.IsNullOrWhiteSpace(gameDownloadPath) ? null : gameDownloadPath;
        
        // 获取当前主题设置
        if (rbThemeLight.IsChecked == true)
            uc.theme = AppTheme.Light;
        else if (rbThemeDark.IsChecked == true)
            uc.theme = AppTheme.Dark;
        else
            uc.theme = AppTheme.System;
            
        PathUtils.SaveConfig(uc);
    }
    
    private void OnThemeChanged(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (sender is not RadioButton rb) return;
        if (rb.IsChecked != true) return;
        
        AppTheme theme = AppTheme.System;
        if (rb == rbThemeLight)
            theme = AppTheme.Light;
        else if (rb == rbThemeDark)
            theme = AppTheme.Dark;
        else if (rb == rbThemeSystem)
            theme = AppTheme.System;
            
        App.SetTheme(theme);
    }
    
    private void OnAccentColorModeChanged(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (sender is not RadioButton rb) return;
        if (rb.IsChecked != true) return;
        
        bool useCustom = rb == rbAccentCustom;
        siAccentColorPicker.IsVisible = useCustom;
        
        var config = PathUtils.LoadConfig() ?? new UserConfig();
        config.useCustomAccentColor = useCustom;
        
        if (!useCustom)
        {
            // 恢复系统强调色
            App.RestoreSystemAccentColor();
        }
        else if (config.customAccentColor.HasValue)
        {
            // 应用已保存的自定义颜色
            var color = Avalonia.Media.Color.FromUInt32(config.customAccentColor.Value);
            App.UpdateAccentColor(color);
            cpThemeColor.Color = FluentAvalonia.UI.Media.Color2.FromRGB(color.R, color.G, color.B);
        }
        
        PathUtils.SaveConfig(config);
    }
    
    private void OnColorPickerColorChanged(FluentAvalonia.UI.Controls.ColorPickerButton sender, FluentAvalonia.UI.Controls.ColorButtonColorChangedEventArgs args)
    {
        if (args.NewColor == null) return;
        
        var color2 = args.NewColor.Value;
        var color = Avalonia.Media.Color.FromRgb(color2.R, color2.G, color2.B);
        
        App.UpdateAccentColor(color);
        
        // 保存配置
        var config = PathUtils.LoadConfig() ?? new UserConfig();
        config.customAccentColor = color.ToUInt32();
        config.useCustomAccentColor = true;
        PathUtils.SaveConfig(config);
    }
    
    private void OnPresetColorClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (sender is not Button btn) return;
        if (btn.Tag is not string colorHex) return;
        
        try
        {
            var color = Avalonia.Media.Color.Parse(colorHex);
            App.UpdateAccentColor(color);
            cpThemeColor.Color = FluentAvalonia.UI.Media.Color2.FromRGB(color.R, color.G, color.B);
            
            // 保存配置
            var config = PathUtils.LoadConfig() ?? new UserConfig();
            config.customAccentColor = color.ToUInt32();
            config.useCustomAccentColor = true;
            PathUtils.SaveConfig(config);
        }
        catch { }
    }

    private void OnGameConsoleToggled(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var config = PathUtils.LoadConfig() ?? new UserConfig();
        config.showGameConsole = tsShowGameConsole.IsChecked ?? false;
        PathUtils.SaveConfig(config);
    }
    
    private void OnDeviceSpoofToggled(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var config = PathUtils.LoadConfig() ?? new UserConfig();
        config.enableDeviceSpoof = tsEnableDeviceSpoof.IsChecked ?? true;
        PathUtils.SaveConfig(config);
    }

    // 设备选择是三级联动：品牌 → 型号 → 设备码。
    // 设备码由型号固定推导（DeviceProfile.DeriveUdid），不可手改，
    // 保证同一型号每次都得到同一个设备码。
    private void PopulateDeviceBrands(string? selectedBrand)
    {
        if (cbDeviceBrand is null)
        {
            return;
        }

        var items = new List<DeviceBrandOption>
        {
            new(null, "随机（每次启动随机挑选）")
        };

        foreach (var brand in DeviceProfile.Brands)
        {
            items.Add(new DeviceBrandOption(brand, $"{brand}（{DeviceProfile.ModelsOf(brand).Count}）"));
        }

        cbDeviceBrand.ItemsSource = items;
        cbDeviceBrand.SelectedItem =
            items.FirstOrDefault(o => string.Equals(o.Brand, selectedBrand, StringComparison.OrdinalIgnoreCase))
            ?? items[0];
    }

    // 按当前品牌刷新型号下拉。
    private void PopulateDeviceModelsOfBrand(string? brand, string? selectedModelKey)
    {
        if (cbDeviceModel is null)
        {
            return;
        }

        var items = new List<DeviceModelOption>();

        if (string.IsNullOrWhiteSpace(brand))
        {
            // 随机模式：型号下拉不可选，只显示提示
            items.Add(new DeviceModelOption(null, "随机（每次启动随机挑选）"));
            cbDeviceModel.ItemsSource = items;
            cbDeviceModel.SelectedItem = items[0];
            cbDeviceModel.IsEnabled = false;
            UpdateDeviceIdDisplay(null);
            return;
        }

        items.Add(new DeviceModelOption(null, "随机（该品牌内随机）"));

        foreach (var entry in DeviceProfile.ModelsOf(brand))
        {
            items.Add(new DeviceModelOption(entry.Key, entry.Model, entry.DisplayName));
        }

        cbDeviceModel.IsEnabled = true;
        cbDeviceModel.ItemsSource = items;
        cbDeviceModel.SelectedItem =
            items.FirstOrDefault(o => string.Equals(o.Key, selectedModelKey, StringComparison.OrdinalIgnoreCase))
            ?? items[0];

        UpdateDeviceIdDisplay((cbDeviceModel.SelectedItem as DeviceModelOption)?.DeviceLabel,
                              (cbDeviceModel.SelectedItem as DeviceModelOption)?.Key);
    }

    // 刷新设备码显示。设备码每次选定设备时重新随机生成，
    // 因此这里只展示「已生成/未生成」状态，具体值在启动时写入。
    private void UpdateDeviceIdDisplay(string? deviceLabel, string? key = null)
    {
        if (tbDeviceId is null)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(key))
        {
            tbDeviceId.Text = "—（随机时启动才确定）";
            return;
        }

        // 预览一个本机设备码样例，实际值在每次启动时重新生成
        tbDeviceId.Text = $"{DeviceProfile.RandomHex16()}（每次设置型号重新随机）";
    }

    private void OnDeviceBrandChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (cbDeviceBrand?.SelectedItem is not DeviceBrandOption option)
        {
            return;
        }

        var config = PathUtils.LoadConfig() ?? new UserConfig();
        config.deviceBrand = option.Brand;

        // 换品牌时型号回到该品牌的第一项（随机）
        config.deviceModelKey = null;
        PathUtils.SaveConfig(config);

        PopulateDeviceModelsOfBrand(option.Brand, null);
        DeviceIdentity.Ensure();
        DeviceProfile.SelectPersistent(null, option.Brand, DeviceIdentity.Udid, DeviceIdentity.Mac);
    }

    private void OnDeviceModelChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (cbDeviceModel?.SelectedItem is not DeviceModelOption option)
        {
            return;
        }

        var config = PathUtils.LoadConfig() ?? new UserConfig();
        config.deviceModelKey = option.Key;
        PathUtils.SaveConfig(config);

        UpdateDeviceIdDisplay(option.DeviceLabel, option.Key);

        // 立刻生效，本次启动游戏即使用新选定的机型
        DeviceIdentity.Ensure();
        DeviceProfile.SelectPersistent(option.Key, null, DeviceIdentity.Udid, DeviceIdentity.Mac);
    }

    // 品牌下拉项。Brand 为 null 表示随机。
    private sealed record DeviceBrandOption(string? Brand, string DisplayName)
    {
        public override string ToString() => DisplayName;
    }

    // 型号下拉项。Key 为 null 表示该品牌内随机。
    private sealed record DeviceModelOption(string? Key, string DeviceLabel, string Detail = "")
    {
        public override string ToString() =>
            string.IsNullOrEmpty(Detail) ? DeviceLabel : $"{DeviceLabel} · {Detail}";
    }

    private void LoadRenderEngineSettings()
    {
        ApplyRenderEngineGroup(GameEdition.NeteasePE, rbPEOpenGL, rbPERenderDragon);
        ApplyRenderEngineGroup(GameEdition.NeteasePB, rbPBOpenGL, rbPBRenderDragon);
    }

    private static void ApplyRenderEngineGroup(GameEdition edition,
                                              Avalonia.Controls.RadioButton rbOpenGL,
                                              Avalonia.Controls.RadioButton rbRD)
    {
        if (!RenderEngineHelper.TryReadRenderEngine(edition, out RenderEngine current))
        {
            rbOpenGL.IsEnabled = false;
            rbRD.IsEnabled = false;
            return;
        }

        rbOpenGL.IsEnabled = true;
        rbRD.IsEnabled = true;
        (current == RenderEngine.OpenGL ? rbOpenGL : rbRD).IsChecked = true;
    }

    private void OnPERenderEngineClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (sender is not Avalonia.Controls.RadioButton rb || rb.IsChecked != true) return;
        HandleRenderEngineChange(GameEdition.NeteasePE, rb == rbPEOpenGL ? RenderEngine.OpenGL : RenderEngine.RenderDragon);
    }

    private void OnPBRenderEngineClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (sender is not Avalonia.Controls.RadioButton rb || rb.IsChecked != true) return;
        HandleRenderEngineChange(GameEdition.NeteasePB, rb == rbPBOpenGL ? RenderEngine.OpenGL : RenderEngine.RenderDragon);
    }

    private void HandleRenderEngineChange(GameEdition edition, RenderEngine engine)
    {
        bool ok = RenderEngineHelper.WriteRenderEngine(edition, engine);
        if (!ok)
        {
            MainWindow.RaiseMessage("写入 options.txt 失败",
                                    "渲染引擎",
                                    Avalonia.Controls.Notifications.NotificationType.Error);
            return;
        }

        MainWindow.RaiseMessage(edition == GameEdition.NeteasePE
                                    ? "网易原版渲染引擎已切换"
                                    : "基岩互通版渲染引擎已切换",
                                "渲染引擎",
                                Avalonia.Controls.Notifications.NotificationType.Success);
    }

    private void OnSkinPathChanged(object? sender, TextChangedEventArgs e)
    {
        UpdateSkinPreview();
    }

    private void OnSkinSlimToggled(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        UpdateSkinPreview();
        SaveCurrentSkinToHistory();
    }
    
    private void SaveCurrentSkinToHistory()
    {
        if (string.IsNullOrEmpty(tbSkinPath.Text) || !File.Exists(tbSkinPath.Text) || !ValidateSkinSize(tbSkinPath.Text))
            return;
            
        var config = PathUtils.LoadConfig() ?? new UserConfig();
        config.skinHistory ??= new List<SkinHistoryItem>();
        
        // 检查是否已存在
        var existing = config.skinHistory.FirstOrDefault(x => x.Path == tbSkinPath.Text);
        if (existing != null)
        {
            existing.IsSlim = tsIsSlimSkin.IsChecked ?? false;
            existing.LastUsed = DateTime.Now;
        }
        else
        {
            config.skinHistory.Insert(0, new SkinHistoryItem
            {
                Path = tbSkinPath.Text,
                IsSlim = tsIsSlimSkin.IsChecked ?? false,
                LastUsed = DateTime.Now
            });
        }
        
        // 最多保留 10 条
        if (config.skinHistory.Count > 10)
        {
            config.skinHistory = config.skinHistory
                .OrderByDescending(x => x.LastUsed)
                .Take(10)
                .ToList();
        }
        
        PathUtils.SaveConfig(config);
        LoadSkinHistory();
    }
    
    private void LoadSkinHistory()
    {
        var config = PathUtils.LoadConfig();
        if (config?.skinHistory == null || config.skinHistory.Count == 0)
        {
            icSkinHistory.ItemsSource = null;
            return;
        }
        
        var historyButtons = new List<Button>();
        foreach (var item in config.skinHistory.OrderByDescending(x => x.LastUsed).Take(5))
        {
            if (string.IsNullOrEmpty(item.Path) || !File.Exists(item.Path))
                continue;
                
            var btn = new Button
            {
                Width = 56,
                Height = 56,
                Padding = new Avalonia.Thickness(4),
                Tag = item,
                CornerRadius = new Avalonia.CornerRadius(8)
            };
            ToolTip.SetTip(btn, Path.GetFileName(item.Path));
            
            // 生成小预览图
            try
            {
                var preview = SkinPreviewer.RenderSkinPreview(item.Path, item.IsSlim, false, 0, 0);
                btn.Content = new Image
                {
                    Source = preview,
                    Stretch = Avalonia.Media.Stretch.Uniform
                };
            }
            catch
            {
                btn.Content = new TextBlock { Text = "👤", FontSize = 16 };
            }
            
            btn.Click += OnSkinHistoryItemClick;
            historyButtons.Add(btn);
        }
        
        icSkinHistory.ItemsSource = historyButtons;
    }
    
    private void OnSkinHistoryItemClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (sender is not Button btn) return;
        if (btn.Tag is not SkinHistoryItem item) return;
        
        tbSkinPath.Text = item.Path;
        tsIsSlimSkin.IsChecked = item.IsSlim;
        UpdateSkinPreview();
        
        // 更新最后使用时间
        var config = PathUtils.LoadConfig() ?? new UserConfig();
        var existing = config.skinHistory?.FirstOrDefault(x => x.Path == item.Path);
        if (existing != null)
        {
            existing.LastUsed = DateTime.Now;
            PathUtils.SaveConfig(config);
        }
        
        MainWindow.RaiseMessage($"已切换到皮肤: {Path.GetFileName(item.Path)}", "提示", Avalonia.Controls.Notifications.NotificationType.Success);
    }
    
    private void OnClearSkinHistoryClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var config = PathUtils.LoadConfig() ?? new UserConfig();
        config.skinHistory = new List<SkinHistoryItem>();
        PathUtils.SaveConfig(config);
        icSkinHistory.ItemsSource = null;
        MainWindow.RaiseMessage("皮肤历史已清空", "提示", Avalonia.Controls.Notifications.NotificationType.Success);
    }

    private void OnPreviewModeChanged(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        UpdateAnimTimerState();
        UpdateSkinPreview();
    }

    private void OnSkinPreviewPointerPressed(object? sender, Avalonia.Input.PointerPressedEventArgs e) { }
    private void OnSkinPreviewPointerMoved(object? sender, Avalonia.Input.PointerEventArgs e) { }
    private void OnSkinPreviewPointerReleased(object? sender, Avalonia.Input.PointerReleasedEventArgs e) { }

    private void UpdateSkinPreview()
    {
        if (string.IsNullOrEmpty(tbSkinPath.Text) || !File.Exists(tbSkinPath.Text) || !ValidateSkinSize(tbSkinPath.Text))
        {
            imgSkinPreview.Source = null;
            return;
        }

        try
        {
            bool is3D = rbPreview3D.IsChecked ?? true;
            var preview = SkinPreviewer.RenderSkinPreview(tbSkinPath.Text, tsIsSlimSkin.IsChecked ?? false, is3D, -30, _skinAnimPhase);
            imgSkinPreview.Source = preview;
        }
        catch (Exception ex)
        {
            LogSys.Log($"更新皮肤预览失败: {ex.Message}");
            imgSkinPreview.Source = null;
        }
    }

    
    private void LoadGamePlayStats()
    {
        try
        {
            var stats = GamePlayTracker.GetStats();
            
            if (stats == null)
            {
                tbTotalPlayTime.Text = "0小时";
                tbTodayPlayTime.Text = "0分钟";
                tbGameSessions.Text = "0次";
                return;
            }
            
            // 总游戏时长
            tbTotalPlayTime.Text = GamePlayTracker.FormatDuration(stats.TotalPlayTimeSeconds);
            
            // 今日游戏时长
            var todaySeconds = GamePlayTracker.GetTodayPlayTime();
            tbTodayPlayTime.Text = GamePlayTracker.FormatDuration(todaySeconds);
            
            // 游戏次数
            tbGameSessions.Text = $"{stats.TotalGameSessions}次";
        }
        catch (Exception ex)
        {
            LogSys.Err($"[UCMainMenu] LoadGamePlayStats: {ex}");
        }
    }

}
