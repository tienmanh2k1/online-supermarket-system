using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OnlineSupermarket.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddBackgroundJobRunBranch : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "branch_id",
                table: "background_job_runs",
                type: "char(36)",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_background_job_runs_branch_id",
                table: "background_job_runs",
                column: "branch_id");

            migrationBuilder.AddForeignKey(
                name: "FK_background_job_runs_branches_branch_id",
                table: "background_job_runs",
                column: "branch_id",
                principalTable: "branches",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.Sql(
                "UPDATE background_job_runs " +
                "SET branch_id = SUBSTRING_INDEX(lock_key, ':', -1) " +
                "WHERE job_name = 'Forecast' " +
                "AND lock_key LIKE 'branch:%' " +
                "AND branch_id IS NULL;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_background_job_runs_branches_branch_id",
                table: "background_job_runs");

            migrationBuilder.DropIndex(
                name: "IX_background_job_runs_branch_id",
                table: "background_job_runs");

            migrationBuilder.DropColumn(
                name: "branch_id",
                table: "background_job_runs");
        }
    }
}
