using System.Diagnostics;
using Microsoft.Win32;

var cmdArgs = Environment.GetCommandLineArgs();
if (cmdArgs.Length > 1 && cmdArgs[1] == "-uninstall")
    return Uninstall();

var appDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "BiliAnalytics");
var serviceDir = Path.Combine(appDir, "Service");
var dataDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "BiliAnalytics");
var startMenu = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu), "Programs", "BiliAnalytics");
var desktop = Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory);
var selfDir = AppContext.BaseDirectory;
var panelUrl = "http://localhost:8099/";

Console.WriteLine("========================================");
Console.WriteLine("  BiliAnalytics - B站视频数据监控");
Console.WriteLine("========================================");
Console.WriteLine();

// Admin check
using var identity = System.Security.Principal.WindowsIdentity.GetCurrent();
var principal = new System.Security.Principal.WindowsPrincipal(identity);
if (!principal.IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator))
{
    var psi = new ProcessStartInfo
    {
        FileName = Process.GetCurrentProcess().MainModule!.FileName,
        UseShellExecute = true, Verb = "runas",
        WorkingDirectory = selfDir
    };
    try { Process.Start(psi); } catch { Console.WriteLine("需要管理员权限！"); }
    return 1;
}

// Kill running service
foreach (var p in Process.GetProcessesByName("BiliAnalytics.Service"))
    try { p.Kill(); p.WaitForExit(3000); } catch { }

Directory.CreateDirectory(serviceDir);
Directory.CreateDirectory(startMenu);

CopyDir(Path.Combine(selfDir, "Service"), serviceDir);
Console.WriteLine("  ✓ 文件已复制");

// Launcher: batch file that starts service + opens browser (in installation folder)
var launcherPath = Path.Combine(appDir, "启动面板.cmd");
var launcherContent = 
    "@echo off\r\n" +
    "chcp 65001 >nul\r\n" +
    "title BiliAnalytics - B站视频数据监控\r\n" +
    "set SERVICE=" + serviceDir + "\\BiliAnalytics.Service.exe\r\n" +
    "set URL=http://localhost:8099/\r\n" +
    "\r\n" +
    "netstat -ano | findstr \"0.0.0.0:8099\" >nul\r\n" +
    "if not errorlevel 1 (\r\n" +
    "    echo 端口 8099 已被占用，正在打开面板...\r\n" +
    "    start \"\" \"%URL%\"\r\n" +
    "    exit /b\r\n" +
    ")\r\n" +
    "\r\n" +
    "tasklist /fi \"IMAGENAME eq BiliAnalytics.Service.exe\" 2>nul | find /i \"BiliAnalytics.Service.exe\" >nul\r\n" +
    "if errorlevel 1 (\r\n" +
    "    echo 正在启动服务...\r\n" +
    "    start \"\" /B \"%SERVICE%\"\r\n" +
    ")\r\n" +
    "\r\n" +
    "echo 正在打开面板...\r\n" +
    ">nul ping -n 6 127.0.0.1\r\n" +
    "start \"\" \"%URL%\"\r\n" +
    "exit\r\n";
File.WriteAllText(launcherPath, launcherContent);
Console.WriteLine("  ✓ 桌面启动器已创建");

// Start Menu: Open panel (URL)
CreateUrlShortcut(Path.Combine(startMenu, "打开面板.lnk"), panelUrl, "BiliAnalytics - B站视频数据监控");
// Start Menu: Start service
CreateShortcut(Path.Combine(startMenu, "启动服务.lnk"), Path.Combine(serviceDir, "BiliAnalytics.Service.exe"));

// Install self as uninstaller
var uninstallerPath = Path.Combine(appDir, "Uninstall.exe");
try { File.Copy(Process.GetCurrentProcess().MainModule!.FileName, uninstallerPath, true); } catch { }
CreateShortcut(Path.Combine(startMenu, "卸载 BiliAnalytics.lnk"), uninstallerPath, "-uninstall");

// Register in Add/Remove Programs
try
{
    using var key = Registry.LocalMachine.CreateSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\BiliAnalytics");
    if (key != null)
    {
        key.SetValue("DisplayName", "BiliAnalytics - B站视频数据监控");
        key.SetValue("DisplayVersion", "1.0.0");
        key.SetValue("Publisher", "BiliAnalytics");
        key.SetValue("InstallLocation", appDir);
        key.SetValue("UninstallString", $"\"{uninstallerPath}\" -uninstall");
        key.SetValue("DisplayIcon", Path.Combine(serviceDir, "BiliAnalytics.Service.exe"));
        key.SetValue("NoModify", 1, RegistryValueKind.DWord);
        key.SetValue("NoRepair", 1, RegistryValueKind.DWord);
        key.SetValue("EstimatedSize", 110000, RegistryValueKind.DWord);
        Console.WriteLine("  ✓ 已注册到 控制面板 → 卸载程序");
    }
}
catch (Exception ex)
{
    Console.WriteLine("  注册失败: " + ex.Message);
}
Console.WriteLine();
Console.WriteLine("安装完成！");
Console.WriteLine("  程序目录: " + serviceDir);
Console.WriteLine("  数据目录: " + dataDir);
Console.WriteLine();
Console.WriteLine("使用说明：");
Console.WriteLine("  打开安装目录 → 双击 启动面板.cmd → 自动启服务 + 打开浏览器");
Console.WriteLine("  或  开始菜单 → BiliAnalytics → 启动服务");
Console.WriteLine();
Thread.Sleep(3000);
return 0;

static int Uninstall()
{
    var appDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "BiliAnalytics");
    var dataDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "BiliAnalytics");
    var startMenu = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu), "Programs", "BiliAnalytics");
    var desktop = Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory);

    using var identity = System.Security.Principal.WindowsIdentity.GetCurrent();
    var principal = new System.Security.Principal.WindowsPrincipal(identity);
    if (!principal.IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator))
    {
        var psi = new ProcessStartInfo
        {
            FileName = Process.GetCurrentProcess().MainModule!.FileName,
            Arguments = "-uninstall",
            UseShellExecute = true, Verb = "runas"
        };
        try { Process.Start(psi); } catch { Console.WriteLine("需要管理员权限！"); }
        return 1;
    }

    Console.WriteLine("卸载 BiliAnalytics...");

    foreach (var p in Process.GetProcessesByName("BiliAnalytics.Service"))
        try { p.Kill(); p.WaitForExit(3000); } catch { }

    if (Directory.Exists(dataDir))
    {
        Directory.Delete(dataDir, true);
        Console.WriteLine("  ✓ 采集数据已清除");
    }

    if (Directory.Exists(appDir))
    {
        for (int i = 0; i < 5; i++)
        {
            try { Directory.Delete(appDir, true); Console.WriteLine("  ✓ 程序文件已删除"); break; }
            catch { Thread.Sleep(500); }
        }
    }

    if (Directory.Exists(startMenu)) Directory.Delete(startMenu, true);
    foreach (var lnk in new[] { "BiliAnalytics.cmd", "BiliAnalytics.url", "BiliAnalytics.lnk" })
    {
        var path = Path.Combine(desktop, lnk);
        if (File.Exists(path)) File.Delete(path);
    }

    Registry.LocalMachine.DeleteSubKeyTree(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\BiliAnalytics", false);
    Console.WriteLine("  ✓ 快捷方式和注册信息已清除");
    Console.WriteLine("卸载完成！");
    Thread.Sleep(2000);
    return 0;
}

static void CreateShortcut(string path, string target, string? args = null)
{
    var t = Type.GetTypeFromCLSID(new Guid("72C24DD5-D70A-438B-8A42-98424B88AFB8"));
    if (t == null) return;
    dynamic shortcut = Activator.CreateInstance(t)!;
    shortcut.TargetPath = target;
    shortcut.Arguments = args ?? "";
    shortcut.WorkingDirectory = Path.GetDirectoryName(target) ?? "";
    shortcut.Save(path);
}

static void CreateUrlShortcut(string path, string url, string name)
{
    File.WriteAllText(path,
        "[InternetShortcut]\r\nURL=" + url + "\r\n");
}

static void CopyDir(string src, string dst)
{
    foreach (var file in Directory.GetFiles(src))
        File.Copy(file, Path.Combine(dst, Path.GetFileName(file)), true);
    foreach (var dir in Directory.GetDirectories(src))
    {
        var sub = Path.Combine(dst, Path.GetFileName(dir));
        Directory.CreateDirectory(sub);
        CopyDir(dir, sub);
    }
}
