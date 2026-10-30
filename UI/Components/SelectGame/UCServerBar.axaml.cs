using System;
using System.IO;
using System.Net.NetworkInformation;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Input;
using Avalonia.Threading;
using Elysium.Core.Utils;
using PLDUOBAO.Network.Netease;
using PLDUOBAO.Network.Netease.NetworkSDK.PECommon;
using PLDUOBAO.Util;

namespace PLDUOBAO;

internal partial class UCServerBar : UserControl
{
    public NetGameItem? ngi = null;
    private bool _pingRequested;

    public UCServerBar()
    {
        InitializeComponent();
        PointerEntered += OnPointerEntered;
    }

    public UCServerBar(NetGameItem ngi)
    {
        InitializeComponent();
        this.ngi = ngi;
    }

    public async Task LoadServerIcon()
    {
        await using (var imageStream = await PathUtils.LoadServerIconAsync(this))
        {
            if (imageStream == null)
            {
                return;
            }

            imageStream.Seek(0, SeekOrigin.Begin);
            var bmp = await Task.Run(() => Bitmap.DecodeToWidth(imageStream, 400));

            if (bmp == null)
            {
                return;
            }

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (imgServerIcon != null)
                {
                    imgServerIcon.Source = bmp;
                    iconPlaceholder.IsVisible = false;
                }
            });
        }
    }

    private async void onLoaded(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        ngi ??= DataContext as NetGameItem;
        if (ngi == null)
            return;

        try
        {
            if (string.IsNullOrWhiteSpace(ngi.RunningStatus))
                ngi.RunningStatus = "normal";

            tbServerName.Text = ngi.ResName;
            tbServerOnlineCount.Text = $"在线人数：{ngi.OnlineNum}";
            tbServerOnlineStatus.Text = ngi.RunningStatus;
            tbServerOnlineStatus.Foreground = ngi.RunningStatus == "normal" ? Brushes.Green : Brushes.Red;
            tbEntityId.Text = $"ID: {ngi.EntityId}";
            tbServerPing.Text = "--";
            tbServerAddress.Text = "连接: --";

            Dispatcher.UIThread.Post(() => _ = LoadServerIcon(), DispatcherPriority.Background);
        }
        catch (Exception ex)
        {
            MainWindow.RaiseMessage($"加载服务器信息出错：{ex.Message}", "错误",
                Avalonia.Controls.Notifications.NotificationType.Error);
        }
    }

    private void OnPointerEntered(object? sender, PointerEventArgs e)
    {
        if (_pingRequested || ngi == null)
            return;

        _pingRequested = true;
        _ = CheckServerPingAsync();
    }
    
    private async Task CheckServerPingAsync()
    {
        try
        {
            var result = await NeteasePosts.GetNetGameServerAddress(ngi?.ItemId ?? "");
            if (result?.entity == null) 
            {
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    tbServerPing.Text = "N/A";
                    tbServerAddress.Text = "连接: N/A";
                });
                return;
            }
            
            string host = result.entity.Host;
            int port = result.entity.Port;
            
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                tbServerAddress.Text = $"连接: {host}:{port}";
            });
            
            using var ping = new Ping();
            var reply = await ping.SendPingAsync(host, 3000);
            
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (reply.Status == IPStatus.Success)
                {
                    long ms = reply.RoundtripTime;
                    tbServerPing.Text = $"{ms}ms";
                    
                    if (ms < 50)
                        tbServerPing.Foreground = new SolidColorBrush(Color.Parse("#10893E"));
                    else if (ms < 100)
                        tbServerPing.Foreground = new SolidColorBrush(Color.Parse("#CA5010"));
                    else
                        tbServerPing.Foreground = new SolidColorBrush(Color.Parse("#E81123"));
                }
                else
                {
                    tbServerPing.Text = "超时";
                    tbServerPing.Foreground = new SolidColorBrush(Color.Parse("#E81123"));
                }
            });
        }
        catch
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                tbServerPing.Text = "--";
                tbServerAddress.Text = "连接: --";
            });
        }
    }

    private async void onJoinGame(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (ngi == null)
        {
            LogSys.Err("UCServerBar::onJoinGame ngi is null");
            return;
        }

        try
        {
            if (string.IsNullOrEmpty(MainWindow.UCMainmenu?.SkinPath) ||
                string.IsNullOrEmpty(MainWindow.UCMainmenu?.GamePath))
            {
                MainWindow.RaiseMessage("请先设置游戏路径和皮肤路径", "错误", Avalonia.Controls.Notifications.NotificationType.Error);
                return;
            }

            var result = await NeteasePosts.GetNetGameServerAddress(ngi.ItemId);

            if (result == null || result.entity == null)
                return;

            UCJoinGame jg = new UCJoinGame(result.entity,ngi);
            jg.ZIndex = 20;
            MainWindow.GridMainMenu.Children.Add(jg);

        }
        catch (Exception ex)
        {
            MainWindow.RaiseMessage($"获取服务器IP地址失败：{ex.Message}",
                                     "错误",
                                     Avalonia.Controls.Notifications.NotificationType.Error);
            LogSys.Err(ex);
        }
    }
}
