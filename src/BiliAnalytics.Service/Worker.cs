using System.Text.Json;
using BiliAnalytics.Core.Services;
using Microsoft.EntityFrameworkCore;

namespace BiliAnalytics.Service;

public class CollectorWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<CollectorWorker> _log;

    public CollectorWorker(IServiceScopeFactory scopeFactory, ILogger<CollectorWorker> log)
    {
        _scopeFactory = scopeFactory;
        _log = log;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _log.LogInformation("Collector worker starting");
        await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);

        using (var scope = _scopeFactory.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<Core.Data.AppDbContext>();
            await db.Database.MigrateAsync(stoppingToken);
            await db.Database.ExecuteSqlRawAsync("PRAGMA journal_mode=WAL", stoppingToken);
            await ImportLegacyData(db, stoppingToken);
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var collector = scope.ServiceProvider.GetRequiredService<CollectorService>();
                await collector.CollectAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _log.LogError(ex, "Collect cycle failed");
            }

            try
            {
                var hour = DateTime.Now.Hour;
                var baseMinutes = (hour >= 0 && hour <= 6) ? 30 : 15;
                var jitter = Random.Shared.Next(-3, 4);
                await Task.Delay(TimeSpan.FromMinutes(baseMinutes + jitter), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
        _log.LogInformation("Collector worker stopped");
    }

    private async Task ImportLegacyData(Core.Data.AppDbContext db, CancellationToken ct)
    {
        var legacyDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "AI", "bili-analytics", "data");

        // Only import if DB is empty
        if (await db.Videos.AnyAsync(ct)) return;

        var videosPath = Path.Combine(legacyDir, "watched_videos.json");
        var historyPath = Path.Combine(legacyDir, "history.json");

        if (!File.Exists(videosPath) && !File.Exists(historyPath)) return;

        try
        {
            // Import videos
            if (File.Exists(videosPath))
            {
                var json = File.ReadAllText(videosPath).TrimStart('\uFEFF');
                var vids = JsonSerializer.Deserialize<List<LegacyVideo>>(json,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (vids != null)
                {
                    foreach (var v in vids)
                    {
                        db.Videos.Add(new Core.Models.Video
                        {
                            Bvid = v.Bvid,
                            Title = v.Title ?? "",
                            AddedDate = v.Added ?? DateTime.Now.ToString("yyyy-MM-dd")
                        });
                    }
                    _log.LogInformation("Imported {Count} videos from legacy JSON", vids.Count);
                }
            }

            // Import history
            if (File.Exists(historyPath))
            {
                var json = File.ReadAllText(historyPath).TrimStart('\uFEFF');
                var records = JsonSerializer.Deserialize<List<LegacyRecord>>(json,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (records != null)
                {
                    foreach (var r in records)
                    {
                        db.History.Add(new Core.Models.HistoryRecord
                        {
                            Bvid = r.Bvid,
                            RecordedAt = r.Date ?? DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                            ViewCount = r.View,
                            LikeCount = r.Like,
                            CoinCount = r.Coin,
                            FavoriteCount = r.Favorite,
                            ShareCount = r.Share,
                            DanmakuCount = r.Danmaku,
                            ReplyCount = r.Reply
                        });
                    }
                    _log.LogInformation("Imported {Count} history records from legacy JSON", records.Count);
                }
            }

            await db.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Legacy data import skipped (may already be imported)");
        }
    }

    private class LegacyVideo
    {
        public string Bvid { get; set; } = "";
        public string? Title { get; set; }
        public string? Added { get; set; }
    }

    private class LegacyRecord
    {
        public string Bvid { get; set; } = "";
        public string? Date { get; set; }
        public int View { get; set; }
        public int Like { get; set; }
        public int Coin { get; set; }
        public int Favorite { get; set; }
        public int Share { get; set; }
        public int Danmaku { get; set; }
        public int Reply { get; set; }
    }
}
