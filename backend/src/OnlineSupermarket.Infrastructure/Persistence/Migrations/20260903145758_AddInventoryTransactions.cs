using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OnlineSupermarket.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddInventoryTransactions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "inventory_transactions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "char(36)", nullable: false),
                    branch_inventory_id = table.Column<Guid>(type: "char(36)", nullable: false),
                    transaction_type = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: false),
                    quantity_on_hand_delta = table.Column<int>(type: "int", nullable: false),
                    reserved_quantity_delta = table.Column<int>(type: "int", nullable: false),
                    quantity_on_hand_after = table.Column<int>(type: "int", nullable: false),
                    reserved_quantity_after = table.Column<int>(type: "int", nullable: false),
                    reference_type = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: false),
                    reference_id = table.Column<Guid>(type: "char(36)", nullable: true),
                    operation_key = table.Column<string>(type: "varchar(180)", maxLength: 180, nullable: true),
                    actor_user_id = table.Column<Guid>(type: "char(36)", nullable: true),
                    note = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true),
                    created_at_utc = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_inventory_transactions", x => x.id);
                    table.ForeignKey(
                        name: "FK_inventory_transactions_branch_inventories_branch_inventory_id",
                        column: x => x.branch_inventory_id,
                        principalTable: "branch_inventories",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_inventory_transactions_users_actor_user_id",
                        column: x => x.actor_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                })
;

            migrationBuilder.CreateIndex(
                name: "IX_inventory_transactions_actor_user_id",
                table: "inventory_transactions",
                column: "actor_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_inventory_transactions_inventory_created",
                table: "inventory_transactions",
                columns: new[] { "branch_inventory_id", "created_at_utc" });

            migrationBuilder.CreateIndex(
                name: "ix_inventory_transactions_operation_key",
                table: "inventory_transactions",
                column: "operation_key",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "inventory_transactions");
        }
    }
}
