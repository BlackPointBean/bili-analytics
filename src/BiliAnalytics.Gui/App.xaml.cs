using System.Diagnostics;
using System.Windows;

namespace BiliAnalytics.Gui;

public partial class App : System.Windows.Application
{
    private System.Windows.Forms.NotifyIcon? _trayIcon;
    private MainWindow? _mainWindow;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _trayIcon = new System.Windows.Forms.NotifyIcon
        {
            Icon = System.Drawing.Icon.ExtractAssociatedIcon(
                Process.GetCurrentProcess().MainModule?.FileName ?? "shell32.dll"),
            Visible = true,
            Text = "BiliAnalytics - B站视频数据监控",
            ContextMenuStrip = new System.Windows.Forms.ContextMenuStrip()
        };
        _trayIcon.ContextMenuStrip.Items.Add("打开面板", null, (s, ev) => ShowWindow());
        _trayIcon.ContextMenuStrip.Items.Add("-");
        _trayIcon.ContextMenuStrip.Items.Add("退出", null, (s, ev) => ShutdownApp());
        _trayIcon.DoubleClick += (s, ev) => ShowWindow();

        ShowWindow();
    }

    private void ShowWindow()
    {
        if (_mainWindow == null)
        {
            _mainWindow = new MainWindow();
            _mainWindow.Closed += (s, e) => _mainWindow = null;
            _mainWindow.Show();
        }
        else
        {
            _mainWindow.Show();
            _mainWindow.Activate();
            if (_mainWindow.WindowState == WindowState.Minimized)
                _mainWindow.WindowState = WindowState.Normal;
        }
    }

    private void ShutdownApp()
    {
        _trayIcon?.Dispose();
        Shutdown();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _trayIcon?.Dispose();
        base.OnExit(e);
    }
}
