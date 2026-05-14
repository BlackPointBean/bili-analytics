using BiliAnalytics.Core.Data;
using BiliAnalytics.Core.Services;
using Microsoft.EntityFrameworkCore;

namespace BiliAnalytics.Service;

public class Program
{
    public static void Main(string[] args)
    {
        var workingDir = AppContext.BaseDirectory;
        var publishedWwwroot = Path.Combine(workingDir, "wwwroot");
        var devWwwroot = Path.GetFullPath(Path.Combine(workingDir, "..", "..", "..", "wwwroot"));
        var wwwrootPath = Directory.Exists(publishedWwwroot) ? publishedWwwroot
            : Directory.Exists(devWwwroot) ? devWwwroot
            : publishedWwwroot;

        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            ContentRootPath = workingDir,
            WebRootPath = wwwrootPath,
            Args = args
        });

        var dbPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "BiliAnalytics", "bili.db");
        Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);

        builder.Services.AddCors();
        builder.Services.AddDbContext<AppDbContext>(opts =>
            opts.UseSqlite($"Data Source={dbPath}"));

        builder.Services.AddHttpClient<BiliApiClient>();
        builder.Services.AddScoped<CollectorService>();
        builder.Services.AddHostedService<CollectorWorker>();

        builder.Services.AddWindowsService(opts =>
            opts.ServiceName = "BiliAnalytics");

        builder.WebHost.ConfigureKestrel(opts =>
            opts.ListenLocalhost(8099));

        var app = builder.Build();

        app.UseDefaultFiles();
        app.UseStaticFiles();
        app.UseCors(p => p.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader());

        app.MapGet("/api/videos", async (AppDbContext db) =>
        {
            var videos = await db.Videos
                .Select(v => new { v.Bvid, v.Title, v.AddedDate, active = v.IsMonitoring })
                .ToListAsync();
            return Results.Ok(videos);
        });

        app.MapPost("/api/videos", async (AppDbContext db, BiliApiClient api, AddVideoRequest req) =>
        {
            if (string.IsNullOrWhiteSpace(req.Bvid))
                return Results.BadRequest(new { error = "bvid required" });

            var existing = await db.Videos.FirstOrDefaultAsync(v => v.Bvid == req.Bvid);
            if (existing != null)
            {
                if (existing.IsMonitoring)
                    return Results.Ok(new { message = "already exists" });

                existing.IsMonitoring = true;
                var data = await api.GetVideoDataAsync(req.Bvid);
                if (data != null) existing.Title = data.Title;
                await db.SaveChangesAsync();
                return Results.Ok(new { message = "reactivated", title = existing.Title ?? "", warning = data == null ? "B站API暂时无法获取视频信息" : "" });
            }

            // Best-effort fetch: add video even if B站API fails (title will come later)
            var newData = await api.GetVideoDataAsync(req.Bvid);
            db.Videos.Add(new Core.Models.Video
            {
                Bvid = req.Bvid,
                Title = newData?.Title ?? "",
                AddedDate = DateTime.Now.ToString("yyyy-MM-dd")
            });

            // Create initial history record so user sees data immediately
            if (newData != null)
            {
                db.History.Add(new Core.Models.HistoryRecord
                {
                    Bvid = req.Bvid,
                    RecordedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                    ViewCount = newData.ViewCount,
                    LikeCount = newData.LikeCount,
                    CoinCount = newData.CoinCount,
                    FavoriteCount = newData.FavoriteCount,
                    ShareCount = newData.ShareCount,
                    DanmakuCount = newData.DanmakuCount,
                    ReplyCount = newData.ReplyCount
                });
            }

            await db.SaveChangesAsync();
            await db.SaveChangesAsync();
            return Results.Ok(new { message = "added", title = newData?.Title ?? "", warning = newData == null ? "B站API暂时无法获取视频信息，标题待采集" : "" });
        });

        app.MapDelete("/api/videos/{bvid}", async (AppDbContext db, string bvid, string? hard) =>
        {
            var video = await db.Videos.FirstOrDefaultAsync(v => v.Bvid == bvid);
            if (video == null)
                return Results.NotFound(new { error = "not found" });

            if (hard == "true")
            {
                db.Videos.Remove(video);
            }
            else
            {
                video.IsMonitoring = false;
            }
            await db.SaveChangesAsync();
            return Results.Ok(new { message = "removed" });
        });

        app.MapGet("/api/history", async (AppDbContext db, string? range) =>
        {
            var query = db.History.AsQueryable();
            if (range == "1h") query = query.Where(h => h.RecordedAt.CompareTo(
                DateTime.Now.AddHours(-1).ToString("yyyy-MM-dd HH:mm:ss")) >= 0);
            else if (range == "6h") query = query.Where(h => h.RecordedAt.CompareTo(
                DateTime.Now.AddHours(-6).ToString("yyyy-MM-dd HH:mm:ss")) >= 0);
            else if (range == "24h") query = query.Where(h => h.RecordedAt.CompareTo(
                DateTime.Now.AddHours(-24).ToString("yyyy-MM-dd HH:mm:ss")) >= 0);
            else if (range == "7d") query = query.Where(h => h.RecordedAt.CompareTo(
                DateTime.Now.AddDays(-7).ToString("yyyy-MM-dd HH:mm:ss")) >= 0);

            var data = await query.OrderBy(h => h.RecordedAt).ToListAsync();
            return Results.Ok(data.Select(h => new
            {
                bvid = h.Bvid,
                date = h.RecordedAt,
                view = h.ViewCount,
                like = h.LikeCount,
                coin = h.CoinCount,
                favorite = h.FavoriteCount,
                share = h.ShareCount,
                danmaku = h.DanmakuCount,
                reply = h.ReplyCount
            }));
        });

        app.MapGet("/api/history/latest", async (AppDbContext db) =>
        {
            var latest = await db.Videos.Where(v => v.IsMonitoring).Select(v => new
            {
                v.Bvid,
                v.Title,
                v.AddedDate,
                LastRecord = db.History
                    .Where(h => h.Bvid == v.Bvid)
                    .OrderByDescending(h => h.RecordedAt)
                    .FirstOrDefault()
            }).ToListAsync();

            return Results.Ok(latest.Select(v => new
            {
                bvid = v.Bvid,
                title = v.Title,
                added = v.AddedDate,
                view = v.LastRecord?.ViewCount,
                like = v.LastRecord?.LikeCount,
                coin = v.LastRecord?.CoinCount,
                favorite = v.LastRecord?.FavoriteCount,
                share = v.LastRecord?.ShareCount,
                danmaku = v.LastRecord?.DanmakuCount,
                reply = v.LastRecord?.ReplyCount,
                date = v.LastRecord?.RecordedAt
            }));
        });

        // Serve dashboard (frontend) - integrates with the GUI
        app.MapFallbackToFile("dashboard.html");

        app.Run();
    }
}

public record AddVideoRequest(string Bvid);
