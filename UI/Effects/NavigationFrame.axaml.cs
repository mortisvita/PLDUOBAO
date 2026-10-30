using System;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.VisualTree;

namespace PLDUOBAO.UI.Effects;

public partial class NavigationFrame : UserControl
{
    private const int SlidePx = 50;

    private bool _isFrame1Current;
    private ContentControl? _currentFrame;
    private object? _currentPage;
    private object? _previousPage;
    private ContentControl? _previousFrame;
    private CancellationTokenSource? _hideFrameCts;
    private bool _isUnloaded;

    public NavigationFrame()
    {
        InitializeComponent();
    }

    public NavigationFrameDirection NavigationFrameDirection { get; set; } = NavigationFrameDirection.Right;

    public Panel? PagePool { get; set; }

    public object? CurrentPage => _currentPage;

    public async void NavigateTo(object page)
    {
        if (_isUnloaded || ReferenceEquals(page, _currentPage))
            return;

        _hideFrameCts?.Cancel();
        _hideFrameCts = new CancellationTokenSource();
        var token = _hideFrameCts.Token;

        DestroyPreviousPage();

        ContentControl incoming;
        ContentControl outgoing;

        if (!_isFrame1Current)
        {
            _isFrame1Current = true;
            outgoing = Frame2;
            incoming = Frame1;
        }
        else
        {
            _isFrame1Current = false;
            outgoing = Frame1;
            incoming = Frame2;
        }

        outgoing.IsVisible = true;
        outgoing.Opacity = 0;
        outgoing.Margin = GetSlideOutMargin();

        if (page is Control control)
            DetachFromParent(control);

        incoming.IsVisible = true;
        incoming.Opacity = 1;
        incoming.Margin = new Thickness(0);
        incoming.Content = page;

        _previousPage = outgoing.Content;
        _previousFrame = outgoing;
        _currentPage = page;
        _currentFrame = incoming;

        try
        {
            await Task.Delay(380, token);
            if (token.IsCancellationRequested || _isUnloaded)
                return;

            outgoing.IsVisible = false;
            ClearFrameContent(outgoing);
            _previousPage = null;
            _previousFrame = null;
        }
        catch (TaskCanceledException)
        {
        }
    }

    private Thickness GetSlideOutMargin() => NavigationFrameDirection switch
    {
        NavigationFrameDirection.Top => new Thickness(0, SlidePx, 0, -SlidePx),
        NavigationFrameDirection.Left => new Thickness(SlidePx, 0, -SlidePx, 0),
        NavigationFrameDirection.Bottom => new Thickness(0, -SlidePx, 0, SlidePx),
        _ => new Thickness(SlidePx, 0, -SlidePx, 0)
    };

    private void DestroyPreviousPage()
    {
        if (_previousFrame == null)
            return;

        ClearFrameContent(_previousFrame);
        _previousPage = null;
        _previousFrame = null;
    }

    private void ClearFrameContent(ContentControl frame)
    {
        if (frame.Content is not Control control)
        {
            frame.Content = null;
            return;
        }

        frame.Content = null;
        ReturnToPool(control);
    }

    private static void DetachFromParent(Control control)
    {
        switch (control.Parent)
        {
            case Panel panel:
                panel.Children.Remove(control);
                break;
            case ContentControl contentControl when ReferenceEquals(contentControl.Content, control):
                contentControl.Content = null;
                break;
            case Decorator decorator when ReferenceEquals(decorator.Child, control):
                decorator.Child = null;
                break;
        }
    }

    private void ReturnToPool(Control control)
    {
        if (_isUnloaded || PagePool == null)
            return;

        if (ReferenceEquals(control.Parent, PagePool))
            return;

        if (PagePool.GetVisualRoot() == null || this.GetVisualRoot() == null)
            return;

        try
        {
            DetachFromParent(control);

            if (!PagePool.Children.Contains(control))
                PagePool.Children.Add(control);
        }
        catch (Avalonia.AvaloniaInternalException)
        {
        }
    }

    protected override void OnUnloaded(RoutedEventArgs e)
    {
        _isUnloaded = true;
        base.OnUnloaded(e);
        _hideFrameCts?.Cancel();
        _hideFrameCts?.Dispose();
        _hideFrameCts = null;

        Frame1.Content = null;
        Frame2.Content = null;

        _currentPage = null;
        _currentFrame = null;
        _previousPage = null;
        _previousFrame = null;
        _isFrame1Current = false;
    }
}
