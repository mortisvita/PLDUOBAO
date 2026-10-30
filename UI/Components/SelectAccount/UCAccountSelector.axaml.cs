using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using FluentAvalonia.UI.Controls;
using PLDUOBAO.SDK;

namespace PLDUOBAO;

internal partial class UCAccountSelector : UserControl
{
    public UserCookie? cookie { get; set; }
    public UCCookieSelector? selector { get; set; }
    public UCAccountSelector()
    {
        InitializeComponent();
    }

    public UCAccountSelector(UserCookie cookie,UCCookieSelector selector)
    {
        InitializeComponent();
        this.cookie = cookie;
        this.selector = selector;
        tbUsername.Text = $"{cookie.Platform} - {cookie.Name}";
        
        // 显示备注
        if (!string.IsNullOrEmpty(cookie.Nickname))
        {
            bdNickname.IsVisible = true;
            tbNickname.Text = cookie.Nickname;
        }
        
        // 4399账号显示编辑按钮
        if (cookie.Platform == UserCookie.CookiePlatform.com4399)
        {
            btnEditCredentials.IsVisible = true;
        }
    }

    private async void onEditNickname(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (cookie == null) return;
        
        var dialog = new ContentDialog
        {
            Title = "编辑账号备注",
            PrimaryButtonText = "保存",
            CloseButtonText = "取消"
        };
        
        var textBox = new TextBox
        {
            Text = cookie.Nickname ?? "",
            Watermark = "输入备注，例如：小号",
            MaxLength = 20
        };
        
        dialog.Content = new StackPanel
        {
            Spacing = 12,
            Children =
            {
                new TextBlock { Text = $"账号: {cookie.Name}", Foreground = Avalonia.Media.Brushes.Gray },
                textBox
            }
        };
        
        var result = await dialog.ShowAsync();
        
        if (result == ContentDialogResult.Primary)
        {
            cookie.Nickname = string.IsNullOrWhiteSpace(textBox.Text) ? null : textBox.Text.Trim();
            
            // 更新 UI
            if (!string.IsNullOrEmpty(cookie.Nickname))
            {
                bdNickname.IsVisible = true;
                tbNickname.Text = cookie.Nickname;
            }
            else
            {
                bdNickname.IsVisible = false;
            }
            
            // 保存到文件
            LocalAccountManager.SaveUsers();
            MainWindow.RaiseMessage("备注已保存", "提示", Avalonia.Controls.Notifications.NotificationType.Success);
        }
    }
    
    private async void onEditCredentials(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (cookie == null || cookie.Platform != UserCookie.CookiePlatform.com4399) return;
        
        var dialog = new ContentDialog
        {
            Title = "编辑 4399 账号密码",
            PrimaryButtonText = "保存",
            CloseButtonText = "取消"
        };
        
        var tbUsername = new TextBox
        {
            Text = cookie.Username4399 ?? "",
            Watermark = "4399 账号",
            Margin = new Avalonia.Thickness(0, 0, 0, 12)
        };
        
        var tbPassword = new TextBox
        {
            Text = cookie.Password4399 ?? "",
            Watermark = "密码",
            PasswordChar = '●'
        };
        
        var btnTogglePassword = new Button
        {
            Content = new FluentAvalonia.UI.Controls.SymbolIcon { Symbol = FluentAvalonia.UI.Controls.Symbol.View },
            Width = 32,
            Height = 32,
            Padding = new Avalonia.Thickness(0),
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
        };
        
        bool isPasswordVisible = false;
        btnTogglePassword.Click += (s, args) =>
        {
            isPasswordVisible = !isPasswordVisible;
            tbPassword.PasswordChar = isPasswordVisible ? '\0' : '●';
            ((FluentAvalonia.UI.Controls.SymbolIcon)btnTogglePassword.Content!).Symbol = 
                isPasswordVisible ? FluentAvalonia.UI.Controls.Symbol.ViewAll : FluentAvalonia.UI.Controls.Symbol.View;
        };
        
        var passwordRow = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*, Auto"),
            Children = { tbPassword }
        };
        Grid.SetColumn(btnTogglePassword, 1);
        btnTogglePassword.Margin = new Avalonia.Thickness(8, 0, 0, 0);
        passwordRow.Children.Add(btnTogglePassword);
        
        dialog.Content = new StackPanel
        {
            Spacing = 8,
            Width = 300,
            Children =
            {
                new TextBlock { Text = $"当前账号: {cookie.Name}", Foreground = Avalonia.Media.Brushes.Gray },
                tbUsername,
                new TextBlock { Text = "密码", FontSize = 12, Foreground = Avalonia.Media.Brushes.Gray },
                passwordRow
            }
        };
        
        var result = await dialog.ShowAsync();
        
        if (result == ContentDialogResult.Primary)
        {
            if (string.IsNullOrWhiteSpace(tbUsername.Text) || string.IsNullOrWhiteSpace(tbPassword.Text))
            {
                MainWindow.RaiseMessage("账号或密码不能为空", "错误", Avalonia.Controls.Notifications.NotificationType.Error);
                return;
            }
            
            cookie.Username4399 = tbUsername.Text.Trim();
            cookie.Password4399 = tbPassword.Text;
            
            // 保存到文件
            LocalAccountManager.SaveUsers();
            MainWindow.RaiseMessage("账号密码已更新", "提示", Avalonia.Controls.Notifications.NotificationType.Success);
        }
    }

    private void onDeleteAccount(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (cookie == null)
            return;

        LocalAccountManager.DeleteUser(cookie);
        
        // 直接刷新列表，不重建控件
        if (selector != null)
        {
            selector.ReloadUsers();
        }
        
        MainWindow.RaiseMessage("账户已删除", "提示", Avalonia.Controls.Notifications.NotificationType.Success);
    }

    private void onUseAccount(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (cookie == null)
            return;

        if (selector == null)
            return;

        // 登录方式已统一为 4399 账号密码，这里只把账号密码回填到登录页
        selector.tbUsername4399.Text = cookie.Username4399;
        selector.tbPassword4399.Text = cookie.Password4399;

        selector.pageAccounts.IsVisible = false;
        selector.pageLogin.IsVisible = true;
        selector.tabAccounts.Classes.Set("inactive", true);
        selector.tabLogin.Classes.Set("inactive", false);

        MainWindow.RaiseMessage("已经为你自动填写账号密码，请开始登录", "提示");
    }
}