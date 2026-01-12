using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Magic_casino_sportbook.Migrations
{
    /// <inheritdoc />
    public partial class CriarTabelaSports : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SportsEvents",
                columns: table => new
                {
                    ExternalId = table.Column<string>(type: "text", nullable: false),
                    SportKey = table.Column<string>(type: "text", nullable: false),
                    HomeTeam = table.Column<string>(type: "text", nullable: false),
                    AwayTeam = table.Column<string>(type: "text", nullable: false),
                    CommenceTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    RawOddsHome = table.Column<decimal>(type: "numeric", nullable: false),
                    RawOddsAway = table.Column<decimal>(type: "numeric", nullable: false),
                    RawOddsDraw = table.Column<decimal>(type: "numeric", nullable: false),
                    ProfitMargin = table.Column<decimal>(type: "numeric", nullable: false),
                    LastUpdate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SportsEvents", x => x.ExternalId);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SportsEvents");
        }
    }
}
