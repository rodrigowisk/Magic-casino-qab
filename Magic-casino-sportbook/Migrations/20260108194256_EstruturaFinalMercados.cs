using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Magic_casino_sportbook.Migrations
{
    /// <inheritdoc />
    public partial class EstruturaFinalMercados : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ProfitMargin",
                table: "SportsEvents");

            migrationBuilder.DropColumn(
                name: "RawOddsAway",
                table: "SportsEvents");

            migrationBuilder.DropColumn(
                name: "RawOddsDraw",
                table: "SportsEvents");

            migrationBuilder.DropColumn(
                name: "RawOddsHome",
                table: "SportsEvents");

            migrationBuilder.CreateTable(
                name: "MarketOdds",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SportsEventId = table.Column<string>(type: "text", nullable: false),
                    MarketName = table.Column<string>(type: "text", nullable: false),
                    OutcomeName = table.Column<string>(type: "text", nullable: false),
                    Point = table.Column<decimal>(type: "numeric", nullable: true),
                    Price = table.Column<decimal>(type: "numeric", nullable: false),
                    ProfitMargin = table.Column<decimal>(type: "numeric", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MarketOdds", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MarketOdds_SportsEvents_SportsEventId",
                        column: x => x.SportsEventId,
                        principalTable: "SportsEvents",
                        principalColumn: "ExternalId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MarketOdds_SportsEventId",
                table: "MarketOdds",
                column: "SportsEventId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MarketOdds");

            migrationBuilder.AddColumn<decimal>(
                name: "ProfitMargin",
                table: "SportsEvents",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "RawOddsAway",
                table: "SportsEvents",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "RawOddsDraw",
                table: "SportsEvents",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "RawOddsHome",
                table: "SportsEvents",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);
        }
    }
}
