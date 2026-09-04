using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OnlineSupermarket.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRecommendationResults : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "recommendation_results",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "char(36)", nullable: false),
                    scope = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: false),
                    audience_key = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false),
                    user_id = table.Column<Guid>(type: "char(36)", nullable: true),
                    source_product_id = table.Column<Guid>(type: "char(36)", nullable: true),
                    recommended_product_id = table.Column<Guid>(type: "char(36)", nullable: false),
                    score = table.Column<decimal>(type: "decimal(12,6)", precision: 12, scale: 6, nullable: false),
                    rank = table.Column<int>(type: "int", nullable: false),
                    reason = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: false),
                    algorithm_version = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false),
                    generated_at_utc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    expires_at_utc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    job_run_id = table.Column<Guid>(type: "char(36)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_recommendation_results", x => x.id);
                    table.CheckConstraint("ck_recommendation_results_rank", "rank > 0");
                    table.CheckConstraint("ck_recommendation_results_score", "score >= 0 AND score <= 1");
                    table.ForeignKey(
                        name: "FK_recommendation_results_background_job_runs_job_run_id",
                        column: x => x.job_run_id,
                        principalTable: "background_job_runs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_recommendation_results_products_recommended_product_id",
                        column: x => x.recommended_product_id,
                        principalTable: "products",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_recommendation_results_products_source_product_id",
                        column: x => x.source_product_id,
                        principalTable: "products",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_recommendation_results_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_recommendation_results_recommended_product_id",
                table: "recommendation_results",
                column: "recommended_product_id");

            migrationBuilder.CreateIndex(
                name: "ix_recommendation_results_run_audience_product",
                table: "recommendation_results",
                columns: new[] { "job_run_id", "audience_key", "recommended_product_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_recommendation_results_source_product_id",
                table: "recommendation_results",
                column: "source_product_id");

            migrationBuilder.CreateIndex(
                name: "IX_recommendation_results_user_id",
                table: "recommendation_results",
                column: "user_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "recommendation_results");
        }
    }
}
