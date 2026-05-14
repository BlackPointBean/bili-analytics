using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BiliAnalytics.Core.Models;

[Table("videos")]
public class Video
{
    [Key]
    [MaxLength(20)]
    public string Bvid { get; set; } = string.Empty;

    [MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(20)]
    public string AddedDate { get; set; } = string.Empty;

    public bool IsMonitoring { get; set; } = true;

    public ICollection<HistoryRecord> History { get; set; } = new List<HistoryRecord>();
}
