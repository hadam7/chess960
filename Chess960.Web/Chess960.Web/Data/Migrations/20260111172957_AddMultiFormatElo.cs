using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Chess960.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddMultiFormatElo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "EloBlitz",
                table: "AspNetUsers",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "EloBullet",
                table: "AspNetUsers",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "EloRapid",
                table: "AspNetUsers",
                type: "INTEGER",
                nullable: false,
                defaultValue: 1200);

            // Reset all ratings to 1200 (including existing users)
            migrationBuilder.Sql("UPDATE AspNetUsers SET EloBullet = 1200, EloBlitz = 1200, EloRapid = 1200;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EloBlitz",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "EloBullet",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "EloRapid",
                table: "AspNetUsers");
        }
    }
}
