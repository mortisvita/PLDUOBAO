using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using Elysium.Core.Utils;
using PLDUOBAO.Network.Netease.NetworkSDK.LoginAuth;
using PLDUOBAO.Network.Netease;
using PLDUOBAO.SDK;
using PLDUOBAO.Util;
using PLDUOBAO.Network.MPay;
using PLDUOBAO.Network.Netease.NetworkSDK.Cookie;
using System.Text.Json;
using PLDUOBAO.Network.Netease.NetworkSDK;
using System.Diagnostics;
using static PLDUOBAO.Network.Netease.NetworkSDK.LoginAuth.SercurityAuth;
using Avalonia.Controls.Documents;
using PLDUOBAO.Network.Com4399;
using PLDUOBAO.Network.Com4399.SDK.Exceptions;
using Avalonia.Media.Imaging;
using Org.BouncyCastle.Utilities.Encoders;
using System.Text.RegularExpressions;
using System.Text;
using PLDUOBAO.Network.Netease.NetworkSDK.LoginAuth.PELoginSDK.Exceptions;
using FluentAvalonia.UI.Controls;
using PLDUOBAO.Network.NewVerify;
using Avalonia.Platform.Storage;

namespace PLDUOBAO;

public partial class UCCookieSelector : UserControl
{
    private X4399Service? service;

    public UCCookieSelector()
    {
        InitializeComponent();
    }

    public void ReloadUsers()
    {
        listCookieList.Items.Clear();

        foreach (var cookie in LocalAccountManager.Users)
        {
            var selector = new UCAccountSelector(cookie, this);
            listCookieList.Items.Add(selector);
        }
    }

    private void onCreateNewCookie(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        WDAddCookie addCookieWindow = new WDAddCookie(this);
        addCookieWindow.Show();
    }

    private void onExportAccounts(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (LocalAccountManager.Users == null || LocalAccountManager.Users.Count == 0)
        {
            MainWindow.RaiseMessage("当前没有任何账户", "提示", Avalonia.Controls.Notifications.NotificationType.Warning);
            return;
        }

        if (MainWindow.GridMainMenu == null) return;

        var container = MainWindow.GridMainMenu;
        // 防重复 - 如果当前已有 export dialog 没关，不要再开
        foreach (var child in container.Children)
        {
            if (child is UCDialogAccountExport) return;
        }

        UCDialogAccountExport.ShowModal(container);
    }

    private async void onLoaded(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (MainWindow.UCCookieSelector != this)
        {
            MainWindow.UCCookieSelector = this;
        }


        loginOverlay.IsVisible = false;

        // 触发入场动画
        await Task.Delay(50);
        mainGrid.Classes.Add("show");

        // 加载配置
        LoadSettings();

        LocalAccountManager.LoadUsers();
    }

    private void LoadSettings()
    {
        try
        {
            var config = PathUtils.LoadConfig();
            if (config != null)
            {
                // 应用主题
                App.SetTheme(config.theme);
            }
        }
        catch
        {
        }
    }

    // 选项卡切换处理
    private void OnTabAccountsClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        pageAccounts.IsVisible = true;
        pageLogin.IsVisible = false;
        tabAccounts.Classes.Set("inactive", false);
        tabLogin.Classes.Set("inactive", true);
    }

    private void OnTabLoginClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        pageAccounts.IsVisible = false;
        pageLogin.IsVisible = true;
        tabAccounts.Classes.Set("inactive", true);
        tabLogin.Classes.Set("inactive", false);
    }

    private void onRefresh(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        onLoaded(sender, e);
    }


    private Task ShowLoginOverlay(string status)
    {
        loginLoadCard.BigTitle = status;
        loginLoadCard.Message = "请稍候";
        loginOverlay.IsVisible = true;
        return Task.CompletedTask;
    }

    private Task HideLoginOverlay()
    {
        loginOverlay.IsVisible = false;
        return Task.CompletedTask;
    }

    private string GetLoginInfo(PEAuth.LoginState state)
    {
        switch (state)
        {
            case PEAuth.LoginState.Success:
                return "登录成功";
            case PEAuth.LoginState.TokenExpired:
                return "Cookie 已过期，请重新获取";
            case PEAuth.LoginState.InvaildCookie:
                return "Cookie 无效，请检查格式";
            case PEAuth.LoginState.InvaildDevice:
                return "设备验证失败，请重试";
            case PEAuth.LoginState.AccountBanned:
                return "账号已被封禁";
            case PEAuth.LoginState.Failed:
                return "登录失败";
            default:
                return "未知错误";
        }
    }

    private void onClickSendVerifyCode(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        MainWindow.RaiseMessage("此功能尚未实现",
            "提示",
            Avalonia.Controls.Notifications.NotificationType.Error);
    }

    public async Task<string> Get4399LoginToken(string username, string password, string? verifyCode = "")
    {
        if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
        {
            MainWindow.RaiseMessage("账号或密码不能为空",
                "错误",
                Avalonia.Controls.Notifications.NotificationType.Error);
            return "";
        }

        if (service == null)
            service = await X4399Service.GetServiceAsync();

        var result = await service.LoginAndAuthorize(username, password, verifyCode, service.CurrentCaptchaId);

        // result 是完整 sauth_json（含 sessionid / udid），只报摘要
        LogSys.Log($"4399Cookie:{LogRedactor.Brief(result)}");

        if (string.IsNullOrEmpty(result))
            throw new Exception("获取 Cookie 失败，请检查账号密码");

        return result;
    }

    public async void onLogin4399(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        string? username = tbUsername4399.Text;
        string? password = tbPassword4399.Text;
        string? verifyCode = tbVerifyCode4399.Text;

        if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
        {
            MainWindow.RaiseMessage("账号或密码不能为空",
                "错误",
                Avalonia.Controls.Notifications.NotificationType.Error);
            return;
        }

        btnLogin4399.IsEnabled = false; // 禁用按钮
        await ShowLoginOverlay("正在获取 4399 登录信息...");

        try
        {
            var result = await Get4399LoginToken(username, password, verifyCode);

            if (string.IsNullOrEmpty(result))
                throw new Exception("获取 Cookie 失败");

            loginLoadCard.BigTitle = "正在登录...";

            var state = await PEAuth.Login(result);

            MainWindow.RaiseMessage($"登录结果：{GetLoginInfo(state)}",
                "提示",
                Avalonia.Controls.Notifications.NotificationType.Information);

            if (state == PEAuth.LoginState.Success && NetworkLocalAPI.CurrentAvaliableUser != null)
            {
                loginLoadCard.BigTitle = "登录成功！";
                await Task.Delay(300);

                NetworkLocalAPI.Current4399Username = tbUsername4399.Text;
                NetworkLocalAPI.Current4399Password = tbPassword4399.Text;
                NetworkLocalAPI.CurrentPlatform = UserCookie.CookiePlatform.com4399;

                if (spVerifyCode.IsVisible)
                {
                    tbVerifyCode4399.Text = "";
                    spVerifyCode.IsVisible = false;
                }

                tbUsername4399.Text = "";
                tbPassword4399.Text = "";

                UCCheckName cn = new UCCheckName();
                MainWindow.GridMainMenu.Children.Add(cn);
                MainWindow.GridMainMenu.Children.Remove(this);
            }
            else
            {
                await HideLoginOverlay();
            }
        }
        catch (RealNameRequiredException rnex)
        {
            await HideLoginOverlay();
            MainWindow.RaiseMessage("需要实名认证，请先在 4399 官网完成认证",
                "错误",
                Avalonia.Controls.Notifications.NotificationType.Error);
            LogSys.Err($"[X4399Service] ShouldRealNameVerify: {rnex.Message}");

        }
        catch (ShouldCaptchaException scex)
        {
            await HideLoginOverlay();
            spVerifyCode.IsVisible = true;
            if (scex.ImageBytes == null)
            {
                spVerifyCode.IsVisible = false;
                throw new Exception("获取验证码失败");
            }

            Bitmap bitmap;
            using (var ms = new MemoryStream(scex.ImageBytes))
            {
                bitmap = new Bitmap(ms);
            }

            imgVerifyCode.Source = bitmap;
            MainWindow.RaiseMessage("请输入验证码后重试",
                "验证码",
                Avalonia.Controls.Notifications.NotificationType.Error);
            LogSys.Err($"[X4399Service] ShouldVerify:{scex}");
        }
        catch (Exception ex)
        {
            await HideLoginOverlay();
            MainWindow.RaiseMessage($"4399 登录失败：{ex.Message}",
                "错误",
                Avalonia.Controls.Notifications.NotificationType.Error);
            LogSys.Err($"[X4399Service] Login Failed:{ex}");
        }
        finally
        {
            btnLogin4399.IsEnabled = true; // 恢复按钮
        }
    }

    private bool _is4399PasswordVisible = false;

    private void OnToggle4399PasswordClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        _is4399PasswordVisible = !_is4399PasswordVisible;
        tbPassword4399.PasswordChar = _is4399PasswordVisible ? '\0' : '●';
        icon4399Password.Symbol = _is4399PasswordVisible
            ? FluentAvalonia.UI.Controls.Symbol.ViewAll
            : FluentAvalonia.UI.Controls.Symbol.View;
    }

}