-- =============================================================================
-- LegacyBackgroundJobs.sql
-- Nguồn: git show ad74a69:backend/src/OnlineSupermarket.Infrastructure/Persistence/Migrations/20260903170019_AddBackgroundJobRuns.cs
--        git show ad74a69:backend/src/OnlineSupermarket.Infrastructure/Persistence/Migrations/20260904055925_AddRecommendationResults.cs
-- Mục đích: Mô phỏng schema và dữ liệu từ các bản phát hành lịch sử để kiểm thử nâng cấp.
-- =============================================================================

-- -----------------------------------------------------------------------------
-- 1. DDL Bảng background_job_runs lịch sử với cột PascalCase
-- Tương ứng với migration 20260903170019_AddBackgroundJobRuns trước khi sửa thành snake_case
-- -----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS `background_job_runs` (
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
    CONSTRAINT `PK_background_job_runs` PRIMARY KEY (`Id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

CREATE UNIQUE INDEX `IX_background_job_runs_JobName_LockKey`
    ON `background_job_runs` (`JobName`, `LockKey`);

-- -----------------------------------------------------------------------------
-- 2. Đăng ký migration lịch sử vào __EFMigrationsHistory
-- -----------------------------------------------------------------------------
INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
VALUES ('20260903170019_AddBackgroundJobRuns', '10.0.9')
ON DUPLICATE KEY UPDATE `ProductVersion` = VALUES(`ProductVersion`);
