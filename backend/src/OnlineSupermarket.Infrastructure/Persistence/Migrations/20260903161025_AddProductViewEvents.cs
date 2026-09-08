using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OnlineSupermarket.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddProductViewEvents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "product_view_events",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "char(36)", nullable: false),
                    product_id = table.Column<Guid>(type: "char(36)", nullable: false),
                    user_id = table.Column<Guid>(type: "char(36)", nullable: true),
                    anonymous_session_id = table.Column<Guid>(type: "char(36)", nullable: true),
                    branch_id = table.Column<Guid>(type: "char(36)", nullable: true),
                    viewed_at_utc = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_product_view_events", x => x.id);
                    table.ForeignKey(
                        name: "FK_product_view_events_branches_branch_id",
                        column: x => x.branch_id,
                        principalTable: "branches",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_product_view_events_products_product_id",
                        column: x => x.product_id,
                        principalTable: "products",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_product_view_events_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                })
;

            migrationBuilder.CreateIndex(
                name: "ix_product_view_events_anonymous_viewed",
                table: "product_view_events",
                columns: new[] { "anonymous_session_id", "viewed_at_utc" });

            migrationBuilder.CreateIndex(
                name: "IX_product_view_events_branch_id",
                table: "product_view_events",
                column: "branch_id");

            migrationBuilder.CreateIndex(
                name: "ix_product_view_events_product_viewed",
                table: "product_view_events",
                columns: new[] { "product_id", "viewed_at_utc" });

            migrationBuilder.CreateIndex(
                name: "ix_product_view_events_user_viewed",
                table: "product_view_events",
                columns: new[] { "user_id", "viewed_at_utc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "product_view_events");
        }
    }
}
