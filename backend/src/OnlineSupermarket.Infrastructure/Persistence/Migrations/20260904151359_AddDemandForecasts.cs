using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OnlineSupermarket.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDemandForecasts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "demand_forecasts",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "char(36)", nullable: false),
                    branch_inventory_id = table.Column<Guid>(type: "char(36)", nullable: false),
                    horizon_days = table.Column<int>(type: "int", nullable: false),
                    forecast_start_date = table.Column<DateOnly>(type: "date", nullable: false),
                    forecast_end_date = table.Column<DateOnly>(type: "date", nullable: false),
                    predicted_quantity = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    actual_data_days = table.Column<int>(type: "int", nullable: false),
                    data_quality = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false),
                    algorithm_version = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false),
                    generated_at_utc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    job_run_id = table.Column<Guid>(type: "char(36)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_demand_forecasts", x => x.id);
                    table.ForeignKey(
                        name: "FK_demand_forecasts_background_job_runs_job_run_id",
                        column: x => x.job_run_id,
                        principalTable: "background_job_runs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_demand_forecasts_branch_inventories_branch_inventory_id",
                        column: x => x.branch_inventory_id,
                        principalTable: "branch_inventories",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.CheckConstraint("ck_demand_forecasts_horizon", "horizon_days IN (7, 14)");
                    table.CheckConstraint("ck_demand_forecasts_predicted_quantity", "predicted_quantity + 0 >= 0");
                    table.CheckConstraint("ck_demand_forecasts_actual_data_days", "actual_data_days + 0 >= 0 AND actual_data_days <= 28");
                })
;

            migrationBuilder.CreateIndex(
                name: "IX_demand_forecasts_branch_inventory_id",
                table: "demand_forecasts",
                column: "branch_inventory_id");

            migrationBuilder.CreateIndex(
                name: "ix_demand_forecasts_run_inventory_horizon",
                table: "demand_forecasts",
                columns: new[] { "job_run_id", "branch_inventory_id", "horizon_days" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "demand_forecasts");
        }
    }
}
