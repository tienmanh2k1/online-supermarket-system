using System.Data;
using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage;
using MySql.Data.MySqlClient;
using OnlineSupermarket.Domain.Jobs;
using OnlineSupermarket.Infrastructure.Persistence;

namespace OnlineSupermarket.Infrastructure.Tests.Persistence;

[Collection(MySqlInfrastructureCollection.Name)]
public sealed class MySqlMigrationUpgradeTests(MySqlFixture fixture)
{
    private readonly MySqlFixture _fixture = fixture;

    private string MasterConnectionString => _fixture.MasterConnectionString;

    private string CreateTestDatabaseConnectionString(string dbName) =>
        _fixture.CreateDatabaseConnectionString(dbName);

    private DbContextOptions<AppDbContext> CreateOptions(string dbName) =>
        new DbContextOptionsBuilder<AppDbContext>()
            .UseMySQL(CreateTestDatabaseConnectionString(dbName))
            .Options;

    private async Task DropAndCreateDatabaseAsync(string dbName)
    {
        await using var master = new MySqlConnection(MasterConnectionString);
        await master.OpenAsync();

        await using (var dropCmd = new MySqlCommand($"DROP DATABASE IF EXISTS `{dbName}`;", master))
        {
            await dropCmd.ExecuteNonQueryAsync();
        }

        await using (var createCmd = new MySqlCommand($"CREATE DATABASE `{dbName}` CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci;", master))
        {
            await createCmd.ExecuteNonQueryAsync();
        }
    }

    private async Task DropDatabaseAsync(string dbName)
    {
        await using var master = new MySqlConnection(MasterConnectionString);
        await master.OpenAsync();

        await using var dropCmd = new MySqlCommand($"DROP DATABASE IF EXISTS `{dbName}`;", master);
        await dropCmd.ExecuteNonQueryAsync();
    }

    private async Task ExecuteSqlAsync(string dbName, string sql)
    {
        await using var conn = new MySqlConnection(CreateTestDatabaseConnectionString(dbName));
        await conn.OpenAsync();

        await using var cmd = new MySqlCommand(sql, conn);
        await cmd.ExecuteNonQueryAsync();
    }

    private static string ResolveFilePath(string relativePath)
    {
        var baseDir = AppContext.BaseDirectory;
        var candidate = Path.Combine(baseDir, relativePath);
        if (File.Exists(candidate)) return candidate;

        var dir = new DirectoryInfo(baseDir);
        while (dir != null)
        {
            var target = Path.Combine(dir.FullName, relativePath);
            if (File.Exists(target)) return target;
            dir = dir.Parent;
        }
        throw new FileNotFoundException($"Cannot locate file: {relativePath}");
    }

    private async Task ExecuteScriptFileAsync(string dbName, string scriptRelativePath)
    {
        var fullPath = ResolveFilePath(scriptRelativePath);
        var sql = await File.ReadAllTextAsync(fullPath);
        await using var conn = new MySqlConnection(CreateTestDatabaseConnectionString(dbName));
        await conn.OpenAsync();

        var script = new MySqlScript(conn, sql);
        await script.ExecuteAsync();
    }

    private static (int exitCode, string stdout, string stderr) RunProcess(
        string fileName,
        string arguments,
        string workingDirectory,
        IDictionary<string, string?>? envOverrides = null,
        int timeoutSeconds = 90)
    {
        var psi = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        if (envOverrides != null)
        {
            foreach (var (k, v) in envOverrides)
            {
                if (v == null) psi.Environment.Remove(k);
                else psi.Environment[k] = v;
            }
        }

        using var proc = new Process { StartInfo = psi };
        var stdoutBuilder = new System.Text.StringBuilder();
        var stderrBuilder = new System.Text.StringBuilder();

        proc.OutputDataReceived += (_, e) => { if (e.Data != null) stdoutBuilder.AppendLine(e.Data); };
        proc.ErrorDataReceived += (_, e) => { if (e.Data != null) stderrBuilder.AppendLine(e.Data); };

        if (!proc.Start())
        {
            throw new InvalidOperationException($"Failed to start {fileName}");
        }

        proc.BeginOutputReadLine();
        proc.BeginErrorReadLine();

        if (!proc.WaitForExit(TimeSpan.FromSeconds(timeoutSeconds)))
        {
            try { proc.Kill(entireProcessTree: true); } catch { }
            throw new TimeoutException($"Process '{fileName} {arguments}' timed out after {timeoutSeconds}s.\nStdout:\n{stdoutBuilder}\nStderr:\n{stderrBuilder}");
        }

        proc.WaitForExit();
        return (proc.ExitCode, stdoutBuilder.ToString(), stderrBuilder.ToString());
    }

    private static string GetRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "OnlineSupermarket.slnx")))
            {
                return dir.FullName;
            }
            dir = dir.Parent;
        }
        throw new DirectoryNotFoundException("Could not locate repository root containing OnlineSupermarket.slnx");
    }

    // =========================================================================
    // 1. Existing Legacy PascalCase & Baseline Tests
    // =========================================================================

    [Fact]
    public async Task UpgradeFromLegacyPascalCase_WithoutPrepareScript_FailsWithUnknownColumn()
    {
        var dbName = $"upgrade_pascal_fail_{Guid.NewGuid():N}";
        await DropAndCreateDatabaseAsync(dbName);

        try
        {
            await using (var db = new AppDbContext(CreateOptions(dbName)))
            {
                var migrator = db.Database.GetService<IMigrator>();
                await migrator!.MigrateAsync("20260903161025_AddProductViewEvents");
            }

            await ExecuteScriptFileAsync(dbName, "Persistence/Fixtures/LegacyBackgroundJobs.sql");

            var branchId = Guid.NewGuid();
            var forecastRunId = Guid.NewGuid();
            await ExecuteSqlAsync(dbName, $@"
                INSERT INTO `branches` (`id`, `name`, `address`, `phone`, `latitude`, `longitude`, `is_active`)
                VALUES ('{branchId}', 'Test Branch', '123 Test St', '0901234567', 10.76, 106.66, 1);

                INSERT INTO `background_job_runs` (`Id`, `JobName`, `LockKey`, `Status`, `CreatedAtUtc`, `LockToken`)
                VALUES ('{forecastRunId}', 'Forecast', 'branch:{branchId}', 'Queued', NOW(6), '{Guid.NewGuid()}');
            ");

            await using (var db = new AppDbContext(CreateOptions(dbName)))
            {
                var ex = await Assert.ThrowsAnyAsync<Exception>(() => db.Database.MigrateAsync());
                Assert.Contains("job_name", ex.Message, StringComparison.OrdinalIgnoreCase);
            }
        }
        finally
        {
            await DropDatabaseAsync(dbName);
        }
    }

    [Fact]
    public async Task UpgradeFromLegacyPascalCase_WithPrepareScript_SucceedsAndBackfillsBranch()
    {
        var dbName = $"upgrade_pascal_success_{Guid.NewGuid():N}";
        await DropAndCreateDatabaseAsync(dbName);

        try
        {
            await using (var db = new AppDbContext(CreateOptions(dbName)))
            {
                var migrator = db.Database.GetService<IMigrator>();
                await migrator!.MigrateAsync("20260903161025_AddProductViewEvents");
            }

            await ExecuteScriptFileAsync(dbName, "Persistence/Fixtures/LegacyBackgroundJobs.sql");

            var branchId = Guid.NewGuid();
            var forecastRunId = Guid.NewGuid();
            var recRunId = Guid.NewGuid();
            var lockToken = Guid.NewGuid().ToString();

            await ExecuteSqlAsync(dbName, $@"
                INSERT INTO `branches` (`id`, `name`, `address`, `phone`, `latitude`, `longitude`, `is_active`)
                VALUES ('{branchId}', 'Test Branch', '123 Test St', '0901234567', 10.76, 106.66, 1);

                INSERT INTO `background_job_runs` (`Id`, `JobName`, `LockKey`, `Status`, `CreatedAtUtc`, `LockToken`)
                VALUES 
                    ('{forecastRunId}', 'Forecast', 'branch:{branchId}', 'Queued', NOW(6), '{lockToken}'),
                    ('{recRunId}', 'Recommendations', 'global', 'Succeeded', NOW(6), NULL);
            ");

            await ExecuteScriptFileAsync(dbName, "backend/scripts/prepare-mysql-upgrade.sql");

            await using (var db = new AppDbContext(CreateOptions(dbName)))
            {
                await db.Database.MigrateAsync();

                var forecastRun = await db.BackgroundJobRuns.FirstOrDefaultAsync(r => r.Id == forecastRunId);
                Assert.NotNull(forecastRun);
                Assert.Equal("Forecast", forecastRun.JobName);
                Assert.Equal($"branch:{branchId}", forecastRun.LockKey);
                Assert.Equal(branchId, forecastRun.BranchId);
                Assert.Equal(lockToken, forecastRun.LockToken);
                Assert.Equal(JobRunStatus.Queued, forecastRun.Status);

                var recRun = await db.BackgroundJobRuns.FirstOrDefaultAsync(r => r.Id == recRunId);
                Assert.NotNull(recRun);
                Assert.Equal("Recommendations", recRun.JobName);
                Assert.Equal("global", recRun.LockKey);
                Assert.Null(recRun.BranchId);
                Assert.Equal(JobRunStatus.Succeeded, recRun.Status);
            }
        }
        finally
        {
            await DropDatabaseAsync(dbName);
        }
    }

    [Fact]
    public async Task UpgradeWithBothExistingConstraints_SucceedsWithoutDuplicateError()
    {
        var dbName = $"upgrade_both_dup_check_{Guid.NewGuid():N}";
        await DropAndCreateDatabaseAsync(dbName);

        try
        {
            await using (var db = new AppDbContext(CreateOptions(dbName)))
            {
                var migrator = db.Database.GetService<IMigrator>();
                await migrator!.MigrateAsync("20260905081854_SyncModelAndMigrations");
            }

            await ExecuteSqlAsync(dbName, @"
                ALTER TABLE `recommendation_results`
                ADD CONSTRAINT `ck_recommendation_results_rank` CHECK (`rank` > 0),
                ADD CONSTRAINT `ck_recommendation_results_score` CHECK (score + 0 >= 0 AND score + 0 <= 1);
            ");

            await using (var db = new AppDbContext(CreateOptions(dbName)))
            {
                await db.Database.MigrateAsync();
            }

            await using var conn = new MySqlConnection(CreateTestDatabaseConnectionString(dbName));
            await conn.OpenAsync();

            await using var cmd = new MySqlCommand(@"
                SELECT COUNT(*) FROM information_schema.TABLE_CONSTRAINTS
                WHERE TABLE_SCHEMA = DATABASE() 
                  AND TABLE_NAME = 'recommendation_results'
                  AND CONSTRAINT_TYPE = 'CHECK'
                  AND CONSTRAINT_NAME IN ('ck_recommendation_results_rank', 'ck_recommendation_results_score')
                  AND ENFORCED = 'YES';", conn);

            var count = Convert.ToInt64(await cmd.ExecuteScalarAsync());
            Assert.Equal(2L, count);
        }
        finally
        {
            await DropDatabaseAsync(dbName);
        }
    }

    [Fact]
    public async Task UpgradeWithOneMissingConstraint_AddsMissingConstraintSuccessfully()
    {
        var dbName = $"upgrade_safe_check_{Guid.NewGuid():N}";
        await DropAndCreateDatabaseAsync(dbName);

        try
        {
            await using (var db = new AppDbContext(CreateOptions(dbName)))
            {
                var migrator = db.Database.GetService<IMigrator>();
                await migrator!.MigrateAsync("20260905081854_SyncModelAndMigrations");
            }

            await ExecuteSqlAsync(dbName, @"
                ALTER TABLE `recommendation_results`
                ADD CONSTRAINT `ck_recommendation_results_rank` CHECK (`rank` > 0);
            ");

            await using (var db = new AppDbContext(CreateOptions(dbName)))
            {
                await db.Database.MigrateAsync();
            }

            await using var conn = new MySqlConnection(CreateTestDatabaseConnectionString(dbName));
            await conn.OpenAsync();

            await using var cmd = new MySqlCommand(@"
                SELECT COUNT(*) FROM information_schema.TABLE_CONSTRAINTS
                WHERE TABLE_SCHEMA = DATABASE() 
                  AND TABLE_NAME = 'recommendation_results'
                  AND CONSTRAINT_TYPE = 'CHECK'
                  AND CONSTRAINT_NAME IN ('ck_recommendation_results_rank', 'ck_recommendation_results_score')
                  AND ENFORCED = 'YES';", conn);

            var count = Convert.ToInt64(await cmd.ExecuteScalarAsync());
            Assert.Equal(2L, count);
        }
        finally
        {
            await DropDatabaseAsync(dbName);
        }
    }

    [Fact]
    public async Task Upgrade_Idempotency_CanBeRunTwiceWithoutError()
    {
        var dbName = $"upgrade_idempotent_{Guid.NewGuid():N}";
        await DropAndCreateDatabaseAsync(dbName);

        try
        {
            await using (var db = new AppDbContext(CreateOptions(dbName)))
            {
                await db.Database.MigrateAsync();
            }

            await ExecuteScriptFileAsync(dbName, "backend/scripts/prepare-mysql-upgrade.sql");

            await using (var db = new AppDbContext(CreateOptions(dbName)))
            {
                await db.Database.MigrateAsync();
                Assert.False(db.Database.HasPendingModelChanges());
                Assert.Empty(await db.Database.GetPendingMigrationsAsync());
            }

            await using var conn = new MySqlConnection(CreateTestDatabaseConnectionString(dbName));
            await conn.OpenAsync();

            await using var cmd = new MySqlCommand(@"
                SELECT COUNT(*) FROM information_schema.TABLES
                WHERE TABLE_SCHEMA = DATABASE()
                  AND TABLE_TYPE = 'BASE TABLE'
                  AND TABLE_NAME <> '__EFMigrationsHistory';", conn);

            var tableCount = Convert.ToInt64(await cmd.ExecuteScalarAsync());
            Assert.Equal(23L, tableCount);
        }
        finally
        {
            await DropDatabaseAsync(dbName);
        }
    }

    private static async Task<(Guid categoryId, Guid brandId, Guid productId, Guid jobRunId)> SeedCatalogAndJobRunAsync(
        Func<string, string, Task> executeSqlAsync, string dbName)
    {
        var categoryId = Guid.NewGuid();
        var brandId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var jobRunId = Guid.NewGuid();

        await executeSqlAsync(dbName, $@"
            INSERT INTO `categories` (`id`, `name`, `slug`, `parent_category_id`, `is_active`)
            VALUES ('{categoryId}', 'Cat', 'cat-{categoryId:N}', NULL, 1);

            INSERT INTO `brands` (`id`, `name`, `slug`, `is_active`)
            VALUES ('{brandId}', 'Brand', 'brand-{brandId:N}', 1);

            INSERT INTO `products` (`id`, `category_id`, `brand_id`, `sku`, `name`, `slug`, `description`, `base_price`, `unit`, `image_url`, `is_active`)
            VALUES ('{productId}', '{categoryId}', '{brandId}', 'SKU-{productId:N}', 'Prod', 'prod-{productId:N}', 'Desc', 10000, 'cái', NULL, 1);

            INSERT INTO `background_job_runs` (`id`, `job_name`, `lock_key`, `status`, `created_at_utc`)
            VALUES ('{jobRunId}', 'Recommendations', 'global', 'Succeeded', NOW(6));
        ");

        return (categoryId, brandId, productId, jobRunId);
    }

    [Fact]
    public async Task UpgradeWithViolatingRankData_FailsWithDiagnostic()
    {
        var dbName = $"upgrade_bad_rank_{Guid.NewGuid():N}";
        await DropAndCreateDatabaseAsync(dbName);

        try
        {
            await using (var db = new AppDbContext(CreateOptions(dbName)))
            {
                var migrator = db.Database.GetService<IMigrator>();
                await migrator!.MigrateAsync("20260905081854_SyncModelAndMigrations");
            }

            var (_, _, productId, jobRunId) = await SeedCatalogAndJobRunAsync(ExecuteSqlAsync, dbName);

            await ExecuteSqlAsync(dbName, $@"
                INSERT INTO `recommendation_results`
                (`id`, `scope`, `audience_key`, `user_id`, `source_product_id`, `recommended_product_id`, `score`, `rank`, `reason`, `algorithm_version`, `generated_at_utc`, `expires_at_utc`, `job_run_id`)
                VALUES
                ('{Guid.NewGuid()}', 'User', 'aud-1', NULL, NULL, '{productId}', 0.8, 0, 'Reason', 'v1', NOW(6), DATE_ADD(NOW(6), INTERVAL 7 DAY), '{jobRunId}');
            ");

            await using (var db = new AppDbContext(CreateOptions(dbName)))
            {
                var ex = await Assert.ThrowsAnyAsync<Exception>(() => db.Database.MigrateAsync());
                Assert.Contains("ck_recommendation_results_rank", ex.Message, StringComparison.OrdinalIgnoreCase);
            }
        }
        finally
        {
            await DropDatabaseAsync(dbName);
        }
    }

    [Fact]
    public async Task UpgradeWithViolatingScoreData_FailsWithDiagnostic()
    {
        var dbName = $"upgrade_bad_score_{Guid.NewGuid():N}";
        await DropAndCreateDatabaseAsync(dbName);

        try
        {
            await using (var db = new AppDbContext(CreateOptions(dbName)))
            {
                var migrator = db.Database.GetService<IMigrator>();
                await migrator!.MigrateAsync("20260905081854_SyncModelAndMigrations");
            }

            var (_, _, productId, jobRunId) = await SeedCatalogAndJobRunAsync(ExecuteSqlAsync, dbName);

            await ExecuteSqlAsync(dbName, $@"
                INSERT INTO `recommendation_results`
                (`id`, `scope`, `audience_key`, `user_id`, `source_product_id`, `recommended_product_id`, `score`, `rank`, `reason`, `algorithm_version`, `generated_at_utc`, `expires_at_utc`, `job_run_id`)
                VALUES
                ('{Guid.NewGuid()}', 'User', 'aud-2', NULL, NULL, '{productId}', 1.5, 1, 'Reason', 'v1', NOW(6), DATE_ADD(NOW(6), INTERVAL 7 DAY), '{jobRunId}');
            ");

            await using (var db = new AppDbContext(CreateOptions(dbName)))
            {
                var ex = await Assert.ThrowsAnyAsync<Exception>(() => db.Database.MigrateAsync());
                Assert.Contains("ck_recommendation_results_score", ex.Message, StringComparison.OrdinalIgnoreCase);
            }
        }
        finally
        {
            await DropDatabaseAsync(dbName);
        }
    }

    [Fact]
    public async Task Upgrade_EnforcesConstraintRejection_AndAllowsValidBoundaryValues()
    {
        var dbName = $"upgrade_bounds_{Guid.NewGuid():N}";
        await DropAndCreateDatabaseAsync(dbName);

        try
        {
            await using (var db = new AppDbContext(CreateOptions(dbName)))
            {
                await db.Database.MigrateAsync();
            }

            var (_, _, productId, jobRunId) = await SeedCatalogAndJobRunAsync(ExecuteSqlAsync, dbName);

            var exRank = await Assert.ThrowsAnyAsync<Exception>(() => ExecuteSqlAsync(dbName, $@"
                INSERT INTO `recommendation_results`
                (`id`, `scope`, `audience_key`, `user_id`, `source_product_id`, `recommended_product_id`, `score`, `rank`, `reason`, `algorithm_version`, `generated_at_utc`, `expires_at_utc`, `job_run_id`)
                VALUES
                ('{Guid.NewGuid()}', 'User', 'aud-r0', NULL, NULL, '{productId}', 0.5, 0, 'Reason', 'v1', NOW(6), DATE_ADD(NOW(6), INTERVAL 7 DAY), '{jobRunId}');
            "));
            Assert.Contains("ck_recommendation_results_rank", exRank.Message, StringComparison.OrdinalIgnoreCase);

            var exScoreNeg = await Assert.ThrowsAnyAsync<Exception>(() => ExecuteSqlAsync(dbName, $@"
                INSERT INTO `recommendation_results`
                (`id`, `scope`, `audience_key`, `user_id`, `source_product_id`, `recommended_product_id`, `score`, `rank`, `reason`, `algorithm_version`, `generated_at_utc`, `expires_at_utc`, `job_run_id`)
                VALUES
                ('{Guid.NewGuid()}', 'User', 'aud-sneg', NULL, NULL, '{productId}', -0.1, 1, 'Reason', 'v1', NOW(6), DATE_ADD(NOW(6), INTERVAL 7 DAY), '{jobRunId}');
            "));
            Assert.Contains("ck_recommendation_results_score", exScoreNeg.Message, StringComparison.OrdinalIgnoreCase);

            var exScoreOver = await Assert.ThrowsAnyAsync<Exception>(() => ExecuteSqlAsync(dbName, $@"
                INSERT INTO `recommendation_results`
                (`id`, `scope`, `audience_key`, `user_id`, `source_product_id`, `recommended_product_id`, `score`, `rank`, `reason`, `algorithm_version`, `generated_at_utc`, `expires_at_utc`, `job_run_id`)
                VALUES
                ('{Guid.NewGuid()}', 'User', 'aud-sover', NULL, NULL, '{productId}', 1.1, 1, 'Reason', 'v1', NOW(6), DATE_ADD(NOW(6), INTERVAL 7 DAY), '{jobRunId}');
            "));
            Assert.Contains("ck_recommendation_results_score", exScoreOver.Message, StringComparison.OrdinalIgnoreCase);

            await ExecuteSqlAsync(dbName, $@"
                INSERT INTO `recommendation_results`
                (`id`, `scope`, `audience_key`, `user_id`, `source_product_id`, `recommended_product_id`, `score`, `rank`, `reason`, `algorithm_version`, `generated_at_utc`, `expires_at_utc`, `job_run_id`)
                VALUES
                ('{Guid.NewGuid()}', 'User', 'aud-b1', NULL, NULL, '{productId}', 0.0, 1, 'Reason', 'v1', NOW(6), DATE_ADD(NOW(6), INTERVAL 7 DAY), '{jobRunId}');
            ");

            await ExecuteSqlAsync(dbName, $@"
                INSERT INTO `recommendation_results`
                (`id`, `scope`, `audience_key`, `user_id`, `source_product_id`, `recommended_product_id`, `score`, `rank`, `reason`, `algorithm_version`, `generated_at_utc`, `expires_at_utc`, `job_run_id`)
                VALUES
                ('{Guid.NewGuid()}', 'User', 'aud-b2', NULL, NULL, '{productId}', 1.0, 10, 'Reason', 'v1', NOW(6), DATE_ADD(NOW(6), INTERVAL 7 DAY), '{jobRunId}');
            ");
        }
        finally
        {
            await DropDatabaseAsync(dbName);
        }
    }

    // =========================================================================
    // 2. Finding 1 Tests: Constraint Whitelist & Rejection of Malformed Conditions
    // =========================================================================

    [Theory]
    [InlineData("`rank` > 0")]
    [InlineData("`rank` >= 1")]
    [InlineData("0 < `rank`")]
    [InlineData("1 <= `rank`")]
    public async Task UpgradeWithExistingRankConstraint_ValidWhitelistForms_Succeeds(string rankCondition)
    {
        var dbName = $"upgrade_rank_ok_{Guid.NewGuid():N}";
        await DropAndCreateDatabaseAsync(dbName);

        try
        {
            await using (var db = new AppDbContext(CreateOptions(dbName)))
            {
                var migrator = db.Database.GetService<IMigrator>();
                await migrator!.MigrateAsync("20260905081854_SyncModelAndMigrations");
            }

            await ExecuteSqlAsync(dbName, $@"
                ALTER TABLE `recommendation_results`
                ADD CONSTRAINT `ck_recommendation_results_rank` CHECK ({rankCondition});
            ");

            await using (var db = new AppDbContext(CreateOptions(dbName)))
            {
                await db.Database.MigrateAsync();
            }

            await using var conn = new MySqlConnection(CreateTestDatabaseConnectionString(dbName));
            await conn.OpenAsync();
            await using var cmd = new MySqlCommand(@"
                SELECT COUNT(*) FROM information_schema.TABLE_CONSTRAINTS
                WHERE TABLE_SCHEMA = DATABASE() 
                  AND TABLE_NAME = 'recommendation_results' 
                  AND CONSTRAINT_NAME = 'ck_recommendation_results_rank'
                  AND ENFORCED = 'YES';", conn);

            var count = Convert.ToInt64(await cmd.ExecuteScalarAsync());
            Assert.Equal(1L, count);
        }
        finally
        {
            await DropDatabaseAsync(dbName);
        }
    }

    [Theory]
    [InlineData("score + 0 >= 0 AND score + 0 <= 1")]
    [InlineData("score >= 0 AND score <= 1")]
    [InlineData("score + 0 <= 1 AND score + 0 >= 0")]
    [InlineData("score <= 1 AND score >= 0")]
    [InlineData("0 <= score AND score <= 1")]
    [InlineData("0 <= score + 0 AND score + 0 <= 1")]
    public async Task UpgradeWithExistingScoreConstraint_ValidWhitelistForms_Succeeds(string scoreCondition)
    {
        var dbName = $"upgrade_score_ok_{Guid.NewGuid():N}";
        await DropAndCreateDatabaseAsync(dbName);

        try
        {
            await using (var db = new AppDbContext(CreateOptions(dbName)))
            {
                var migrator = db.Database.GetService<IMigrator>();
                await migrator!.MigrateAsync("20260905081854_SyncModelAndMigrations");
            }

            await ExecuteSqlAsync(dbName, $@"
                ALTER TABLE `recommendation_results`
                ADD CONSTRAINT `ck_recommendation_results_score` CHECK ({scoreCondition});
            ");

            await using (var db = new AppDbContext(CreateOptions(dbName)))
            {
                await db.Database.MigrateAsync();
            }

            await using var conn = new MySqlConnection(CreateTestDatabaseConnectionString(dbName));
            await conn.OpenAsync();
            await using var cmd = new MySqlCommand(@"
                SELECT COUNT(*) FROM information_schema.TABLE_CONSTRAINTS
                WHERE TABLE_SCHEMA = DATABASE() 
                  AND TABLE_NAME = 'recommendation_results' 
                  AND CONSTRAINT_NAME = 'ck_recommendation_results_score'
                  AND ENFORCED = 'YES';", conn);

            var count = Convert.ToInt64(await cmd.ExecuteScalarAsync());
            Assert.Equal(1L, count);
        }
        finally
        {
            await DropDatabaseAsync(dbName);
        }
    }

    [Fact]
    public async Task UpgradeWithExistingRankConstraint_WithOrCondition_FailsWithDiagnostic()
    {
        var dbName = $"upgrade_rank_or_fail_{Guid.NewGuid():N}";
        await DropAndCreateDatabaseAsync(dbName);

        try
        {
            await using (var db = new AppDbContext(CreateOptions(dbName)))
            {
                var migrator = db.Database.GetService<IMigrator>();
                await migrator!.MigrateAsync("20260905081854_SyncModelAndMigrations");
            }

            // Permissive OR condition: rank > 0 OR rank = -1
            await ExecuteSqlAsync(dbName, @"
                ALTER TABLE `recommendation_results`
                ADD CONSTRAINT `ck_recommendation_results_rank` CHECK (`rank` > 0 OR `rank` = -1);
            ");

            await using (var db = new AppDbContext(CreateOptions(dbName)))
            {
                var ex = await Assert.ThrowsAnyAsync<Exception>(() => db.Database.MigrateAsync());
                Assert.Contains("ck_recommendation_results_rank", ex.Message, StringComparison.OrdinalIgnoreCase);
            }
        }
        finally
        {
            await DropDatabaseAsync(dbName);
        }
    }

    [Fact]
    public async Task UpgradeWithExistingScoreConstraint_WithOrCondition_FailsWithDiagnostic()
    {
        var dbName = $"upgrade_score_or_fail_{Guid.NewGuid():N}";
        await DropAndCreateDatabaseAsync(dbName);

        try
        {
            await using (var db = new AppDbContext(CreateOptions(dbName)))
            {
                var migrator = db.Database.GetService<IMigrator>();
                await migrator!.MigrateAsync("20260905081854_SyncModelAndMigrations");
            }

            // Permissive OR condition: score >= 0 OR score <= 1
            await ExecuteSqlAsync(dbName, @"
                ALTER TABLE `recommendation_results`
                ADD CONSTRAINT `ck_recommendation_results_score` CHECK (score >= 0 OR score <= 1);
            ");

            await using (var db = new AppDbContext(CreateOptions(dbName)))
            {
                var ex = await Assert.ThrowsAnyAsync<Exception>(() => db.Database.MigrateAsync());
                Assert.Contains("ck_recommendation_results_score", ex.Message, StringComparison.OrdinalIgnoreCase);
            }
        }
        finally
        {
            await DropDatabaseAsync(dbName);
        }
    }

    [Fact]
    public async Task UpgradeWithExistingScoreConstraint_WithTrickyPrecedenceCondition_FailsWithDiagnostic()
    {
        var dbName = $"upgrade_score_tricky_fail_{Guid.NewGuid():N}";
        await DropAndCreateDatabaseAsync(dbName);

        try
        {
            await using (var db = new AppDbContext(CreateOptions(dbName)))
            {
                var migrator = db.Database.GetService<IMigrator>();
                await migrator!.MigrateAsync("20260905081854_SyncModelAndMigrations");
            }

            // Tricky precedence condition: ((score + (0 >= 0)) AND (score + 0 <= 1))
            // MySQL stores: ((0 <> (`score` + (0 >= 0))) and ((`score` + 0) <= 1))
            await ExecuteSqlAsync(dbName, @"
                ALTER TABLE `recommendation_results`
                ADD CONSTRAINT `ck_recommendation_results_score` CHECK ((score + (0 >= 0)) AND (score + 0 <= 1));
            ");

            await using (var db = new AppDbContext(CreateOptions(dbName)))
            {
                var ex = await Assert.ThrowsAnyAsync<Exception>(() => db.Database.MigrateAsync());
                Assert.Contains("ck_recommendation_results_score", ex.Message, StringComparison.OrdinalIgnoreCase);
            }
        }
        finally
        {
            await DropDatabaseAsync(dbName);
        }
    }

    // =========================================================================
    // 3. Finding 4 Tests: Comprehensive Index Verification in prepare-mysql-upgrade.sql
    // =========================================================================

    [Fact]
    public async Task PrepareUpgrade_WhenIndexIsNonUnique_ThrowsDiagnostic()
    {
        var dbName = $"prep_idx_non_unique_{Guid.NewGuid():N}";
        await DropAndCreateDatabaseAsync(dbName);

        try
        {
            // Seed legacy PascalCase schema but with NON-UNIQUE index KEY instead of UNIQUE KEY
            await ExecuteSqlAsync(dbName, @"
                CREATE TABLE `background_job_runs` (
                    `Id` char(36) NOT NULL,
                    `JobName` varchar(100) NOT NULL,
                    `LockKey` varchar(100) NOT NULL,
                    `Status` varchar(20) NOT NULL,
                    `CreatedAtUtc` datetime(6) NOT NULL,
                    `StartedAtUtc` datetime(6) NULL,
                    `CompletedAtUtc` datetime(6) NULL,
                    `ErrorSummary` varchar(1000) NULL,
                    `LockToken` varchar(50) NULL,
                    `LeaseExpiresAtUtc` datetime(6) NULL,
                    PRIMARY KEY (`Id`),
                    KEY `IX_background_job_runs_JobName_LockKey` (`JobName`, `LockKey`)
                );
            ");

            var ex = await Assert.ThrowsAnyAsync<Exception>(() =>
                ExecuteScriptFileAsync(dbName, "backend/scripts/prepare-mysql-upgrade.sql"));

            Assert.Contains("must be UNIQUE", ex.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            await DropDatabaseAsync(dbName);
        }
    }

    [Fact]
    public async Task PrepareUpgrade_WhenIndexHasPrefix_ThrowsDiagnostic()
    {
        var dbName = $"prep_idx_prefix_{Guid.NewGuid():N}";
        await DropAndCreateDatabaseAsync(dbName);

        try
        {
            // Seed legacy PascalCase schema with prefix index JobName(50), LockKey(50) -> SUB_PART IS NOT NULL
            await ExecuteSqlAsync(dbName, @"
                CREATE TABLE `background_job_runs` (
                    `Id` char(36) NOT NULL,
                    `JobName` varchar(100) NOT NULL,
                    `LockKey` varchar(100) NOT NULL,
                    `Status` varchar(20) NOT NULL,
                    `CreatedAtUtc` datetime(6) NOT NULL,
                    `StartedAtUtc` datetime(6) NULL,
                    `CompletedAtUtc` datetime(6) NULL,
                    `ErrorSummary` varchar(1000) NULL,
                    `LockToken` varchar(50) NULL,
                    `LeaseExpiresAtUtc` datetime(6) NULL,
                    PRIMARY KEY (`Id`),
                    UNIQUE KEY `IX_background_job_runs_JobName_LockKey` (`JobName`(50), `LockKey`(50))
                );
            ");

            var ex = await Assert.ThrowsAnyAsync<Exception>(() =>
                ExecuteScriptFileAsync(dbName, "backend/scripts/prepare-mysql-upgrade.sql"));

            Assert.Contains("must be UNIQUE on full columns", ex.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            await DropDatabaseAsync(dbName);
        }
    }

    [Fact]
    public async Task PrepareUpgrade_WhenIndexHasWrongColumnsOrOrder_ThrowsDiagnostic()
    {
        var dbName = $"prep_idx_swapped_{Guid.NewGuid():N}";
        await DropAndCreateDatabaseAsync(dbName);

        try
        {
            // Seed legacy schema with swapped columns: LockKey, JobName
            await ExecuteSqlAsync(dbName, @"
                CREATE TABLE `background_job_runs` (
                    `Id` char(36) NOT NULL,
                    `JobName` varchar(100) NOT NULL,
                    `LockKey` varchar(100) NOT NULL,
                    `Status` varchar(20) NOT NULL,
                    `CreatedAtUtc` datetime(6) NOT NULL,
                    `StartedAtUtc` datetime(6) NULL,
                    `CompletedAtUtc` datetime(6) NULL,
                    `ErrorSummary` varchar(1000) NULL,
                    `LockToken` varchar(50) NULL,
                    `LeaseExpiresAtUtc` datetime(6) NULL,
                    PRIMARY KEY (`Id`),
                    UNIQUE KEY `IX_background_job_runs_JobName_LockKey` (`LockKey`, `JobName`)
                );
            ");

            var ex = await Assert.ThrowsAnyAsync<Exception>(() =>
                ExecuteScriptFileAsync(dbName, "backend/scripts/prepare-mysql-upgrade.sql"));

            Assert.Contains("must be UNIQUE on full columns", ex.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            await DropDatabaseAsync(dbName);
        }
    }

    [Fact]
    public async Task PrepareUpgrade_WhenIndexMissing_ThrowsDiagnostic()
    {
        var dbName = $"prep_idx_missing_{Guid.NewGuid():N}";
        await DropAndCreateDatabaseAsync(dbName);

        try
        {
            // Table exists but index is missing entirely
            await ExecuteSqlAsync(dbName, @"
                CREATE TABLE `background_job_runs` (
                    `Id` char(36) NOT NULL,
                    `JobName` varchar(100) NOT NULL,
                    `LockKey` varchar(100) NOT NULL,
                    `Status` varchar(20) NOT NULL,
                    `CreatedAtUtc` datetime(6) NOT NULL,
                    `StartedAtUtc` datetime(6) NULL,
                    `CompletedAtUtc` datetime(6) NULL,
                    `ErrorSummary` varchar(1000) NULL,
                    `LockToken` varchar(50) NULL,
                    `LeaseExpiresAtUtc` datetime(6) NULL,
                    PRIMARY KEY (`Id`)
                );
            ");

            var ex = await Assert.ThrowsAnyAsync<Exception>(() =>
                ExecuteScriptFileAsync(dbName, "backend/scripts/prepare-mysql-upgrade.sql"));

            Assert.Contains("Missing index", ex.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            await DropDatabaseAsync(dbName);
        }
    }

    [Fact]
    public async Task PrepareUpgrade_WhenBothIndexNamesExist_ThrowsDiagnostic()
    {
        var dbName = $"prep_idx_both_{Guid.NewGuid():N}";
        await DropAndCreateDatabaseAsync(dbName);

        try
        {
            // Table has both old and new index names simultaneously
            await ExecuteSqlAsync(dbName, @"
                CREATE TABLE `background_job_runs` (
                    `Id` char(36) NOT NULL,
                    `JobName` varchar(100) NOT NULL,
                    `LockKey` varchar(100) NOT NULL,
                    `Status` varchar(20) NOT NULL,
                    `CreatedAtUtc` datetime(6) NOT NULL,
                    `StartedAtUtc` datetime(6) NULL,
                    `CompletedAtUtc` datetime(6) NULL,
                    `ErrorSummary` varchar(1000) NULL,
                    `LockToken` varchar(50) NULL,
                    `LeaseExpiresAtUtc` datetime(6) NULL,
                    PRIMARY KEY (`Id`),
                    UNIQUE KEY `IX_background_job_runs_JobName_LockKey` (`JobName`, `LockKey`),
                    UNIQUE KEY `IX_background_job_runs_job_name_lock_key` (`JobName`, `LockKey`)
                );
            ");

            var ex = await Assert.ThrowsAnyAsync<Exception>(() =>
                ExecuteScriptFileAsync(dbName, "backend/scripts/prepare-mysql-upgrade.sql"));

            Assert.Contains("Inconsistent state", ex.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            await DropDatabaseAsync(dbName);
        }
    }

    // =========================================================================
    // 4. Finding 2 & 3 Tests: MySQL CLI Execution, Formatter & Partial Failure Recovery
    // =========================================================================

    [Fact]
    public async Task UpgradeScript_ExecutedViaMySqlCli_SucceedsEndToEnd()
    {
        var dbName = $"cli_upgrade_full_{Guid.NewGuid():N}";
        await DropAndCreateDatabaseAsync(dbName);

        var tempDir = Path.Combine(Path.GetTempPath(), $"ef_upgrade_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            // 1. Migrate target database up to baseline AddProductViewEvents
            await using (var db = new AppDbContext(CreateOptions(dbName)))
            {
                var migrator = db.Database.GetService<IMigrator>();
                await migrator!.MigrateAsync("20260903161025_AddProductViewEvents");
            }

            // 2. Apply legacy background job runs schema and data
            await ExecuteScriptFileAsync(dbName, "Persistence/Fixtures/LegacyBackgroundJobs.sql");

            // 3. Run prepare-mysql-upgrade.sql on target database via MySQL CLI inside container
            var prepareScriptPath = ResolveFilePath("backend/scripts/prepare-mysql-upgrade.sql");
            var prepareSql = await File.ReadAllTextAsync(prepareScriptPath);
            var prepResult = await _fixture.ExecuteCliAsync(dbName, prepareSql);
            Assert.Equal(0, prepResult.ExitCode);

            // 4. Query target database __EFMigrationsHistory for starting migration
            await using var conn = new MySqlConnection(CreateTestDatabaseConnectionString(dbName));
            await conn.OpenAsync();

            string startingMigration;
            await using (var cmd = new MySqlCommand(
                "SELECT MigrationId FROM `__EFMigrationsHistory` ORDER BY MigrationId DESC LIMIT 1;", conn))
            {
                startingMigration = (string)(await cmd.ExecuteScalarAsync())!;
            }
            Assert.Equal("20260903170019_AddBackgroundJobRuns", startingMigration);

            // 5. Generate migration range script from starting migration
            var rawScriptPath = Path.Combine(tempDir, "upgrade.raw.sql");
            var formattedScriptPath = Path.Combine(tempDir, "upgrade.formatted.sql");

            var repoRoot = GetRepoRoot();
            var (efExit, efOut, efErr) = RunProcess(
                "dotnet",
                $"ef migrations script {startingMigration} --project backend/src/OnlineSupermarket.Infrastructure --startup-project backend/src/OnlineSupermarket.Api --no-build -o \"{rawScriptPath}\"",
                repoRoot
            );
            Assert.True(efExit == 0, $"dotnet ef migrations script failed: {efOut}\n{efErr}");
            Assert.True(File.Exists(rawScriptPath), "Raw migration script was not created.");

            // 6. Format delimiters using prepare-migration-script.py
            var formatterScriptPath = ResolveFilePath("backend/scripts/prepare-migration-script.py");
            var (pyExit, pyOut, pyErr) = RunProcess(
                "python",
                $"\"{formatterScriptPath}\" \"{rawScriptPath}\" -o \"{formattedScriptPath}\"",
                repoRoot
            );
            Assert.True(pyExit == 0, $"prepare-migration-script.py failed: {pyOut}\n{pyErr}");
            Assert.True(File.Exists(formattedScriptPath), "Formatted migration script was not created.");

            // 7. Execute formatted script via actual MySQL CLI inside container
            var formattedSql = await File.ReadAllTextAsync(formattedScriptPath);
            var execResult = await _fixture.ExecuteCliAsync(dbName, formattedSql);
            Assert.True(execResult.ExitCode == 0, $"MySQL CLI execution failed: {execResult.Stderr}\n{execResult.Stdout}");

            // 8. Verify all 23 base tables exist
            await using (var cmd = new MySqlCommand(@"
                SELECT COUNT(*) FROM information_schema.TABLES
                WHERE TABLE_SCHEMA = DATABASE() 
                  AND TABLE_TYPE = 'BASE TABLE' 
                  AND TABLE_NAME <> '__EFMigrationsHistory';", conn))
            {
                var tableCount = Convert.ToInt64(await cmd.ExecuteScalarAsync());
                Assert.Equal(23L, tableCount);
            }

            // 9. Verify RestoreRecommendationConstraints check constraints are enforced
            await using (var cmd = new MySqlCommand(@"
                SELECT COUNT(*) FROM information_schema.TABLE_CONSTRAINTS 
                WHERE TABLE_SCHEMA = DATABASE() 
                  AND TABLE_NAME = 'recommendation_results' 
                  AND CONSTRAINT_TYPE = 'CHECK'
                  AND CONSTRAINT_NAME IN ('ck_recommendation_results_rank', 'ck_recommendation_results_score')
                  AND ENFORCED = 'YES';", conn))
            {
                var count = Convert.ToInt64(await cmd.ExecuteScalarAsync());
                Assert.Equal(2L, count);
            }
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
            await DropDatabaseAsync(dbName);
        }
    }

    [Fact]
    public async Task Upgrade_MidMigrationFailure_DemonstratesBlindRetryFailsAndBackupRestoresCleanly()
    {
        var dbName = $"mid_fail_{Guid.NewGuid():N}";
        var dumpPath = $"/tmp/backup_test_{Guid.NewGuid():N}.sql";
        await DropAndCreateDatabaseAsync(dbName);

        try
        {
            // 1. Establish database at 20260904151359_AddDemandForecasts
            await using (var db = new AppDbContext(CreateOptions(dbName)))
            {
                var migrator = db.Database.GetService<IMigrator>();
                await migrator!.MigrateAsync("20260904151359_AddDemandForecasts");
            }

            // 2. Seed realistic data into categories, brands, products, and background_job_runs
            var catId = Guid.NewGuid();
            var brandId = Guid.NewGuid();
            var prodId = Guid.NewGuid();
            var jobId = Guid.NewGuid();

            await ExecuteSqlAsync(dbName, $@"
                INSERT INTO `categories` (`id`, `name`, `slug`, `is_active`)
                VALUES ('{catId}', 'Produce', 'produce', 1);

                INSERT INTO `brands` (`id`, `name`, `slug`, `is_active`)
                VALUES ('{brandId}', 'Fresh Farms', 'fresh-farms', 1);

                INSERT INTO `products` (`id`, `category_id`, `brand_id`, `sku`, `name`, `slug`, `description`, `base_price`, `unit`, `is_active`)
                VALUES ('{prodId}', '{catId}', '{brandId}', 'PROD-FUJI-001', 'Organic Fuji Apples', 'organic-fuji-apples', 'Crisp fresh apples', 5.99, 'kg', 1);

                INSERT INTO `background_job_runs` (`id`, `job_name`, `lock_key`, `status`, `created_at_utc`, `lock_token`)
                VALUES ('{jobId}', 'Intelligence.DemandForecast', 'lock_forecast_1', 'Running', NOW(6), 'tok_123');
            ");

            // 3. Take a real database backup using mysqldump inside the container
            var dumpResult = await _fixture.DumpDatabaseAsync(dbName, dumpPath);
            Assert.True(dumpResult.ExitCode == 0, $"mysqldump failed with exit code {dumpResult.ExitCode}:\n{dumpResult.Stderr}");

            var dumpSize = await _fixture.GetFileSizeAsync(dumpPath);
            Assert.True(dumpSize > 0, $"Backup dump file size must be greater than 0, got {dumpSize} bytes.");

            // 4. Simulate mid-migration failure during AddBackgroundJobRunBranch:
            // Statement 1 succeeds (ADD COLUMN branch_id) and implicitly commits in MySQL DDL!
            await ExecuteSqlAsync(dbName, "ALTER TABLE `background_job_runs` ADD `branch_id` char(36) NULL;");

            // Statement 2 fails! (e.g. syntax error or aborted transaction)
            // Notice: __EFMigrationsHistory STILL records '20260904151359_AddDemandForecasts'.

            // 5. Demonstrate that blindly re-running migrations from __EFMigrationsHistory
            // FAILS with duplicate column name error!
            await using (var db = new AppDbContext(CreateOptions(dbName)))
            {
                var ex = await Assert.ThrowsAnyAsync<Exception>(() => db.Database.MigrateAsync());
                Assert.Contains("Duplicate column name", ex.Message, StringComparison.OrdinalIgnoreCase);
            }

            // 6. Recovery: drop corrupted database, recreate clean empty database, and restore from real dump
            await DropDatabaseAsync(dbName);
            await DropAndCreateDatabaseAsync(dbName);

            var restoreResult = await _fixture.RestoreDatabaseAsync(dbName, dumpPath);
            Assert.True(restoreResult.ExitCode == 0, $"mysql restore failed with exit code {restoreResult.ExitCode}:\n{restoreResult.Stderr}");

            // 7. Verify pre-upgrade state in the restored database (record counts, exact IDs, and values)
            await using (var conn = new MySqlConnection(CreateTestDatabaseConnectionString(dbName)))
            {
                await conn.OpenAsync();

                // Check categories
                await using var catCmd = new MySqlCommand($"SELECT `name`, `slug` FROM `categories` WHERE `id` = '{catId}'", conn);
                await using var catReader = await catCmd.ExecuteReaderAsync();
                Assert.True(await catReader.ReadAsync(), "Restored category must exist");
                Assert.Equal("Produce", catReader.GetString(0));
                Assert.Equal("produce", catReader.GetString(1));
                await catReader.CloseAsync();

                // Check products
                await using var prodCmd = new MySqlCommand($"SELECT `name`, `sku`, `base_price`, `unit` FROM `products` WHERE `id` = '{prodId}'", conn);
                await using var prodReader = await prodCmd.ExecuteReaderAsync();
                Assert.True(await prodReader.ReadAsync(), "Restored product must exist");
                Assert.Equal("Organic Fuji Apples", prodReader.GetString(0));
                Assert.Equal("PROD-FUJI-001", prodReader.GetString(1));
                Assert.Equal(5.99m, prodReader.GetDecimal(2));
                Assert.Equal("kg", prodReader.GetString(3));
                await prodReader.CloseAsync();

                // Check background_job_runs
                await using var jobCmd = new MySqlCommand($"SELECT `job_name`, `status`, `lock_token` FROM `background_job_runs` WHERE `id` = '{jobId}'", conn);
                await using var jobReader = await jobCmd.ExecuteReaderAsync();
                Assert.True(await jobReader.ReadAsync(), "Restored background job run must exist");
                Assert.Equal("Intelligence.DemandForecast", jobReader.GetString(0));
                Assert.Equal("Running", jobReader.GetString(1));
                Assert.Equal("tok_123", jobReader.GetString(2));
                await jobReader.CloseAsync();
            }

            // 8. Re-apply remaining migrations cleanly from the restored state
            await using (var db = new AppDbContext(CreateOptions(dbName)))
            {
                var pendingBefore = await db.Database.GetPendingMigrationsAsync();
                Assert.NotEmpty(pendingBefore);

                await db.Database.MigrateAsync(); // Completes all remaining migrations successfully

                Assert.False(db.Database.HasPendingModelChanges());
                Assert.Empty(await db.Database.GetPendingMigrationsAsync());
            }

            // 9. Verify post-upgrade state: all seeded data intact and new branch_id column accessible
            await using (var conn = new MySqlConnection(CreateTestDatabaseConnectionString(dbName)))
            {
                await conn.OpenAsync();

                // Verify category still intact
                await using var catCmd = new MySqlCommand($"SELECT `name` FROM `categories` WHERE `id` = '{catId}'", conn);
                Assert.Equal("Produce", (string?)await catCmd.ExecuteScalarAsync());

                // Verify product still intact
                await using var prodCmd = new MySqlCommand($"SELECT `base_price` FROM `products` WHERE `id` = '{prodId}'", conn);
                Assert.Equal(5.99m, Convert.ToDecimal(await prodCmd.ExecuteScalarAsync()));

                // Verify background_job_run still intact and branch_id is queryable
                await using var jobCmd = new MySqlCommand($"SELECT `branch_id`, `status` FROM `background_job_runs` WHERE `id` = '{jobId}'", conn);
                await using var jobReader = await jobCmd.ExecuteReaderAsync();
                Assert.True(await jobReader.ReadAsync());
                Assert.True(await jobReader.IsDBNullAsync(0), "branch_id should be NULL for pre-existing record");
                Assert.Equal("Running", jobReader.GetString(1));
            }
        }
        finally
        {
            await _fixture.DeleteFileAsync(dumpPath);
            await DropDatabaseAsync(dbName);
        }
    }

    [Fact]
    public async Task DotnetEfDatabaseUpdate_WithConnectionFlag_UpdatesTargetDatabaseWithoutEnv()
    {
        var dbName = $"cli_conn_flag_{Guid.NewGuid():N}";
        await DropAndCreateDatabaseAsync(dbName);

        try
        {
            var targetConn = CreateTestDatabaseConnectionString(dbName);
            var repoRoot = GetRepoRoot();

            // Run dotnet ef database update --connection <target>
            // explicitly isolating environment variables and ignoring appsettings
            var envOverrides = new Dictionary<string, string?>
            {
                ["ConnectionStrings__DefaultConnection"] = null,
                ["DEFAULT_CONNECTION"] = null,
                ["ConnectionStrings:DefaultConnection"] = null,
                ["IGNORE_APPSETTINGS"] = "1",
                ["ASPNETCORE_ENVIRONMENT"] = "Production",
                ["DOTNET_ENVIRONMENT"] = "Production"
            };

            var (exitCode, stdout, stderr) = RunProcess(
                "dotnet",
                $"ef database update --project backend/src/OnlineSupermarket.Infrastructure --startup-project backend/src/OnlineSupermarket.Api --connection \"{targetConn}\" --no-build",
                repoRoot,
                envOverrides
            );

            Assert.True(exitCode == 0, $"dotnet ef database update --connection failed:\n{stdout}\n{stderr}");

            // Verify the target database was created and migrated with all tables
            await using var conn = new MySqlConnection(targetConn);
            await conn.OpenAsync();

            await using var cmd = new MySqlCommand(@"
                SELECT COUNT(*) FROM information_schema.TABLES
                WHERE TABLE_SCHEMA = DATABASE() 
                  AND TABLE_TYPE = 'BASE TABLE' 
                  AND TABLE_NAME <> '__EFMigrationsHistory';", conn);

            var count = Convert.ToInt64(await cmd.ExecuteScalarAsync());
            Assert.Equal(23L, count);
        }
        finally
        {
            await DropDatabaseAsync(dbName);
        }
    }

    [Fact]
    public async Task DatabaseCreator_WhenConnectionOverridden_DelegatesToProviderAndManagesDatabase()
    {
        var dbName = $"creator_deleg_{Guid.NewGuid():N}";
        await DropDatabaseAsync(dbName); // Ensure database does not exist

        try
        {
            var targetConn = CreateTestDatabaseConnectionString(dbName);
            var factory = new AppDbContextFactory();

            // Resolve context with overridden connection via args
            await using var db = factory.CreateDbContext(new[] { "--connection", targetConn });
            var creator = db.Database.GetService<IRelationalDatabaseCreator>();

            // 1. Database does not exist yet -> Exists() and ExistsAsync() must return false (not fake true)
            Assert.False(creator.Exists(), "Exists() must return false when target database does not exist");
            Assert.False(await creator.ExistsAsync(), "ExistsAsync() must return false when target database does not exist");

            // 2. Create() must actually create the database on MySQL server
            creator.Create();
            Assert.True(creator.Exists(), "Exists() must return true after Create()");
            Assert.True(await creator.ExistsAsync(), "ExistsAsync() must return true after Create()");
            Assert.False(creator.HasTables(), "HasTables() must return false on newly created empty database");

            // 3. Delete() must actually drop the database on MySQL server
            creator.Delete();
            Assert.False(creator.Exists(), "Exists() must return false after Delete()");
            Assert.False(await creator.ExistsAsync(), "ExistsAsync() must return false after Delete()");

            // 4. Test async path: CreateAsync and DeleteAsync
            await creator.CreateAsync();
            Assert.True(await creator.ExistsAsync(), "ExistsAsync() must return true after CreateAsync()");

            await creator.DeleteAsync();
            Assert.False(await creator.ExistsAsync(), "ExistsAsync() must return false after DeleteAsync()");
        }
        finally
        {
            await DropDatabaseAsync(dbName);
        }
    }

    [Fact]
    public async Task DotnetEfDatabaseUpdate_WithoutConnectionOrConfig_FailsAndDoesNotTouchTargetDatabase()
    {
        var dbName = $"cli_guard_{Guid.NewGuid():N}";
        await DropAndCreateDatabaseAsync(dbName);

        try
        {
            var repoRoot = GetRepoRoot();

            // 1. Direct AppDbContextFactory and Guard behavior test:
            var savedEnv = new Dictionary<string, string?>
            {
                ["ConnectionStrings__DefaultConnection"] = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection"),
                ["DEFAULT_CONNECTION"] = Environment.GetEnvironmentVariable("DEFAULT_CONNECTION"),
                ["ConnectionStrings:DefaultConnection"] = Environment.GetEnvironmentVariable("ConnectionStrings:DefaultConnection"),
                ["IGNORE_APPSETTINGS"] = Environment.GetEnvironmentVariable("IGNORE_APPSETTINGS")
            };

            try
            {
                Environment.SetEnvironmentVariable("ConnectionStrings__DefaultConnection", null);
                Environment.SetEnvironmentVariable("DEFAULT_CONNECTION", null);
                Environment.SetEnvironmentVariable("ConnectionStrings:DefaultConnection", null);
                Environment.SetEnvironmentVariable("IGNORE_APPSETTINGS", "1"); // Bypasses appsettings.json fallback

                var factory = new AppDbContextFactory();
                await using var db = factory.CreateDbContext(Array.Empty<string>());

                // A. CanConnect / CanConnectAsync catches connection errors and returns false
                Assert.False(db.Database.CanConnect());
                Assert.False(await db.Database.CanConnectAsync());

                // B. EnsureCreated / EnsureCreatedAsync MUST throw guard exception before dialing out
                var exEnsureCreated = Assert.Throws<InvalidOperationException>(() => db.Database.EnsureCreated());
                Assert.Contains("design-time guard configuration is active", exEnsureCreated.Message, StringComparison.OrdinalIgnoreCase);

                var exEnsureCreatedAsync = await Assert.ThrowsAsync<InvalidOperationException>(() => db.Database.EnsureCreatedAsync());
                Assert.Contains("design-time guard configuration is active", exEnsureCreatedAsync.Message, StringComparison.OrdinalIgnoreCase);

                // C. Migrate / MigrateAsync MUST throw guard exception
                var exMigrate = await Assert.ThrowsAsync<InvalidOperationException>(() => db.Database.MigrateAsync());
                Assert.Contains("design-time guard configuration is active", exMigrate.Message, StringComparison.OrdinalIgnoreCase);

                // D. Direct IRelationalDatabaseCreator sync & async methods MUST throw guard exception
                var creator = db.Database.GetService<IRelationalDatabaseCreator>();

                var exExists = Assert.Throws<InvalidOperationException>(() => creator.Exists());
                Assert.Contains("design-time guard configuration is active", exExists.Message, StringComparison.OrdinalIgnoreCase);

                var exExistsAsync = await Assert.ThrowsAsync<InvalidOperationException>(() => creator.ExistsAsync());
                Assert.Contains("design-time guard configuration is active", exExistsAsync.Message, StringComparison.OrdinalIgnoreCase);

                var exCreate = Assert.Throws<InvalidOperationException>(() => creator.Create());
                Assert.Contains("design-time guard configuration is active", exCreate.Message, StringComparison.OrdinalIgnoreCase);

                var exCreateAsync = await Assert.ThrowsAsync<InvalidOperationException>(() => creator.CreateAsync());
                Assert.Contains("design-time guard configuration is active", exCreateAsync.Message, StringComparison.OrdinalIgnoreCase);

                var exDelete = Assert.Throws<InvalidOperationException>(() => creator.Delete());
                Assert.Contains("design-time guard configuration is active", exDelete.Message, StringComparison.OrdinalIgnoreCase);

                var exDeleteAsync = await Assert.ThrowsAsync<InvalidOperationException>(() => creator.DeleteAsync());
                Assert.Contains("design-time guard configuration is active", exDeleteAsync.Message, StringComparison.OrdinalIgnoreCase);

                var exHasTables = Assert.Throws<InvalidOperationException>(() => creator.HasTables());
                Assert.Contains("design-time guard configuration is active", exHasTables.Message, StringComparison.OrdinalIgnoreCase);

                var exHasTablesAsync = await Assert.ThrowsAsync<InvalidOperationException>(() => creator.HasTablesAsync());
                Assert.Contains("design-time guard configuration is active", exHasTablesAsync.Message, StringComparison.OrdinalIgnoreCase);
            }
            finally
            {
                foreach (var (k, v) in savedEnv)
                {
                    Environment.SetEnvironmentVariable(k, v);
                }
            }
        }
        finally
        {
            await DropDatabaseAsync(dbName);
        }
    }

    [Fact]
    public async Task DatabaseCreator_WhenUsingGuardConnection_ThenOverridesWithRealConnection_StillDelegatesToProvider()
    {
        // This test verifies that when Guard is active and we override the connection via SetConnectionString,
        // the Guard's database creator still delegates to the actual MySQL provider.

        var dbName = $"guard_override_{Guid.NewGuid():N}";
        await DropDatabaseAsync(dbName); // Ensure database does not exist

        try
        {
            var targetConn = CreateTestDatabaseConnectionString(dbName);

            // Step 1: Create context with Guard (no connection specified)
            var savedEnv = new Dictionary<string, string?>
            {
                ["ConnectionStrings__DefaultConnection"] = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection"),
                ["DEFAULT_CONNECTION"] = Environment.GetEnvironmentVariable("DEFAULT_CONNECTION"),
                ["ConnectionStrings:DefaultConnection"] = Environment.GetEnvironmentVariable("ConnectionStrings:DefaultConnection"),
                ["IGNORE_APPSETTINGS"] = Environment.GetEnvironmentVariable("IGNORE_APPSETTINGS")
            };

            try
            {
                Environment.SetEnvironmentVariable("ConnectionStrings__DefaultConnection", null);
                Environment.SetEnvironmentVariable("DEFAULT_CONNECTION", null);
                Environment.SetEnvironmentVariable("ConnectionStrings:DefaultConnection", null);
                Environment.SetEnvironmentVariable("IGNORE_APPSETTINGS", "1");

                // Create factory - will produce Guard context
                var factory = new AppDbContextFactory();
                await using var db = factory.CreateDbContext(Array.Empty<string>());

                // Verify Guard is active
                var creator = db.Database.GetService<IRelationalDatabaseCreator>();
                Assert.Throws<InvalidOperationException>(() => creator.Exists());

                // Step 2: Override connection on the SAME context using SetConnectionString
                // This should switch from Guard mode to real provider mode
                db.Database.SetConnectionString(targetConn);

                // Step 3: Verify the creator now delegates to MySQL provider (not Guard)
                Assert.False(creator.Exists(), "Database should not exist yet");
                Assert.False(await creator.ExistsAsync(), "Database should not exist yet (async)");

                // Test sync path
                creator.Create();
                Assert.True(creator.Exists(), "Database should exist after Create()");
                Assert.True(await creator.ExistsAsync(), "Database should exist after CreateAsync()");

                creator.Delete();
                Assert.False(creator.Exists(), "Database should not exist after Delete()");
                Assert.False(await creator.ExistsAsync(), "Database should not exist after DeleteAsync()");

                // Test async path
                await creator.CreateAsync();
                Assert.True(await creator.ExistsAsync(), "Database should exist after CreateAsync()");

                await creator.DeleteAsync();
                Assert.False(await creator.ExistsAsync(), "Database should not exist after DeleteAsync()");
            }
            finally
            {
                foreach (var (k, v) in savedEnv)
                {
                    Environment.SetEnvironmentVariable(k, v);
                }
            }
        }
        finally
        {
            await DropDatabaseAsync(dbName);
        }
    }

    [Fact]
    public async Task DotnetEfCli_WithoutConnectionOrConfig_FailsAndDoesNotTouchTargetDatabase()
    {
        var dbName = $"cli_guard_{Guid.NewGuid():N}";
        await DropAndCreateDatabaseAsync(dbName);

        try
        {
            var repoRoot = GetRepoRoot();

            // Execute dotnet ef database update when missing all connection configuration and appsettings
            var cliEnvOverrides = new Dictionary<string, string?>
            {
                ["ConnectionStrings__DefaultConnection"] = "",
                ["DEFAULT_CONNECTION"] = "",
                ["ConnectionStrings:DefaultConnection"] = "",
                ["IGNORE_APPSETTINGS"] = "1",
                ["ASPNETCORE_ENVIRONMENT"] = "Production",
                ["DOTNET_ENVIRONMENT"] = "Production"
            };

            var (cliExit, cliOut, cliErr) = RunProcess(
                "dotnet",
                "ef database update --project backend/src/OnlineSupermarket.Infrastructure --startup-project backend/src/OnlineSupermarket.Infrastructure --no-build",
                repoRoot,
                cliEnvOverrides
            );

            // Command must fail with non-zero exit code and report guard error
            Assert.True(cliExit != 0, $"Expected dotnet ef database update without config to fail, but succeeded with exit code 0:\n{cliOut}\n{cliErr}");
            Assert.Contains("design-time guard configuration is active", cliOut + cliErr, StringComparison.OrdinalIgnoreCase);

            // 3. Verify target database was completely untouched (0 tables)
            await using var conn = new MySqlConnection(CreateTestDatabaseConnectionString(dbName));
            await conn.OpenAsync();

            await using var cmd = new MySqlCommand(@"
                SELECT COUNT(*) FROM information_schema.TABLES
                WHERE TABLE_SCHEMA = DATABASE()
                  AND TABLE_TYPE = 'BASE TABLE';", conn);

            var count = Convert.ToInt64(await cmd.ExecuteScalarAsync());
            Assert.Equal(0L, count);
        }
        finally
        {
            await DropDatabaseAsync(dbName);
        }
    }

    [Theory]
    [InlineData("INSERT INTO audit_log VALUES (10);")]
    [InlineData("BEGIN INSERT INTO audit_log VALUES (10); END;")]
    [InlineData("inner_block: BEGIN INSERT INTO audit_log VALUES (10); END `inner_block`;")]
    [InlineData("BEGIN BEGIN INSERT INTO audit_log VALUES (CASE WHEN 1=1 THEN 10 ELSE 0 END); END; END;")]
    [InlineData("inner_block: BEGIN INSERT INTO audit_log VALUES (10); END /* label */ `inner_block` /* terminator */;")]
    public async Task ScriptFormatter_PreservesNestedBlocksAndStatementsBetweenMixedDelimiters(string body)
    {
        var repoRoot = GetRepoRoot();
        var scriptPath = ResolveFilePath("backend/scripts/prepare-migration-script.py");
        var sql = "CREATE TABLE audit_log (value INT);\n"
            + "CREATE PROCEDURE `p`()\nouter_block: BEGIN\n"
            + body + "\nINSERT INTO audit_log VALUES (1);\nEND `outer_block`;\n"
            + "CALL p();\nDELIMITER ;;\n"
            + "CREATE PROCEDURE q() BEGIN INSERT INTO audit_log VALUES (100); END;;\n"
            + "DELIMITER ;\nCALL q();\n";
        var tempDir = Path.Combine(Path.GetTempPath(), $"py_fmt_regression_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        var dbName = $"fmt_regression_{Guid.NewGuid():N}";

        try
        {
            await DropAndCreateDatabaseAsync(dbName);
            var input = Path.Combine(tempDir, "input.sql");
            var output = Path.Combine(tempDir, "output.sql");
            var secondOutput = Path.Combine(tempDir, "second.sql");
            File.WriteAllText(input, sql);

            var (exit, stdout, stderr) = RunProcess("python", $"\"{scriptPath}\" \"{input}\" -o \"{output}\"", repoRoot);
            Assert.True(exit == 0, $"Formatter failed: {stdout}\n{stderr}");
            var formatted = File.ReadAllText(output);
            Assert.Contains(body, formatted);
            Assert.Contains("CALL p();", formatted);
            Assert.Contains("CALL q();", formatted);
            Assert.Equal(1, formatted.Split("CREATE PROCEDURE `p`()").Length - 1);
            Assert.Equal(1, formatted.Split("CREATE PROCEDURE q()").Length - 1);
            Assert.Equal(1, formatted.Split("END `outer_block`;;").Length - 1);

            var (secondExit, secondStdout, secondStderr) = RunProcess("python", $"\"{scriptPath}\" \"{output}\" -o \"{secondOutput}\"", repoRoot);
            Assert.True(secondExit == 0, $"Second pass failed: {secondStdout}\n{secondStderr}");
            Assert.Equal(formatted, File.ReadAllText(secondOutput));

            var cli = await _fixture.ExecuteCliAsync(dbName, formatted);
            Assert.True(cli.ExitCode == 0, $"Formatted SQL failed in MySQL: {cli.Stderr}");
            await using var conn = new MySqlConnection(CreateTestDatabaseConnectionString(dbName));
            await conn.OpenAsync();
            await using var cmd = new MySqlCommand("SELECT GROUP_CONCAT(value ORDER BY value) FROM audit_log", conn);
            Assert.Equal("1,10,100", Convert.ToString(await cmd.ExecuteScalarAsync()));
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
            await DropDatabaseAsync(dbName);
        }
    }

    [Fact]
    public async Task ScriptFormatter_HandlesMultipleProcedures_Comments_Strings_CaseExpressions_AndIsIdempotent()
    {
        var repoRoot = GetRepoRoot();
        var scriptPath = ResolveFilePath("backend/scripts/prepare-migration-script.py");

        var testSql = @"-- Comment with ; and DELIMITER ;; inside comment
SELECT 1 AS delimiter_col;
/* Block comment
   with DELIMITER ;; inside
*/
-- note DELIMITER ;;
CREATE PROCEDURE `p1`()
BEGIN
    -- Comment with ; inside procedure
    DECLARE msg VARCHAR(100) DEFAULT 'literal with ; inside and DELIMITER ;;';
    IF 1 = 1 THEN
        SELECT CASE WHEN 1=1 THEN 1 ELSE 0 END;
    ELSE
        SELECT 2;
    END IF;
END;
SELECT 2;
CREATE PROCEDURE `p2`()
proc_main: BEGIN
    SELECT 3;
END proc_main;
SELECT 4;
CREATE PROCEDURE `p3`()
BEGIN
    IF 1 = 1 THEN
        CASE 1
            WHEN 1 THEN
                SELECT CASE WHEN 2=2 THEN 'A' ELSE 'B' END;
            ELSE
                SELECT 'C';
        END CASE;
    END IF;
END;
SELECT 5;
";

        var tempDir = Path.Combine(Path.GetTempPath(), $"py_fmt_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        var dbName = $"fmt_exec_{Guid.NewGuid():N}";
        await DropAndCreateDatabaseAsync(dbName);

        try
        {
            var inPath = Path.Combine(tempDir, "input.sql");
            var outPath = Path.Combine(tempDir, "output.sql");
            File.WriteAllText(inPath, testSql);

            // Pass 1
            var (exit1, out1, err1) = RunProcess("python", $"\"{scriptPath}\" \"{inPath}\" -o \"{outPath}\"", repoRoot);
            Assert.True(exit1 == 0, $"Pass 1 failed: {out1}\n{err1}");

            var formattedPass1 = File.ReadAllText(outPath).Replace("\r\n", "\n");
            Assert.Contains("DELIMITER ;;\n\n/* Block comment", formattedPass1);
            Assert.Contains("-- note DELIMITER ;;\nCREATE PROCEDURE `p1`()", formattedPass1);
            Assert.Contains("END;;\n\nDELIMITER ;\n", formattedPass1);
            Assert.Contains("DELIMITER ;;\n\nCREATE PROCEDURE `p2`()", formattedPass1);
            Assert.Contains("END proc_main;;\n\nDELIMITER ;\n", formattedPass1);
            Assert.Contains("DELIMITER ;;\n\nCREATE PROCEDURE `p3`()", formattedPass1);

            // CASE expression inside p1 must preserve END; and not cut procedure early
            Assert.Contains("SELECT CASE WHEN 1=1 THEN 1 ELSE 0 END;", formattedPass1);

            // Nested IF + CASE statement + CASE expression inside p3
            Assert.Contains("SELECT CASE WHEN 2=2 THEN 'A' ELSE 'B' END;", formattedPass1);
            Assert.Contains("END CASE;", formattedPass1);

            // Comments and literals containing DELIMITER ;; must remain verbatim
            Assert.Contains("-- Comment with ; and DELIMITER ;; inside comment", formattedPass1);
            Assert.Contains("-- note DELIMITER ;;", formattedPass1);
            Assert.Contains("'literal with ; inside and DELIMITER ;;'", formattedPass1);
            Assert.Contains("SELECT 1 AS delimiter_col;", formattedPass1);

            // Pass 2: Re-run on already formatted script (Idempotency test)
            var idempotentOutPath = Path.Combine(tempDir, "idempotent.sql");
            var (exit2, out2, err2) = RunProcess("python", $"\"{scriptPath}\" \"{outPath}\" -o \"{idempotentOutPath}\"", repoRoot);
            Assert.True(exit2 == 0, $"Pass 2 failed: {out2}\n{err2}");

            var formattedPass2 = File.ReadAllText(idempotentOutPath).Replace("\r\n", "\n");
            Assert.Equal(formattedPass1, formattedPass2);

            // Execute the formatted SQL script via MySQL CLI in the container to prove real execution validity
            var cliResult = await _fixture.ExecuteCliAsync(dbName, formattedPass1);
            Assert.True(cliResult.ExitCode == 0, $"MySQL CLI execution of formatted script failed:\n{cliResult.Stderr}");
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
            await DropDatabaseAsync(dbName);
        }
    }
}
