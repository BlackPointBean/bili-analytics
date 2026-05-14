using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace BiliAnalytics.Core.Services;

public class BiliApiClient
{
    private readonly HttpClient _http;
    private readonly Random _rng = new();

    private static readonly string[] UserAgents = [
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/126.0.0.0 Safari/537.36",
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/125.0.6422.142 Safari/537.36",
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:127.0) Gecko/20100101 Firefox/127.0",
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/126.0.0.0 Safari/537.36 Edg/126.0.0.0",
    ];

    public BiliApiClient(HttpClient http)
    {
        _http = http;
    }

    public async Task<BiliVideoData?> GetVideoDataAsync(string bvid, CancellationToken ct = default)
    {
        for (int attempt = 0; attempt < 3; attempt++)
        {
            try
            {
                await Task.Delay(_rng.Next(500, 2001), ct);

                var request = new HttpRequestMessage(HttpMethod.Get,
                    $"https://api.bilibili.com/x/web-interface/view?bvid={bvid}");
                request.Headers.Add("User-Agent", UserAgents[_rng.Next(UserAgents.Length)]);
                request.Headers.Add("Referer", $"https://www.bilibili.com/video/{bvid}");
                request.Headers.Add("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,image/apng,*/*;q=0.8");
                request.Headers.Add("Accept-Language", "zh-CN,zh;q=0.9,en;q=0.8");

                using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                cts.CancelAfter(TimeSpan.FromSeconds(8));

                var response = await _http.SendAsync(request, cts.Token);
                response.EnsureSuccessStatusCode();
                var result = await response.Content.ReadFromJsonAsync<BiliApiResponse>(
                    BiliJsonContext.Default.BiliApiResponse, cts.Token);

                if (result?.Code == 0 && result.Data?.Stat != null)
                {
                    return new BiliVideoData
                    {
                        Title = result.Data.Title ?? "",
                        ViewCount = result.Data.Stat.View,
                        LikeCount = result.Data.Stat.Like,
                        CoinCount = result.Data.Stat.Coin,
                        FavoriteCount = result.Data.Stat.Favorite,
                        ShareCount = result.Data.Stat.Share,
                        DanmakuCount = result.Data.Stat.Danmaku,
                        ReplyCount = result.Data.Stat.Reply
                    };
                }

                if (result?.Code == 412)
                {
                    var wait = Math.Pow(10, attempt + 1);
                    await Task.Delay(TimeSpan.FromSeconds(wait), ct);
                    continue;
                }
            }
            catch (TaskCanceledException) { }
            catch (HttpRequestException) { }
            catch (Exception) { }

            if (attempt < 2)
                await Task.Delay(TimeSpan.FromSeconds(5), ct);
        }

        return null;
    }
}

public class BiliVideoData
{
    public string Title { get; set; } = "";
    public int ViewCount { get; set; }
    public int LikeCount { get; set; }
    public int CoinCount { get; set; }
    public int FavoriteCount { get; set; }
    public int ShareCount { get; set; }
    public int DanmakuCount { get; set; }
    public int ReplyCount { get; set; }
}

// JSON source-gen types for trimming support
[JsonSerializable(typeof(BiliApiResponse))]
internal partial class BiliJsonContext : JsonSerializerContext { }

public class BiliApiResponse
{
    [JsonPropertyName("code")]
    public int Code { get; set; }

    [JsonPropertyName("message")]
    public string? Message { get; set; }

    [JsonPropertyName("data")]
    public BiliApiData? Data { get; set; }
}

public class BiliApiData
{
    [JsonPropertyName("title")]
    public string? Title { get; set; }

    [JsonPropertyName("stat")]
    public BiliStat? Stat { get; set; }
}

public class BiliStat
{
    [JsonPropertyName("view")]
    public int View { get; set; }

    [JsonPropertyName("like")]
    public int Like { get; set; }

    [JsonPropertyName("coin")]
    public int Coin { get; set; }

    [JsonPropertyName("favorite")]
    public int Favorite { get; set; }

    [JsonPropertyName("share")]
    public int Share { get; set; }

    [JsonPropertyName("danmaku")]
    public int Danmaku { get; set; }

    [JsonPropertyName("reply")]
    public int Reply { get; set; }
}
