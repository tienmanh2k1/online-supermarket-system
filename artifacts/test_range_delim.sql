START TRANSACTION;
CREATE TABLE `background_job_runs` (
    `id` char(36) NOT NULL,
    `job_name` varchar(100) NOT NULL,
    `lock_key` varchar(100) NOT NULL,
    `status` varchar(20) NOT NULL,
    `created_at_utc` datetime(6) NOT NULL,
    `started_at_utc` datetime(6) NULL,
    `completed_at_utc` datetime(6) NULL,
    `error_summary` varchar(1000) NULL,
    `lock_token` varchar(50) NULL,
    `lease_expires_at_utc` datetime(6) NULL,
    PRIMARY KEY (`id`)
);

CREATE UNIQUE INDEX `IX_background_job_runs_job_name_lock_key` ON `background_job_runs` (`job_name`, `lock_key`);

INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
VALUES ('20260903170019_AddBackgroundJobRuns', '10.0.9');

CREATE TABLE `recommendation_results` (
    `id` char(36) NOT NULL,
    `scope` varchar(30) NOT NULL,
    `audience_key` varchar(100) NOT NULL,
    `user_id` char(36) NULL,
    `source_product_id` char(36) NULL,
    `recommended_product_id` char(36) NOT NULL,
    `score` decimal(12,6) NOT NULL,
    `rank` int NOT NULL,
    `reason` varchar(500) NOT NULL,
    `algorithm_version` varchar(50) NOT NULL,
    `generated_at_utc` datetime(6) NOT NULL,
    `expires_at_utc` datetime(6) NOT NULL,
    `job_run_id` char(36) NOT NULL,
    PRIMARY KEY (`id`),
    CONSTRAINT `FK_recommendation_results_background_job_runs_job_run_id` FOREIGN KEY (`job_run_id`) REFERENCES `background_job_runs` (`Id`) ON DELETE RESTRICT,
    CONSTRAINT `FK_recommendation_results_products_recommended_product_id` FOREIGN KEY (`recommended_product_id`) REFERENCES `products` (`id`) ON DELETE RESTRICT,
    CONSTRAINT `FK_recommendation_results_products_source_product_id` FOREIGN KEY (`source_product_id`) REFERENCES `products` (`id`) ON DELETE RESTRICT,
    CONSTRAINT `FK_recommendation_results_users_user_id` FOREIGN KEY (`user_id`) REFERENCES `users` (`id`) ON DELETE RESTRICT
);

CREATE INDEX `IX_recommendation_results_recommended_product_id` ON `recommendation_results` (`recommended_product_id`);

CREATE UNIQUE INDEX `ix_recommendation_results_run_audience_product` ON `recommendation_results` (`job_run_id`, `audience_key`, `recommended_product_id`);

CREATE INDEX `IX_recommendation_results_source_product_id` ON `recommendation_results` (`source_product_id`);

CREATE INDEX `IX_recommendation_results_user_id` ON `recommendation_results` (`user_id`);

INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
VALUES ('20260904055925_AddRecommendationResults', '10.0.9');

CREATE TABLE `demand_forecasts` (
    `id` char(36) NOT NULL,
    `branch_inventory_id` char(36) NOT NULL,
    `horizon_days` int NOT NULL,
    `forecast_start_date` date NOT NULL,
    `forecast_end_date` date NOT NULL,
    `predicted_quantity` decimal(18,2) NOT NULL,
    `actual_data_days` int NOT NULL,
    `data_quality` varchar(20) NOT NULL,
    `algorithm_version` varchar(50) NOT NULL,
    `generated_at_utc` datetime(6) NOT NULL,
    `job_run_id` char(36) NOT NULL,
    PRIMARY KEY (`id`),
    CONSTRAINT `ck_demand_forecasts_horizon` CHECK (horizon_days IN (7, 14)),
    CONSTRAINT `ck_demand_forecasts_predicted_quantity` CHECK (predicted_quantity + 0 >= 0),
    CONSTRAINT `ck_demand_forecasts_actual_data_days` CHECK (actual_data_days + 0 >= 0 AND actual_data_days <= 28),
    CONSTRAINT `FK_demand_forecasts_background_job_runs_job_run_id` FOREIGN KEY (`job_run_id`) REFERENCES `background_job_runs` (`Id`) ON DELETE RESTRICT,
    CONSTRAINT `FK_demand_forecasts_branch_inventories_branch_inventory_id` FOREIGN KEY (`branch_inventory_id`) REFERENCES `branch_inventories` (`id`) ON DELETE RESTRICT
);

CREATE INDEX `IX_demand_forecasts_branch_inventory_id` ON `demand_forecasts` (`branch_inventory_id`);

CREATE UNIQUE INDEX `ix_demand_forecasts_run_inventory_horizon` ON `demand_forecasts` (`job_run_id`, `branch_inventory_id`, `horizon_days`);

INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
VALUES ('20260904151359_AddDemandForecasts', '10.0.9');

ALTER TABLE `background_job_runs` ADD `branch_id` char(36) NULL;

CREATE INDEX `IX_background_job_runs_branch_id` ON `background_job_runs` (`branch_id`);

ALTER TABLE `background_job_runs` ADD CONSTRAINT `FK_background_job_runs_branches_branch_id` FOREIGN KEY (`branch_id`) REFERENCES `branches` (`id`) ON DELETE RESTRICT;

UPDATE background_job_runs SET branch_id = SUBSTRING_INDEX(lock_key, ':', -1) WHERE job_name = 'Forecast' AND lock_key LIKE 'branch:%' AND branch_id IS NULL;

INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
VALUES ('20260904160056_AddBackgroundJobRunBranch', '10.0.9');

INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
VALUES ('20260905081854_SyncModelAndMigrations', '10.0.9');

DROP PROCEDURE IF EXISTS `_sp_restore_recommendation_constraints`;


CREATE PROCEDURE `_sp_restore_recommendation_constraints`()
proc_main: BEGIN
    DECLARE v_table_exists INT DEFAULT 0;
    DECLARE v_violating_count INT DEFAULT 0;
    DECLARE v_rank_exists INT DEFAULT 0;
    DECLARE v_rank_enforced VARCHAR(10) DEFAULT '';
    DECLARE v_rank_clause TEXT DEFAULT '';
    DECLARE v_score_exists INT DEFAULT 0;
    DECLARE v_score_enforced VARCHAR(10) DEFAULT '';
    DECLARE v_score_clause TEXT DEFAULT '';

    -- 1. Kiểm tra bảng recommendation_results có tồn tại không
    SELECT COUNT(*) INTO v_table_exists
    FROM information_schema.TABLES
    WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'recommendation_results';

    IF v_table_exists = 0 THEN
        LEAVE proc_main;
    END IF;

    -- 2. Xử lý constraint ck_recommendation_results_rank
    SELECT COUNT(*) INTO v_violating_count
    FROM `recommendation_results`
    WHERE `rank` <= 0 OR `rank` IS NULL;

    IF v_violating_count > 0 THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'Cannot apply ck_recommendation_results_rank: violating records exist with rank <= 0.';
    END IF;

    SELECT COUNT(*), COALESCE(MAX(ENFORCED), '')
    INTO v_rank_exists, v_rank_enforced
    FROM information_schema.TABLE_CONSTRAINTS
    WHERE TABLE_SCHEMA = DATABASE()
      AND TABLE_NAME = 'recommendation_results'
      AND CONSTRAINT_TYPE = 'CHECK'
      AND CONSTRAINT_NAME = 'ck_recommendation_results_rank';

    IF v_rank_exists = 0 THEN
        ALTER TABLE `recommendation_results`
        ADD CONSTRAINT `ck_recommendation_results_rank` CHECK (`rank` > 0);
    ELSE
        SELECT COALESCE(MAX(CHECK_CLAUSE), '')
        INTO v_rank_clause
        FROM information_schema.CHECK_CONSTRAINTS
        WHERE CONSTRAINT_SCHEMA = DATABASE()
          AND CONSTRAINT_NAME = 'ck_recommendation_results_rank';

        IF v_rank_enforced <> 'YES' OR v_rank_clause NOT LIKE '%rank% > 0%' THEN
            SIGNAL SQLSTATE '45000'
                SET MESSAGE_TEXT = 'Constraint ck_recommendation_results_rank exists but is invalid or not enforced.';
        END IF;
    END IF;

    -- 3. Xử lý constraint ck_recommendation_results_score
    SELECT COUNT(*) INTO v_violating_count
    FROM `recommendation_results`
    WHERE score < 0 OR score > 1 OR score IS NULL;

    IF v_violating_count > 0 THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'Cannot apply ck_recommendation_results_score: violating records exist with score outside [0, 1].';
    END IF;

    SELECT COUNT(*), COALESCE(MAX(ENFORCEd), '')
    INTO v_score_exists, v_score_enforced
    FROM information_schema.TABLE_CONSTRAINTS
    WHERE TABLE_SCHEMA = DATABASE()
      AND TABLE_NAME = 'recommendation_results'
      AND CONSTRAINT_TYPE = 'CHECK'
      AND CONSTRAINT_NAME = 'ck_recommendation_results_score';

    IF v_score_exists = 0 THEN
        ALTER TABLE `recommendation_results`
        ADD CONSTRAINT `ck_recommendation_results_score` CHECK (score + 0 >= 0 AND score + 0 <= 1);
    ELSE
        SELECT COALESCE(MAX(CHECK_CLAUSE), '')
        INTO v_score_clause
        FROM information_schema.CHECK_CONSTRAINTS
        WHERE CONSTRAINT_SCHEMA = DATABASE()
          AND CONSTRAINT_NAME = 'ck_recommendation_results_score';

        IF v_score_enforced <> 'YES' OR v_score_clause NOT LIKE '%score%>= 0%' OR v_score_clause NOT LIKE '%score%<= 1%' THEN
            SIGNAL SQLSTATE '45000'
                SET MESSAGE_TEXT = 'Constraint ck_recommendation_results_score exists but is invalid or not enforced.';
        END IF;
    END IF;

END;

CALL `_sp_restore_recommendation_constraints`();

DROP PROCEDURE IF EXISTS `_sp_restore_recommendation_constraints`;

INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
VALUES ('20260905094527_RestoreRecommendationConstraints', '10.0.9');

COMMIT;

