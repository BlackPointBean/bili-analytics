using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BiliAnalytics.Core.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "videos",
                columns: table => new
                {
                    Bvid = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    Title = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    AddedDate = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    IsMonitoring = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_videos", x => x.Bvid);
                });

            migrationBuilder.CreateTable(
                name: "history",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Bvid = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    RecordedAt = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    ViewCount = table.Column<int>(type: "INTEGER", nullable: false),
                    LikeCount = table.Column<int>(type: "INTEGER", nullable: false),
                    CoinCount = table.Column<int>(type: "INTEGER", nullable: false),
                    FavoriteCount = table.Column<int>(type: "INTEGER", nullable: false),
                    ShareCount = table.Column<int>(type: "INTEGER", nullable: false),
                    DanmakuCount = table.Column<int>(type: "INTEGER", nullable: false),
                    ReplyCount = table.Column<int>(type: "INTEGER", nullable: false),
                    VideoBvid = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_history", x => x.Id);
                    table.ForeignKey(
                        name: "FK_history_videos_VideoBvid",
                        column: x => x.VideoBvid,
                        principalTable: "videos",
                        principalColumn: "Bvid");
                });

            migrationBuilder.CreateIndex(
                name: "IX_history_Bvid_RecordedAt",
                table: "history",
                columns: new[] { "Bvid", "RecordedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_history_RecordedAt",
                table: "history",
                column: "RecordedAt");

            migrationBuilder.CreateIndex(
                name: "IX_history_VideoBvid",
                table: "history",
                column: "VideoBvid");

            migrationBuilder.CreateIndex(
                name: "IX_videos_IsMonitoring",
                table: "videos",
                column: "IsMonitoring");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "history");

            migrationBuilder.DropTable(
                name: "videos");
        }
    }
}
