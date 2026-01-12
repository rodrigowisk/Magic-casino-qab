using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Magic_casino.Migrations
{
    /// <inheritdoc />
    public partial class InitialDb : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "users",
                columns: table => new
                {
                    cpf = table.Column<string>(type: "character varying(11)", maxLength: 11, nullable: false),
                    password = table.Column<string>(type: "text", nullable: false),
                    email = table.Column<string>(type: "text", nullable: true),
                    phone = table.Column<string>(type: "text", nullable: true),
                    is_admin = table.Column<bool>(type: "boolean", nullable: false),
                    agent_code = table.Column<string>(type: "text", nullable: true),
                    agent_linked_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_users", x => x.cpf);
                });

            migrationBuilder.CreateTable(
                name: "wallets",
                columns: table => new
                {
                    user_cpf = table.Column<string>(type: "character varying(11)", nullable: false),
                    balance_fiver = table.Column<decimal>(type: "numeric", nullable: false),
                    balance_qab = table.Column<decimal>(type: "numeric", nullable: false),
                    balance_bonus = table.Column<decimal>(type: "numeric", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_wallets", x => x.user_cpf);
                    table.ForeignKey(
                        name: "FK_wallets_users_user_cpf",
                        column: x => x.user_cpf,
                        principalTable: "users",
                        principalColumn: "cpf",
                        onDelete: ReferentialAction.Cascade);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "wallets");

            migrationBuilder.DropTable(
                name: "users");
        }
    }
}
