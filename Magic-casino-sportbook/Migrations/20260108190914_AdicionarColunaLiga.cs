using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Magic_casino_sportbook.Migrations
{
    /// <inheritdoc />
    public partial class AdicionarColunaLiga : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "League",
                table: "SportsEvents",
                type: "text",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "League",
                table: "SportsEvents");
        }
    }
}
