using BiliAnalytics.Core.Data;
using BiliAnalytics.Core.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BiliAnalytics.Core.Services;

public class CollectorService
{
    private readonly AppDbContext _db;
    private readonly BiliApiClient _api;
    private readonly ILogger<CollectorService> _log;

    public CollectorService(AppDbContext db, BiliApiClient api, ILogger<CollectorService> log)
    {
        _db = db;
        _api = api;
        _log = log;
    }

    public async Task<int> CollectAsync(CancellationToken ct = default)
    {
        _log.LogInformation("=== Collect cycle started ===");
        var videos = await _db.Videos
            .Where(v => v.IsMonitoring)
            .ToListAsync(ct);

        if (videos.Count == 0)
        {
            _log.LogInformation("No videos to monitor");
            return 0;
        }

        int newRecords = 0;
        foreach (var video in videos)
        {
            try
            {
                _log.LogInformation("Collecting {Bvid}", video.Bvid);
                var data = await _api.GetVideoDataAsync(video.Bvid, ct);

                if (data == null)
                {
                    _log.LogWarning("Skipping {Bvid} (no data)", video.Bvid);
                    continue;
                }

                // Update title if changed
                if (video.Title != data.Title)
                {
                    video.Title = data.Title;
                }

                // Check dedup: skip if same values AND last record within 10 min
                var last = await _db.History
                    .Where(h => h.Bvid == video.Bvid)
                    .OrderByDescending(h => h.RecordedAt)
                    .FirstOrDefaultAsync(ct);

                var isSame = last != null &&
                    last.ViewCount == data.ViewCount &&
                    last.LikeCount == data.LikeCount &&
                    last.CoinCount == data.CoinCount &&
                    last.FavoriteCount == data.FavoriteCount &&
                    last.ShareCount == data.ShareCount &&
                    last.DanmakuCount == data.DanmakuCount &&
                    last.ReplyCount == data.ReplyCount;

                if (isSame)
                {
                    var elapsed = DateTime.Now - DateTime.Parse(last!.RecordedAt);
                    if (elapsed.TotalMinutes < 10)
                    {
                        _log.LogInformation("No change for {Bvid}, skipping (last {Elapsed:F1}min ago)",
                            video.Bvid, elapsed.TotalMinutes);
                        continue;
                    }
                    _log.LogInformation("No change for {Bvid} but force write ({Elapsed:F1}min elapsed)",
                        video.Bvid, elapsed.TotalMinutes);
                }

                var record = new HistoryRecord
                {
                    Bvid = video.Bvid,
                    RecordedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                    ViewCount = data.ViewCount,
                    LikeCount = data.LikeCount,
                    CoinCount = data.CoinCount,
                    FavoriteCount = data.FavoriteCount,
                    ShareCount = data.ShareCount,
                    DanmakuCount = data.DanmakuCount,
                    ReplyCount = data.ReplyCount
                };
                _db.History.Add(record);
                newRecords++;
                _log.LogInformation("Recorded {Bvid}: view={View} like={Like}",
                    video.Bvid, data.ViewCount, data.LikeCount);

                await _db.SaveChangesAsync(ct);

                // Random delay between requests
                var delay = Random.Shared.Next(500, 3001);
                await Task.Delay(delay, ct);
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Error collecting {Bvid}", video.Bvid);
            }
        }

        if (newRecords > 0)
            _log.LogInformation("Saved {Count} new records", newRecords);
        else
            _log.LogInformation("No new records to save");

        _log.LogInformation("=== Collect cycle finished ===");
        return newRecords;
    }
}
