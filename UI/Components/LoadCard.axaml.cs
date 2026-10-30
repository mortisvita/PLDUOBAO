using Avalonia;
using Avalonia.Controls;

namespace PLDUOBAO;

public partial class LoadCard : UserControl
{
    public static readonly StyledProperty<string> BigTitleProperty =
        AvaloniaProperty.Register<LoadCard, string>(nameof(BigTitle), "正在加载中...");

    public static readonly StyledProperty<string> MessageProperty =
        AvaloniaProperty.Register<LoadCard, string>(nameof(Message), string.Empty);

    public string BigTitle
    {
        get => GetValue(BigTitleProperty);
        set => SetValue(BigTitleProperty, value);
    }

    public string Message
    {
        get => GetValue(MessageProperty);
        set => SetValue(MessageProperty, value);
    }

    public LoadCard()
    {
        InitializeComponent();
        PropertyChanged += (_, e) =>
        {
            if (e.Property == BigTitleProperty)
                titleBlock.Text = BigTitle;
            else if (e.Property == MessageProperty)
                messageBlock.Text = Message;
        };
    }

    protected override void OnAttachedToVisualTree(Avalonia.VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        titleBlock.Text = BigTitle;
        messageBlock.Text = Message;
    }
}
