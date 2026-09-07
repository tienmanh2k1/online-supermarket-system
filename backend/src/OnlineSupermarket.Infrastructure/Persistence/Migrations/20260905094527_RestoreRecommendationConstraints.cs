using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OnlineSupermarket.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RestoreRecommendationConstraints : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP PROCEDURE IF EXISTS `_sp_restore_recommendation_constraints`;");

            migrationBuilder.Sql(@"
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

        IF v_rank_enforced <> 'YES' OR LOWER(TRIM(v_rank_clause)) NOT IN (
            '(`rank` > 0)',
            '(`rank` >= 1)',
            '(0 < `rank`)',
            '(1 <= `rank`)'
        ) THEN
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

        IF v_score_enforced <> 'YES' OR LOWER(TRIM(v_score_clause)) NOT IN (
            '(((`score` + 0) >= 0) and ((`score` + 0) <= 1))',
            '((`score` >= 0) and (`score` <= 1))',
            '(((`score` + 0) <= 1) and ((`score` + 0) >= 0))',
            '((`score` <= 1) and (`score` >= 0))',
            '((0 <= `score`) and (`score` <= 1))',
            '((0 <= (`score` + 0)) and ((`score` + 0) <= 1))'
        ) THEN
            SIGNAL SQLSTATE '45000'
                SET MESSAGE_TEXT = 'Constraint ck_recommendation_results_score exists but is invalid or not enforced.';
        END IF;
    END IF;

END;");

            migrationBuilder.Sql("CALL `_sp_restore_recommendation_constraints`();");
            migrationBuilder.Sql("DROP PROCEDURE IF EXISTS `_sp_restore_recommendation_constraints`;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            throw new NotSupportedException(
                "Rollback of RestoreRecommendationConstraints is blocked to prevent dropping constraints that may have existed prior to upgrade. Restore database from backup if downgrade is required.");
        }
    }
}
