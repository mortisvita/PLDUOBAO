using System.IO;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Notifications;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using PLDUOBAO.Util;

namespace PLDUOBAO
{
    public partial class MainWindow : Window
    {
        public static MainWindow Instance => instance!;

        public MainWindow()
        {
            InitializeComponent();
            instance = this;
            PropertyChanged += (_, e) =>
            {
                if (e.Property == WindowStateProperty)
                    UpdateMaximizeIcon();
            };
        }

        private static MainWindow? instance;

        public static Grid GridMainMenu => Instance.gridMainMenu;

        public static UCCookieSelector? UCCookieSelector = null;

        public static UCMainMenu? UCMainmenu = null;

        public void RefreshList()
        {
        }

        private void Window_Loaded(object? sender, RoutedEventArgs e)
        {
            UpdateMaximizeIcon();

            gridMainMenu.Children.Remove(loadingPanel);
            UCCookieSelector cs = new UCCookieSelector();
            UCCookieSelector = cs;
            gridMainMenu.Children.Add(cs);
        }

        private void OnContentPointerPressed(object? sender, PointerPressedEventArgs e)
        {
            if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
                return;

            var pt = e.GetCurrentPoint(this).Position;
            if (pt.Y > 76 || pt.X < 220 || pt.X > Bounds.Width - 132)
                return;

            if (e.Source is Button or TextBox or ComboBox or ToggleSwitch or Slider or CheckBox)
                return;

            for (var src = e.Source as Control; src != null && src != gridMainMenu; src = src.Parent as Control)
            {
                if (src is Button or TextBox or ComboBox or ToggleSwitch or Slider or CheckBox)
                    return;
            }

            BeginMoveDrag(e);
        }

        private void OnMinimizeClick(object? sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void OnMaximizeClick(object? sender, RoutedEventArgs e)
        {
            WindowState = WindowState == WindowState.Maximized
                ? WindowState.Normal
                : WindowState.Maximized;
        }

        private void OnCloseClick(object? sender, RoutedEventArgs e)
        {
            Close();
        }

        private void UpdateMaximizeIcon()
        {
            if (maximizeIcon == null) return;
            maximizeIcon.Text = WindowState == WindowState.Maximized ? "\uE923" : "\uE922";
        }

        public static bool RaiseMessage(string msg, string title = "", NotificationType type = NotificationType.Information)
        {
            Dispatcher.UIThread.Invoke(() =>
            {
                var host = instance?.toastHost;
                if (host == null) return;

                host.Children.Add(new UCToast(title, msg, type));

                if (host.Children.Count > 5 && host.Children[0] is UCToast oldest)
                    oldest.Close();
            });
            return true;
        }
    }
}
