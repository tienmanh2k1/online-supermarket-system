using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OnlineSupermarket.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddBackgroundJobRuns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "background_job_runs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "char(36)", nullable: false),
                    job_name = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false),
                    lock_key = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false),
                    status = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    started_at_utc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    completed_at_utc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    error_summary = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: true),
                    lock_token = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: true),
                    lease_expires_at_utc = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_background_job_runs", x => x.id);
                })
;

            migrationBuilder.CreateIndex(
                name: "IX_background_job_runs_job_name_lock_key",
                table: "background_job_runs",
                columns: new[] { "job_name", "lock_key" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "background_job_runs");
        }
    }
}
