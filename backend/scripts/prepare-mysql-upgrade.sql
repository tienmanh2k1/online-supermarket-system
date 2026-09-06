-- =============================================================================
-- prepare-mysql-upgrade.sql
-- Mục đích: Chuẩn hóa tên cột và index từ PascalCase sang snake_case cho bảng
--          background_job_runs trước khi áp dụng các EF Core migrations mới.
-- An toàn: Idempotent (chạy nhiều lần an toàn), giữ nguyên kiểu dữ liệu,
--          nullability, dữ liệu và khóa ngoại. Không drop/recreate table.
-- =============================================================================

DROP PROCEDURE IF EXISTS `sp_prepare_mysql_upgrade`;

DELIMITER ;;

CREATE PROCEDURE `sp_prepare_mysql_upgrade`()
proc_main: BEGIN
    DECLARE v_table_exists INT DEFAULT 0;
    DECLARE v_has_branch_col INT DEFAULT 0;
    DECLARE v_has_branch_migration INT DEFAULT 0;
    DECLARE v_old_cnt INT DEFAULT 0;
    DECLARE v_new_cnt INT DEFAULT 0;
    DECLARE v_old_idx INT DEFAULT 0;
    DECLARE v_new_idx INT DEFAULT 0;
    DECLARE v_idx_col_count INT DEFAULT 0;
    DECLARE v_idx_non_unique INT DEFAULT 0;
    DECLARE v_idx_prefix_count INT DEFAULT 0;
    DECLARE v_idx_col1 VARCHAR(100) DEFAULT '';
    DECLARE v_idx_col2 VARCHAR(100) DEFAULT '';

    -- 1. Kiểm tra bảng background_job_runs có tồn tại trong database hiện tại không
    SELECT COUNT(*) INTO v_table_exists
    FROM information_schema.TABLES
    WHERE TABLE_SCHEMA = DATABASE()
      AND TABLE_NAME = 'background_job_runs';

    IF v_table_exists = 0 THEN
        -- Bảng chưa tồn tại (database mới trước khi chạy AddBackgroundJobRuns), an toàn bỏ qua
        LEAVE proc_main;
    END IF;

    -- 2. Kiểm tra trạng thái di trú dở dang:
    -- Nếu cột branch_id đã tồn tại nhưng migration AddBackgroundJobRunBranch chưa được ghi nhận
    SELECT COUNT(*) INTO v_has_branch_col
    FROM information_schema.COLUMNS
    WHERE TABLE_SCHEMA = DATABASE()
      AND TABLE_NAME = 'background_job_runs'
      AND COLUMN_NAME = 'branch_id';

    IF v_has_branch_col > 0 THEN
        SELECT COUNT(*) INTO v_has_branch_migration
        FROM information_schema.TABLES
        WHERE TABLE_SCHEMA = DATABASE()
          AND TABLE_NAME = '__EFMigrationsHistory';

        IF v_has_branch_migration > 0 THEN
            SELECT COUNT(*) INTO v_has_branch_migration
            FROM `__EFMigrationsHistory`
            WHERE `MigrationId` = '20260904160056_AddBackgroundJobRunBranch';
        END IF;

        IF v_has_branch_migration = 0 THEN
            SIGNAL SQLSTATE '45000'
                SET MESSAGE_TEXT = 'Incomplete migration state detected: column branch_id exists in background_job_runs but AddBackgroundJobRunBranch is missing in __EFMigrationsHistory. Please inspect database before proceeding.';
        END IF;
    END IF;

    -- 3. Xử lý riêng biệt Id -> id (so sánh nhị phân BINARY để tránh nhầm do collation)
    SELECT COUNT(*) INTO v_old_cnt
    FROM information_schema.COLUMNS
    WHERE TABLE_SCHEMA = DATABASE()
      AND TABLE_NAME = 'background_job_runs'
      AND BINARY COLUMN_NAME = 'Id';

    IF v_old_cnt = 1 THEN
        ALTER TABLE `background_job_runs` RENAME COLUMN `Id` TO `id`;
    END IF;

    -- 4. Xử lý riêng biệt Status -> status
    SELECT COUNT(*) INTO v_old_cnt
    FROM information_schema.COLUMNS
    WHERE TABLE_SCHEMA = DATABASE()
      AND TABLE_NAME = 'background_job_runs'
      AND BINARY COLUMN_NAME = 'Status';

    IF v_old_cnt = 1 THEN
        ALTER TABLE `background_job_runs` RENAME COLUMN `Status` TO `status`;
    END IF;

    -- 5. Chuẩn hóa JobName -> job_name
    SELECT COUNT(*) INTO v_old_cnt
    FROM information_schema.COLUMNS
    WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'background_job_runs' AND BINARY COLUMN_NAME = 'JobName';

    SELECT COUNT(*) INTO v_new_cnt
    FROM information_schema.COLUMNS
    WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'background_job_runs' AND BINARY COLUMN_NAME = 'job_name';

    IF v_old_cnt = 1 AND v_new_cnt = 0 THEN
        ALTER TABLE `background_job_runs` RENAME COLUMN `JobName` TO `job_name`;
    ELSEIF v_old_cnt = 1 AND v_new_cnt = 1 THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'Inconsistent state: background_job_runs has both JobName and job_name columns.';
    ELSEIF v_old_cnt = 0 AND v_new_cnt = 0 THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'Missing column: background_job_runs has neither JobName nor job_name.';
    END IF;

    -- 6. Chuẩn hóa LockKey -> lock_key
    SELECT COUNT(*) INTO v_old_cnt
    FROM information_schema.COLUMNS
    WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'background_job_runs' AND BINARY COLUMN_NAME = 'LockKey';

    SELECT COUNT(*) INTO v_new_cnt
    FROM information_schema.COLUMNS
    WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'background_job_runs' AND BINARY COLUMN_NAME = 'lock_key';

    IF v_old_cnt = 1 AND v_new_cnt = 0 THEN
        ALTER TABLE `background_job_runs` RENAME COLUMN `LockKey` TO `lock_key`;
    ELSEIF v_old_cnt = 1 AND v_new_cnt = 1 THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'Inconsistent state: background_job_runs has both LockKey and lock_key columns.';
    ELSEIF v_old_cnt = 0 AND v_new_cnt = 0 THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'Missing column: background_job_runs has neither LockKey nor lock_key.';
    END IF;

    -- 7. Chuẩn hóa CreatedAtUtc -> created_at_utc
    SELECT COUNT(*) INTO v_old_cnt
    FROM information_schema.COLUMNS
    WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'background_job_runs' AND BINARY COLUMN_NAME = 'CreatedAtUtc';

    SELECT COUNT(*) INTO v_new_cnt
    FROM information_schema.COLUMNS
    WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'background_job_runs' AND BINARY COLUMN_NAME = 'created_at_utc';

    IF v_old_cnt = 1 AND v_new_cnt = 0 THEN
        ALTER TABLE `background_job_runs` RENAME COLUMN `CreatedAtUtc` TO `created_at_utc`;
    ELSEIF v_old_cnt = 1 AND v_new_cnt = 1 THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'Inconsistent state: background_job_runs has both CreatedAtUtc and created_at_utc columns.';
    ELSEIF v_old_cnt = 0 AND v_new_cnt = 0 THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'Missing column: background_job_runs has neither CreatedAtUtc nor created_at_utc.';
    END IF;

    -- 8. Chuẩn hóa StartedAtUtc -> started_at_utc
    SELECT COUNT(*) INTO v_old_cnt
    FROM information_schema.COLUMNS
    WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'background_job_runs' AND BINARY COLUMN_NAME = 'StartedAtUtc';

    SELECT COUNT(*) INTO v_new_cnt
    FROM information_schema.COLUMNS
    WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'background_job_runs' AND BINARY COLUMN_NAME = 'started_at_utc';

    IF v_old_cnt = 1 AND v_new_cnt = 0 THEN
        ALTER TABLE `background_job_runs` RENAME COLUMN `StartedAtUtc` TO `started_at_utc`;
    ELSEIF v_old_cnt = 1 AND v_new_cnt = 1 THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'Inconsistent state: background_job_runs has both StartedAtUtc and started_at_utc columns.';
    ELSEIF v_old_cnt = 0 AND v_new_cnt = 0 THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'Missing column: background_job_runs has neither StartedAtUtc nor started_at_utc.';
    END IF;

    -- 9. Chuẩn hóa CompletedAtUtc -> completed_at_utc
    SELECT COUNT(*) INTO v_old_cnt
    FROM information_schema.COLUMNS
    WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'background_job_runs' AND BINARY COLUMN_NAME = 'CompletedAtUtc';

    SELECT COUNT(*) INTO v_new_cnt
    FROM information_schema.COLUMNS
    WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'background_job_runs' AND BINARY COLUMN_NAME = 'completed_at_utc';

    IF v_old_cnt = 1 AND v_new_cnt = 0 THEN
        ALTER TABLE `background_job_runs` RENAME COLUMN `CompletedAtUtc` TO `completed_at_utc`;
    ELSEIF v_old_cnt = 1 AND v_new_cnt = 1 THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'Inconsistent state: background_job_runs has both CompletedAtUtc and completed_at_utc columns.';
    ELSEIF v_old_cnt = 0 AND v_new_cnt = 0 THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'Missing column: background_job_runs has neither CompletedAtUtc nor completed_at_utc.';
    END IF;

    -- 10. Chuẩn hóa ErrorSummary -> error_summary
    SELECT COUNT(*) INTO v_old_cnt
    FROM information_schema.COLUMNS
    WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'background_job_runs' AND BINARY COLUMN_NAME = 'ErrorSummary';

    SELECT COUNT(*) INTO v_new_cnt
    FROM information_schema.COLUMNS
    WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'background_job_runs' AND BINARY COLUMN_NAME = 'error_summary';

    IF v_old_cnt = 1 AND v_new_cnt = 0 THEN
        ALTER TABLE `background_job_runs` RENAME COLUMN `ErrorSummary` TO `error_summary`;
    ELSEIF v_old_cnt = 1 AND v_new_cnt = 1 THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'Inconsistent state: background_job_runs has both ErrorSummary and error_summary columns.';
    ELSEIF v_old_cnt = 0 AND v_new_cnt = 0 THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'Missing column: background_job_runs has neither ErrorSummary nor error_summary.';
    END IF;

    -- 11. Chuẩn hóa LockToken -> lock_token
    SELECT COUNT(*) INTO v_old_cnt
    FROM information_schema.COLUMNS
    WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'background_job_runs' AND BINARY COLUMN_NAME = 'LockToken';

    SELECT COUNT(*) INTO v_new_cnt
    FROM information_schema.COLUMNS
    WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'background_job_runs' AND BINARY COLUMN_NAME = 'lock_token';

    IF v_old_cnt = 1 AND v_new_cnt = 0 THEN
        ALTER TABLE `background_job_runs` RENAME COLUMN `LockToken` TO `lock_token`;
    ELSEIF v_old_cnt = 1 AND v_new_cnt = 1 THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'Inconsistent state: background_job_runs has both LockToken and lock_token columns.';
    ELSEIF v_old_cnt = 0 AND v_new_cnt = 0 THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'Missing column: background_job_runs has neither LockToken nor lock_token.';
    END IF;

    -- 12. Chuẩn hóa LeaseExpiresAtUtc -> lease_expires_at_utc
    SELECT COUNT(*) INTO v_old_cnt
    FROM information_schema.COLUMNS
    WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'background_job_runs' AND BINARY COLUMN_NAME = 'LeaseExpiresAtUtc';

    SELECT COUNT(*) INTO v_new_cnt
    FROM information_schema.COLUMNS
    WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'background_job_runs' AND BINARY COLUMN_NAME = 'lease_expires_at_utc';

    IF v_old_cnt = 1 AND v_new_cnt = 0 THEN
        ALTER TABLE `background_job_runs` RENAME COLUMN `LeaseExpiresAtUtc` TO `lease_expires_at_utc`;
    ELSEIF v_old_cnt = 1 AND v_new_cnt = 1 THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'Inconsistent state: background_job_runs has both LeaseExpiresAtUtc and lease_expires_at_utc columns.';
    ELSEIF v_old_cnt = 0 AND v_new_cnt = 0 THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'Missing column: background_job_runs has neither LeaseExpiresAtUtc nor lease_expires_at_utc.';
    END IF;

    -- 13. Chuẩn hóa tên Index và kiểm tra tính toàn vẹn (NON_UNIQUE = 0, SUB_PART IS NULL, cột job_name, lock_key)
    SELECT COUNT(*) INTO v_old_idx
    FROM information_schema.STATISTICS
    WHERE TABLE_SCHEMA = DATABASE()
      AND TABLE_NAME = 'background_job_runs'
      AND INDEX_NAME = 'IX_background_job_runs_JobName_LockKey';

    SELECT COUNT(*) INTO v_new_idx
    FROM information_schema.STATISTICS
    WHERE TABLE_SCHEMA = DATABASE()
      AND TABLE_NAME = 'background_job_runs'
      AND INDEX_NAME = 'IX_background_job_runs_job_name_lock_key';

    IF v_old_idx > 0 AND v_new_idx > 0 THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'Inconsistent state: background_job_runs has both legacy and snake_case indexes.';
    ELSEIF v_old_idx = 0 AND v_new_idx = 0 THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'Missing index: background_job_runs has neither legacy nor snake_case index.';
    END IF;

    IF v_old_idx > 0 THEN
        SELECT
            COUNT(*),
            MAX(NON_UNIQUE),
            COUNT(SUB_PART),
            COALESCE(MAX(CASE WHEN SEQ_IN_INDEX = 1 THEN LOWER(COLUMN_NAME) END), ''),
            COALESCE(MAX(CASE WHEN SEQ_IN_INDEX = 2 THEN LOWER(COLUMN_NAME) END), '')
        INTO v_idx_col_count, v_idx_non_unique, v_idx_prefix_count, v_idx_col1, v_idx_col2
        FROM information_schema.STATISTICS
        WHERE TABLE_SCHEMA = DATABASE()
          AND TABLE_NAME = 'background_job_runs'
          AND INDEX_NAME = 'IX_background_job_runs_JobName_LockKey';

        IF v_idx_col_count <> 2 OR v_idx_non_unique <> 0 OR v_idx_prefix_count <> 0 OR v_idx_col1 <> 'job_name' OR v_idx_col2 <> 'lock_key' THEN
            SIGNAL SQLSTATE '45000'
                SET MESSAGE_TEXT = 'Index IX_background_job_runs_JobName_LockKey is invalid: must be UNIQUE on full columns (job_name, lock_key).';
        END IF;

        ALTER TABLE `background_job_runs`
        RENAME INDEX `IX_background_job_runs_JobName_LockKey` TO `IX_background_job_runs_job_name_lock_key`;
    ELSEIF v_new_idx > 0 THEN
        SELECT
            COUNT(*),
            MAX(NON_UNIQUE),
            COUNT(SUB_PART),
            COALESCE(MAX(CASE WHEN SEQ_IN_INDEX = 1 THEN LOWER(COLUMN_NAME) END), ''),
            COALESCE(MAX(CASE WHEN SEQ_IN_INDEX = 2 THEN LOWER(COLUMN_NAME) END), '')
        INTO v_idx_col_count, v_idx_non_unique, v_idx_prefix_count, v_idx_col1, v_idx_col2
        FROM information_schema.STATISTICS
        WHERE TABLE_SCHEMA = DATABASE()
          AND TABLE_NAME = 'background_job_runs'
          AND INDEX_NAME = 'IX_background_job_runs_job_name_lock_key';

        IF v_idx_col_count <> 2 OR v_idx_non_unique <> 0 OR v_idx_prefix_count <> 0 OR v_idx_col1 <> 'job_name' OR v_idx_col2 <> 'lock_key' THEN
            SIGNAL SQLSTATE '45000'
                SET MESSAGE_TEXT = 'Index IX_background_job_runs_job_name_lock_key is invalid: must be UNIQUE on full columns (job_name, lock_key).';
        END IF;
    END IF;

END proc_main;;

DELIMITER ;

CALL `sp_prepare_mysql_upgrade`();
DROP PROCEDURE IF EXISTS `sp_prepare_mysql_upgrade`;
