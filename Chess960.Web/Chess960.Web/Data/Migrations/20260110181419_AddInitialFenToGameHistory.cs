using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Chess960.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddInitialFenToGameHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "InitialFen",
                table: "GameHistories",
                type: "TEXT",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "InitialFen",
                table: "GameHistories");
        }
    }
}
