using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Chess960.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddGameHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "GameHistories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    WhiteUserId = table.Column<string>(type: "TEXT", nullable: false),
                    BlackUserId = table.Column<string>(type: "TEXT", nullable: false),
                    WhiteUserName = table.Column<string>(type: "TEXT", nullable: false),
                    BlackUserName = table.Column<string>(type: "TEXT", nullable: false),
                    Result = table.Column<string>(type: "TEXT", nullable: false),
                    EndReason = table.Column<string>(type: "TEXT", nullable: false),
                    MovesPgn = table.Column<string>(type: "TEXT", nullable: false),
                    Fen = table.Column<string>(type: "TEXT", nullable: false),
                    DatePlayed = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GameHistories", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GameHistories");
        }
    }
}
