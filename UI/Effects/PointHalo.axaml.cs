using Avalonia.Controls;
using Avalonia.Input;

namespace PLDUOBAO.UI.Effects;

public partial class PointHalo : UserControl
{
    public PointHalo()
    {
        InitializeComponent();
    }

    private void Canvas_PointerMoved(object? sender, PointerEventArgs e)
    {
        var currentPoint = e.GetPosition(MainCanvas);
        Canvas.SetLeft(MovingRing, currentPoint.X - MovingRing.Width / 2);
        Canvas.SetTop(MovingRing, currentPoint.Y - MovingRing.Height / 2);
    }

    private void Control_PointerEntered(object? sender, PointerEventArgs e)
    {
        MovingRing.Opacity = 0.4;
    }

    private void Control_PointerExited(object? sender, PointerEventArgs e)
    {
        MovingRing.Opacity = 0;
    }
}
