using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BiliAnalytics.Core.Models;

[Table("history")]
public class HistoryRecord
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public long Id { get; set; }

    [MaxLength(20)]
    public string Bvid { get; set; } = string.Empty;

    [MaxLength(20)]
    public string RecordedAt { get; set; } = string.Empty;

    public int ViewCount { get; set; }
    public int LikeCount { get; set; }
    public int CoinCount { get; set; }
    public int FavoriteCount { get; set; }
    public int ShareCount { get; set; }
    public int DanmakuCount { get; set; }
    public int ReplyCount { get; set; }
}
