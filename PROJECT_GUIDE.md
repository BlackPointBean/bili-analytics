# B站视频数据监控系统 — 项目总结

## 任务目标

构建 Windows 本地运行的 B站视频数据监控系统：

- 用户添加任意 BV 号进行监控
- 每分钟自动采集播放 / 点赞 / 硬币 / 收藏 / 转发 / 弹幕 / 评论数据
- 数据去重存储（数值不变则跳过写入）
- 浏览器 / 桌面面板可视化（ECharts 折线图 + 雷达图 + 柱状图 + 数据表格）
- 后台静默运行，资源占用极低
- 终端无窗口弹窗（Task Scheduler 导致 CUI 窗口闪现问题）
- 可扩展为大规模发行的桌面软件

---

## 最终架构

```
D:\AI\bili-analytics\
├── src\                                 ← .NET 8 源码
│   ├── BiliAnalytics.sln
│   ├── BiliAnalytics.Core\              ← 公共类库
│   │   ├── Models\Video.cs              ← 监控视频实体
│   │   ├── Models\HistoryRecord.cs      ← 采集记录实体
│   │   ├── Data\AppDbContext.cs         ← EF Core + SQLite 上下文
│   │   ├── Services\BiliApiClient.cs    ← B站 API 客户端（重试/退避/源生成）
│   │   └── Services\CollectorService.cs ← 采集 + 去重引擎
│   ├── BiliAnalytics.Service\           ← 后台服务（.NET 8 Web SDK）
│   │   ├── Program.cs                   ← Kestrel HTTP API + Windows Service 宿主
│   │   ├── Worker.cs                    ← 每分钟采集循环 + 旧数据导入
│   │   └── wwwroot\dashboard.html       ← ECharts 仪表盘（内嵌，离线可用）
│   └── BiliAnalytics.Gui\               ← WPF 桌面应用
│       ├── App.xaml / App.xaml.cs       ← 系统托盘 + 窗口生命周期
│       ├── MainWindow.xaml / .cs        ← WebView2 嵌入仪表盘
│       └── (含 WebView2 + WinForms 托盘)
├── publish\                             ← 发布输出（self-contained 单文件）
│   ├── Service\BiliAnalytics.Service.exe  ← ~101MB
│   └── Gui\BiliAnalytics.Gui.exe          ← ~160MB
├── collect.ps1 / show.ps1 / manage.ps1  ← 第一版 PowerShell 脚本（已停用）
├── report.html                          ← 第一版 HTML 仪表盘（已停用）
├── launcher.vbs                         ← VBS 静默启动器（已停用）
├── data\                                ← 旧 JSON 数据
└── logs\collect.log                     ← 旧采集日志
```

---

## 技术栈

| 层 | 技术 | 详情 |
|----|------|------|
| 后台服务 | C# (.NET 8) | Worker Service → 定时采集，Kestrel 内置 HTTP API |
| 数据存储 | EF Core + SQLite | WAL 模式，自动迁移，断电安全 |
| API 端点 | Minimal API | `/api/videos` 增删查，`/api/history` 时间序列查询 |
| 仪表盘 | ECharts 5.5 + HTML/JS | 折线图 + 雷达图 + 柱状图 + 数据表格，60s 自动刷新 |
| 桌面 GUI | WPF + WebView2 | 系统托盘，窗口嵌入 WebView2 指向 localhost |
| API 客户端 | HttpClient + 源生成 JSON | 重试 3 次，412 指数退避，请求间随机延迟 |
| 发布 | .NET 8 self-contained | 单文件 exe，无需安装 .NET 运行时 |
| 旧版（已弃用） | PowerShell 5.1 + Task Scheduler | 采集 + HttpListener 服务 + ECharts 浏览器页面 |

---

## API 端点

| 方法 | 路径 | 功能 |
|------|------|------|
| GET | `/` | 仪表盘 HTML 页面 |
| GET | `/api/videos` | 列出监控视频 |
| POST | `/api/videos` | 添加视频 `{"bvid":"BVxxx"}` |
| DELETE | `/api/videos/{bvid}` | 移除视频（软删除） |
| GET | `/api/history` | 全部历史数据（支持 `?range=24h` 过滤） |
| GET | `/api/history/latest` | 每个视频最新数据 |

---

## 使用方式

```powershell
# 启动后台服务（无窗口，静默运行）
D:\AI\bili-analytics\publish\Service\BiliAnalytics.Service.exe

# 浏览器直接访问
http://localhost:8099/

# 或启动 WPF 桌面面板（系统托盘 + WebView2 窗口）
D:\AI\bili-analytics\publish\Gui\BiliAnalytics.Gui.exe
```

---

## 已解决的问题

| 问题 | 原因 | 方案 |
|------|------|------|
| Task Scheduler 每分钟弹出 CUI 窗口 | `powershell.exe` PE 头标记为 CUI 子系统，进程创建时强制分配控制台 | 替换为 .NET 后台进程，完全不创建窗口 |
| `history.json` 单对象而非数组 | PS 5.1 `ConvertTo-Json` 单元素管道展开 | 改用 SQLite，EF Core 统一管理 |
| PowerShell 解析错误 `$bvid:` | PS 5.1 把 `$var:` 解析为驱动器限定路径 | 移到 .NET，消除 PS 字符串解析歧义 |
| 前端表格空白无法排查 | 缺少诊断信息 | dashboard.html 内置可折叠诊断日志面板 |
| 定时任务无窗口但仍有闪现 | Task Scheduler + PowerShell 进程启动时序 | 彻底弃用 Task Scheduler，后台进程常驻 |
| HttpListener 端口残留 | http.sys 内核驱动持留 URL 预留 | 改用 Kestrel，无 URL 预留持久化问题 |
| `ConnectionString` Journal Mode 参数不支持 | Microsoft.Data.Sqlite 不支持此关键字 | 启动后执行 `PRAGMA journal_mode=WAL` |
| CORS 中间件未注册 | `UseCors()` 需先 `AddCors()` | 添加 `builder.Services.AddCors()` |
| PS 5.1 BOM 污染 JSON | `Set-Content -Encoding UTF8` 写入 UTF-8-BOM | SQLite 无 BOM 问题 |
| WPF GUI `Application` 歧义 | `UseWindowsForms` 与 `UseWPF` 共存导致类型冲突 | 使用完全限定类型名 `System.Windows.Application` |

---

## Phase 2 实施要点（.NET 8 改造）

### 实施决策
- **.NET 版本**：.NET 8 LTS（self-contained 单文件发布，用户无需装运行时）
- **GUI 技术**：WPF + WebView2（原生 Windows 体验，嵌入式 Web 仪表盘）
- **数据库**：EF Core + SQLite（WAL 模式，并发读 + 单写者）
- **API 序列化**：System.Text.Json 源生成（支持 AOT 裁剪）

### 关键陷阱防范
| 陷阱 | 防范 |
|------|------|
| Windows Service `OnStart` 30s 超时 | `BackgroundService.ExecuteAsync` 异步执行，启动立即返回 |
| SQLite 多线程写冲突 | WAL 模式 + 仅 Service 写入，GUI 只读 |
| WebView2 运行时缺失 | 发布包含 Fixed Version WebView2 Loader DLL |
| B站 API 频繁请求被封 | 请求间随机 0.5-3s 延迟 + 412 指数退避 + UA 伪装 |
| 旧 JSON 数据丢失 | Service 首次启动自动导入 legacy JSON 到 SQLite |
| EF Core 迁移首次运行慢 | 安装时预创建 SQLite 数据库，运行时只 `MigrateAsync` |
| 端口冲突 | 可配置端口，默认 8099，仅监听 `localhost`（不暴露局域网） |

---

## 验证状态

- [x] 采集引擎：`CollectorService` 调用 B站 API 获取 7 项数据，写入 SQLite
- [x] 去重逻辑：7 字段全部相同则跳过，仅写入变更记录
- [x] HTTP API：`/api/videos` + `/api/history` 正常返回 JSON
- [x] 仪表盘：`/` 返回 ECharts 页面，折线图 / 雷达图 / 柱状图 / 表格均渲染
- [x] 视频管理：`POST /api/videos` 添加 BV 号自动获取标题
- [x] 无窗口运行：后台进程不创建任何控制台或 UI 窗口
- [x] 旧数据迁移：legacy JSON → SQLite 自动导入
- [x] 发布产物：`D:\AI\bili-analytics\publish\` 含 Service.exe + Gui.exe
- [x] 自动刷新：仪表盘每 60 秒拉取最新数据

---

## 环境信息

| 项目 | 值 |
|------|-----|
| OS | Windows 10/11 x64 |
| .NET SDK | 8.0.421 |
| 数据路径 | `%ProgramData%\BiliAnalytics\bili.db` |
| 默认端口 | 8099 |
| 监控视频 | BV1aQddBnE1h（灯祥/MAD） |
