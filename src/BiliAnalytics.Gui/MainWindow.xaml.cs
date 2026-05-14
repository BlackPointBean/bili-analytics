using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Windows;
using Microsoft.Web.WebView2.Core;

namespace BiliAnalytics.Gui;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        Loaded += async (s, e) => await InitializeAsync();
    }

    private async Task InitializeAsync()
    {
        EnsureServiceRunning();

        // Wait for service to be ready (poll localhost:8099)
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };
        for (int i = 0; i < 20; i++)
        {
            try
            {
                var resp = await http.GetAsync("http://localhost:8099/api/videos");
                if (resp.IsSuccessStatusCode) break;
            }
            catch { }
            await Task.Delay(800);
        }

        Title = "BiliAnalytics - B站视频数据监控";

        await webView.EnsureCoreWebView2Async(null);
        webView.CoreWebView2.Navigate("http://localhost:8099/");
    }

    private static void EnsureServiceRunning()
    {
        if (Process.GetProcessesByName("BiliAnalytics.Service").Length > 0)
            return;

        var selfDir = Path.GetDirectoryName(Process.GetCurrentProcess().MainModule!.FileName) ?? "";
        var servicePath = Path.GetFullPath(Path.Combine(selfDir, "..", "Service", "BiliAnalytics.Service.exe"));

        if (!File.Exists(servicePath))
            return;

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = servicePath,
                UseShellExecute = true,
                WindowStyle = ProcessWindowStyle.Hidden,
                CreateNoWindow = true
            };
            Process.Start(psi);
        }
        catch { }
    }

    private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
    {
        e.Cancel = true;
        Hide();
    }
}
