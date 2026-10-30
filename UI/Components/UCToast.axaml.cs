using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Controls.Notifications;
using Avalonia.Input;
using Avalonia.Styling;
using Avalonia.Threading;

namespace PLDUOBAO;

public partial class UCToast : UserControl
{
    private bool _closed;
    private readonly DispatcherTimer _timer;

    public Action<UCToast>? OnClose { get; set; }

    public UCToast()
    {
        InitializeComponent();
        _timer = new DispatcherTimer();
    }

    public UCToast(string title, string message, NotificationType type) : this()
    {
        NoticeTitle.Text = string.IsNullOrWhiteSpace(title) ? GetDefaultTitle(type) : title;
        NoticeContent.Text = message ?? string.Empty;
        NoticeContent.IsVisible = !string.IsNullOrWhiteSpace(message);
        ApplyIconStyle(type);

        PointerPressed += (_, _) => Close();

        _timer.Interval = TimeSpan.FromMilliseconds(type == NotificationType.Error ? 6000 : 4500);
        _timer.Tick += (_, _) =>
        {
            _timer.Stop();
            Close();
        };
        _timer.Start();
    }

    private void ApplyIconStyle(NotificationType type)
    {
        NoticeIconBadge.IsVisible = false;
        NoticeIconRing.IsVisible = true;

        switch (type)
        {
            case NotificationType.Success:
                NoticeIconGlyph.IsVisible = true;
                NoticeIconText.IsVisible = false;
                NoticeIconGlyph.Text = "✓";
                break;
            case NotificationType.Warning:
                NoticeIconGlyph.IsVisible = true;
                NoticeIconText.IsVisible = false;
                NoticeIconGlyph.Text = "!";
                break;
            case NotificationType.Error:
                NoticeIconGlyph.IsVisible = true;
                NoticeIconText.IsVisible = false;
                NoticeIconGlyph.Text = "×";
                break;
            default:
                NoticeIconGlyph.IsVisible = false;
                NoticeIconText.IsVisible = true;
                break;
        }
    }

    private static string GetDefaultTitle(NotificationType type) => type switch
    {
        NotificationType.Success => "成功",
        NotificationType.Warning => "警告",
        NotificationType.Error => "错误",
        _ => "提示"
    };

    public async void Close()
    {
        if (_closed) return;
        _closed = true;
        _timer.Stop();

        var anim = new Animation
        {
            Duration = TimeSpan.FromMilliseconds(800),
            Easing = new ExponentialEaseOut(),
            Children =
            {
                new KeyFrame
                {
                    Cue = new Cue(0),
                    Setters = { new Setter(MarginProperty, new Thickness(15, 5)) }
                },
                new KeyFrame
                {
                    Cue = new Cue(1),
                    Setters = { new Setter(MarginProperty, new Thickness(230, 5, -230, 5)) }
                }
            }
        };

        _ = anim.RunAsync(PATH_Border);
        await Task.Delay(600);

        OnClose?.Invoke(this);
        (Parent as Panel)?.Children.Remove(this);
    }
}
