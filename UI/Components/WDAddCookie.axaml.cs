using System.IO;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Notifications;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using PLDUOBAO.Util;

namespace PLDUOBAO;

public partial class WDAddCookie : Window
{
    private UCCookieSelector? _cookieSelector;

    private WindowNotificationManager? notifiManager;

    public WDAddCookie()
    {
        InitializeComponent();
    }

    public WDAddCookie(UCCookieSelector cs)
    {
        InitializeComponent();
        _cookieSelector = cs;
       
    }

    private void onClosed(object? sender, System.EventArgs e)
    {
        //if (_cookieSelector != null)
        //{
        //   //await _cookieSelector.RefreshCookiesList();
        //}
    }

    public bool RaiseMessage(string msg, string title = "", NotificationType type = NotificationType.Information)
    {
        Dispatcher.UIThread.Invoke(() =>
        {
            notifiManager?.Show(new Notification() { Message = msg, Title = title, Type = type });
            return true;
        });
        return true;
    }

    private void onConfirm(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (notifiManager == null)
        {
            notifiManager = new WindowNotificationManager(GetTopLevel(this));
            notifiManager.Position = NotificationPosition.TopRight;
        }
        if (string.IsNullOrEmpty(tbFileName.Text) || string.IsNullOrEmpty(tbSauth.Text))
        {
            RaiseMessage("Sauth���ļ�������Ϊ��","����",NotificationType.Error);
            return;
        }
        if (!PathUtils.CanUseFileName(PathUtils.AccountListPath,tbFileName.Text))
        {
            RaiseMessage("���ļ���������", "����", NotificationType.Error);
            return;
        }

        PathUtils.CreateNewCookie(tbSauth.Text,tbFileName.Text);

        this.Close();
    }
}