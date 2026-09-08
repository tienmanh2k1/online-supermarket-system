using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OnlineSupermarket.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAddressesAndPasswordResetTokens : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "addresses",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "char(36)", nullable: false),
                    user_id = table.Column<Guid>(type: "char(36)", nullable: false),
                    recipient_name = table.Column<string>(type: "varchar(150)", maxLength: 150, nullable: false),
                    phone = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false),
                    street = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: false),
                    ward = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false),
                    district = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false),
                    city = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false),
                    postal_code = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: true),
                    is_default = table.Column<bool>(type: "tinyint(1)", nullable: false, defaultValue: false),
                    created_at_utc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_addresses", x => x.id);
                })
;

            migrationBuilder.CreateTable(
                name: "password_reset_tokens",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "char(36)", nullable: false),
                    user_id = table.Column<Guid>(type: "char(36)", nullable: false),
                    token_hash = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: false),
                    expires_at_utc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    is_used = table.Column<bool>(type: "tinyint(1)", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_password_reset_tokens", x => x.id);
                })
;

            migrationBuilder.CreateIndex(
                name: "IX_addresses_user_id",
                table: "addresses",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "IX_addresses_user_id_is_default",
                table: "addresses",
                columns: new[] { "user_id", "is_default" });

            migrationBuilder.CreateIndex(
                name: "IX_password_reset_tokens_expires_at_utc",
                table: "password_reset_tokens",
                column: "expires_at_utc");

            migrationBuilder.CreateIndex(
                name: "IX_password_reset_tokens_user_id",
                table: "password_reset_tokens",
                column: "user_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "addresses");

            migrationBuilder.DropTable(
                name: "password_reset_tokens");
        }
    }
}
