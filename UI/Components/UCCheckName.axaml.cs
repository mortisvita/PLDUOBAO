using System;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using Elysium.Core.Utils;
using PLDUOBAO.Network.Netease;
using PLDUOBAO.Util;
using static PLDUOBAO.Network.Netease.NetworkLocalAPI;

namespace PLDUOBAO;

public partial class UCCheckName : UserControl
{
    public UCCheckName()
    {
        InitializeComponent();
    }

    private void NextPage()
    {
        Dispatcher.UIThread.Invoke(() =>
        {
            UCMainMenu mainmenu = new UCMainMenu(this);
            MainWindow.GridMainMenu.Children.Add(mainmenu);
            MainWindow.GridMainMenu.Children.Remove(this);
        });
    }

    private async Task DoCheckName()
    {
        try
        {
            var user = CurrentAvaliableUser;
            if (user == null)
            {
                return;
            }
            var UserDetail = await NeteasePosts.GetUserDetailAsync();
            if (UserDetail == null || UserDetail.entity == null)
            {
                return;
            }
            CurrentLevel = UserDetail.entity.level;

            if (string.IsNullOrEmpty(UserDetail.entity.name) || string.IsNullOrWhiteSpace(UserDetail.entity.name))
            {
                Dispatcher.UIThread.Invoke(() =>
                {
                    tbTitle.Text = "该账号还没有设置名字，请设置一个名字";
                    inputPanel.IsVisible = true;
                    loadingRing.IsVisible = false;
                });

                return;
            }
            CurrentUserName = UserDetail.entity.name;

            NextPage();
        }
        catch (Exception ex)
        {
            MainWindow.RaiseMessage($"检查名字失败：{ex.Message}", "错误", Avalonia.Controls.Notifications.NotificationType.Error);
            LogSys.Err($"{ex}");
        }
    }

    private async void onLoaded(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (CurrentAvaliableUser == null)
        {
#if !DEBUG
            MainWindow.RaiseMessage("出现了一个意外错误，请联系开发者", "提示");

            ResetUser();

            MainWindow.GridMainMenu.Children.Add(MainWindow.UCCookieSelector!);
            MainWindow.GridMainMenu.Children.Remove(this);
#endif

            return;
        }
        await DoCheckName();
    }

    private async void onClickConfirm(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(this.tboxNameEnter.Text))
        {
            MainWindow.RaiseMessage("名字不能为空，长度不超过8个中文或16个英文/数字",
                                    "错误",
                                    Avalonia.Controls.Notifications.NotificationType.Error);
            return;
        }

        try
        {
            var result = await NeteasePosts.SetNickNameAsync(this.tboxNameEnter.Text);

            if (result == null)
                return;

            if (result.code != 0 && result.code != 20)
            {
                MainWindow.RaiseMessage($"失败：{result.message}，状态码：{result.code}",
                                        "错误",
                                        Avalonia.Controls.Notifications.NotificationType.Error);
                return;
            }

            if (result.code == 20)
            {
                await DoCheckName();
                return;
            }

            CurrentUserName = this.tboxNameEnter.Text;

            NextPage();
        }
        catch (Exception ex)
        {
            LogSys.Err($"SetName Error : {ex}");
        }
    }

    private void onClickRandom(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        string username = InternationalNameGenerator.Generate();
        tboxNameEnter.Text = username;
    }

    private void onClickChineseRandom(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        string username = NameGenerator.GenerateRandomName();
        tboxNameEnter.Text = username;
    }
}
