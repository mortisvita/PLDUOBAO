using System;
using System.Diagnostics;
using System.IO;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using Elysium.Core.Utils;
using PLDUOBAO.Network.LocalAuthServer;
using PLDUOBAO.Network.Netease;
using PLDUOBAO.Network.Netease.NetworkSDK;
using PLDUOBAO.Network.Netease.NetworkSDK.PECommon;
using PLDUOBAO.Network.NewVerify;
using PLDUOBAO.Util;

namespace PLDUOBAO;

internal partial class UCJoinGame : UserControl
{
    public NetGameServerAddress? ngsa { get; set; } = null;

    public NetGameItem? ngi { get; set; } = null;

    public RentalGameServerAddress? rgsa { get; set; } = null;
    public string serverItemId { get; set; } = "";
    public string? serverName { get; set; } = null;

    private bool isRental { get; set; } = false;
    public UCJoinGame()
    {
        InitializeComponent();
    }

    public UCJoinGame(NetGameServerAddress ngsa, NetGameItem ngi)
    {
        InitializeComponent();
        this.ngsa = ngsa;
        this.ngi = ngi;
        this.serverItemId = ngi.ItemId;
        this.serverName = ngi.ResName;
        this.isRental = false;
    }

    public UCJoinGame(RentalGameServerAddress rgsa, RentalGameItem rgi)
    {
        InitializeComponent();

        this.ngsa = new NetGameServerAddress { Host = rgsa.Host, Port = rgsa.Port };
        this.serverName = rgi.ServerName;
        if (string.IsNullOrWhiteSpace(rgi.EntityId))
            return;

        this.serverItemId = rgi.EntityId;
        this.isRental = true;
    }

    private double LoadingProgress
    {
        get
        {
            Dispatcher.UIThread.Invoke(() =>
            {
                return pbGameState.Value;
            });
            return pbGameState.Value;
        }
        set
        {
            Dispatcher.UIThread.Invoke(() =>
            {
                pbGameState.Value = value;
            });
        }
    }

    private void SelfDestruct()
    {
        Dispatcher.UIThread.Invoke(() => 
        {
            MainWindow.GridMainMenu.Children.Remove(this);
        }); 
    }

    private async void onLoaded(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        try
        {
            if (this.ngsa == null)
            {
                SelfDestruct();
                return;
            }

            bool isOldAuth = false;

            tbServerIP.Text = $"正在连接服务器：{ngsa.Host}:{ngsa.Port}";

            tbNowInfo.Text = "正在启动认证服务...（请勿关闭程序）";

            LoadingProgress = 5;

            if (isOldAuth)
            {

            }
            else AuthServerHost.StartAsync(NetworkUtil.GetAvailablePort(19132, 25564), serverItemId, isRental);


            tbNowInfo.Text = "正在准备启动游戏...（加载配置）";

            LoadingProgress = 10;

            if (NetworkLocalAPI.CurrentAvaliableUser == null)
            {
                SelfDestruct();
                return;
            }


            if (string.IsNullOrEmpty(MainWindow.UCMainmenu?.SkinPath) ||
                string.IsNullOrEmpty(MainWindow.UCMainmenu?.GamePath))
            {
                MainWindow.RaiseMessage("请先设置皮肤路径和游戏路径", "错误", Avalonia.Controls.Notifications.NotificationType.Error);
                SelfDestruct();
                return;
            }

            if (!File.Exists(Path.Combine(MainWindow.UCMainmenu?.GamePath!, "Minecraft.Windows.exe")))
            {
                MainWindow.RaiseMessage("游戏路径无效，找不到游戏文件", "错误", Avalonia.Controls.Notifications.NotificationType.Error); 
                SelfDestruct();
                return;
            }

            if (!File.Exists(MainWindow.UCMainmenu?.SkinPath!))
            {
                MainWindow.RaiseMessage("皮肤文件不存在", "错误", Avalonia.Controls.Notifications.NotificationType.Error);
                SelfDestruct();
                return;
            }

            int port = 0;

            if (isOldAuth)
            {

            }
            else
                port = AuthServerHost.CurrentRunningPort;

            var cfg = CPPLaunchConfig.GenerateNew(ngsa.Host,
                                                  ngsa.Port,
                                                  MainWindow.UCMainmenu?.SkinPath!,
                                                  MainWindow.UCMainmenu?.IsSlimSkin == true,
                                                  NetworkLocalAPI.CurrentAvaliableUser,
                                                  NetworkLocalAPI.CurrentUserName,
                                                  port
                                                 );

            var options = new JsonSerializerOptions
            {
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
                WriteIndented = true,
            };
            var context = new CPPLaunchConfigContext(options);

            string launchConfig = "";

            try
            {
                launchConfig = JsonSerializer.Serialize(cfg, context.CPPLaunchConfig);
            }
            catch (Exception ex)
            {
                MainWindow.RaiseMessage($"序列化失败：{ex.Message}", "错误", Avalonia.Controls.Notifications.NotificationType.Error);
                LogSys.Err(ex);
                SelfDestruct();
                return;
            }

            if (string.IsNullOrEmpty(launchConfig))
            {
                SelfDestruct();
                return;
            }

            await File.WriteAllTextAsync(PathUtils.LaunchConfigPath, launchConfig);

            LoadingProgress = 75;

            tbNowInfo.Text = "正在启动游戏...";

            var proc = await GameLauncher.StartGameWithAuthAsync(MainWindow.UCMainmenu?.GamePath!, PathUtils.LaunchConfigPath, serverName, isRental ? "Rental" : "NetGame");

            AuthServerHost.proc = proc;

            MainWindow.RaiseMessage("游戏启动成功", "提示", Avalonia.Controls.Notifications.NotificationType.Success);

            SelfDestruct();
            return;
        }
        catch (Exception ex)
        {
            MainWindow.RaiseMessage($"启动失败：{ex.Message}", "错误", Avalonia.Controls.Notifications.NotificationType.Error);
        }
    }
}
