using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OnlineSupermarket.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SyncModelAndMigrations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Empty to only sync the model snapshot without changing the DB
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Empty
        }
    }
}
