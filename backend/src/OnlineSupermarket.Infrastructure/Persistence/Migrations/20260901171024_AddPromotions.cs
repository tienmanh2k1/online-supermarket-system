using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OnlineSupermarket.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPromotions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "promotions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "char(36)", nullable: false),
                    code = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false),
                    discount_type = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false),
                    discount_value = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    min_order_amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    usage_limit = table.Column<int>(type: "int", nullable: true),
                    usage_count = table.Column<int>(type: "int", nullable: false),
                    is_active = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_promotions", x => x.id);
                })
;

            migrationBuilder.CreateIndex(
                name: "ix_promotions_code",
                table: "promotions",
                column: "code",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "promotions");
        }
    }
}
