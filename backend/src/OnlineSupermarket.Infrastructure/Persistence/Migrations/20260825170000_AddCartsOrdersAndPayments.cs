using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OnlineSupermarket.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCartsOrdersAndPayments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "carts",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "char(36)", nullable: false),
                    user_id = table.Column<Guid>(type: "char(36)", nullable: false),
                    branch_id = table.Column<Guid>(type: "char(36)", nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_carts", x => x.id);
                })
;

            migrationBuilder.CreateTable(
                name: "cart_items",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "char(36)", nullable: false),
                    cart_id = table.Column<Guid>(type: "char(36)", nullable: false),
                    product_id = table.Column<Guid>(type: "char(36)", nullable: false),
                    branch_inventory_id = table.Column<Guid>(type: "char(36)", nullable: false),
                    unit_price = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    quantity = table.Column<int>(type: "int", nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cart_items", x => x.id);
                    table.ForeignKey(
                        name: "FK_cart_items_carts_cart_id",
                        column: x => x.cart_id,
                        principalTable: "carts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                })
;

            migrationBuilder.CreateTable(
                name: "orders",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "char(36)", nullable: false),
                    user_id = table.Column<Guid>(type: "char(36)", nullable: false),
                    branch_id = table.Column<Guid>(type: "char(36)", nullable: false),
                    fulfillment_type = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false),
                    delivery_address_id = table.Column<Guid>(type: "char(36)", nullable: true),
                    recipient_name = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false),
                    recipient_phone = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false),
                    delivery_address_snapshot = table.Column<string>(type: "text", nullable: true),
                    subtotal = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    discount_amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    shipping_fee = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    total_amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    promotion_id = table.Column<Guid>(type: "char(36)", nullable: true),
                    promotion_code_snapshot = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: true),
                    status = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_orders", x => x.id);
                })
;

            migrationBuilder.CreateTable(
                name: "order_items",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "char(36)", nullable: false),
                    order_id = table.Column<Guid>(type: "char(36)", nullable: false),
                    product_id = table.Column<Guid>(type: "char(36)", nullable: false),
                    product_name = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false),
                    sku = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false),
                    unit_price = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    quantity = table.Column<int>(type: "int", nullable: false),
                    line_total = table.Column<decimal>(type: "decimal(18,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_order_items", x => x.id);
                    table.ForeignKey(
                        name: "FK_order_items_orders_order_id",
                        column: x => x.order_id,
                        principalTable: "orders",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                })
;

            migrationBuilder.CreateTable(
                name: "order_status_histories",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "char(36)", nullable: false),
                    order_id = table.Column<Guid>(type: "char(36)", nullable: false),
                    from_status = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false),
                    to_status = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false),
                    note = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true),
                    created_at_utc = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_order_status_histories", x => x.id);
                    table.ForeignKey(
                        name: "FK_order_status_histories_orders_order_id",
                        column: x => x.order_id,
                        principalTable: "orders",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                })
;

            migrationBuilder.CreateTable(
                name: "payments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "char(36)", nullable: false),
                    order_id = table.Column<Guid>(type: "char(36)", nullable: false),
                    method = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false),
                    status = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: false),
                    amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    provider_transaction_id = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: true),
                    provider_response = table.Column<string>(type: "text", nullable: true),
                    created_at_utc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    completed_at_utc = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_payments", x => x.id);
                    table.ForeignKey(
                        name: "FK_payments_orders_order_id",
                        column: x => x.order_id,
                        principalTable: "orders",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                })
;

            migrationBuilder.CreateTable(
                name: "payment_callbacks",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "char(36)", nullable: false),
                    payment_id = table.Column<Guid>(type: "char(36)", nullable: false),
                    provider = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false),
                    external_event_id = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false),
                    raw_response = table.Column<string>(type: "text", nullable: false),
                    is_valid_signature = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    amount = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    result_status = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: false),
                    received_at_utc = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_payment_callbacks", x => x.id);
                    table.ForeignKey(
                        name: "FK_payment_callbacks_payments_payment_id",
                        column: x => x.payment_id,
                        principalTable: "payments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                })
;

            migrationBuilder.CreateIndex(
                name: "ix_carts_user_id",
                table: "carts",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_carts_user_branch",
                table: "carts",
                columns: new[] { "user_id", "branch_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_cart_items_cart_id",
                table: "cart_items",
                column: "cart_id");

            migrationBuilder.CreateIndex(
                name: "ix_orders_user_id",
                table: "orders",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_orders_status",
                table: "orders",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "ix_orders_created",
                table: "orders",
                column: "created_at_utc");

            migrationBuilder.CreateIndex(
                name: "ix_order_items_order_id",
                table: "order_items",
                column: "order_id");

            migrationBuilder.CreateIndex(
                name: "ix_order_status_histories_order_id",
                table: "order_status_histories",
                column: "order_id");

            migrationBuilder.CreateIndex(
                name: "ix_payments_order_id",
                table: "payments",
                column: "order_id");

            migrationBuilder.CreateIndex(
                name: "ix_payments_provider_tx_id",
                table: "payments",
                column: "provider_transaction_id");

            migrationBuilder.CreateIndex(
                name: "ix_payment_callbacks_provider_event",
                table: "payment_callbacks",
                columns: new[] { "provider", "external_event_id" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "payment_callbacks");
            migrationBuilder.DropTable(name: "payments");
            migrationBuilder.DropTable(name: "order_status_histories");
            migrationBuilder.DropTable(name: "order_items");
            migrationBuilder.DropTable(name: "orders");
            migrationBuilder.DropTable(name: "cart_items");
            migrationBuilder.DropTable(name: "carts");
        }
    }
}
