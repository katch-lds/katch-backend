START TRANSACTION;
DO $EF$
BEGIN
    IF EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261024180929_AddMessageAndOperationLog') THEN
    DROP TABLE message;
    END IF;
END $EF$;
DO $EF$
BEGIN
    IF EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261024180929_AddMessageAndOperationLog') THEN
    DROP TABLE operation_log;
    END IF;
END $EF$;
DO $EF$
BEGIN
    IF EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261024180929_AddMessageAndOperationLog') THEN
    DELETE FROM "__EFMigrationsHistory"
    WHERE migration_id = '20261024180929_AddMessageAndOperationLog';
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;
DO $EF$
BEGIN
    IF EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261023165549_AddSkillsAndBenefits') THEN
    DROP INDEX uq_candidate_custom_skill;
    END IF;
END $EF$;
DO $EF$
BEGIN
    IF EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261023165549_AddSkillsAndBenefits') THEN
    DROP INDEX uq_benefit_name;
    END IF;
END $EF$;
DO $EF$
BEGIN
    IF EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261023165549_AddSkillsAndBenefits') THEN
    DROP INDEX uq_skill_name;
    END IF;
END $EF$;
DO $EF$
BEGIN
    IF EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261023165549_AddSkillsAndBenefits') THEN
    DROP TABLE candidate_custom_skill;
    END IF;
END $EF$;
DO $EF$
BEGIN
    IF EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261023165549_AddSkillsAndBenefits') THEN
    DROP TABLE candidate_skill;
    END IF;
END $EF$;
DO $EF$
BEGIN
    IF EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261023165549_AddSkillsAndBenefits') THEN
    DROP TABLE job_benefit;
    END IF;
END $EF$;
DO $EF$
BEGIN
    IF EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261023165549_AddSkillsAndBenefits') THEN
    DROP TABLE job_skill;
    END IF;
END $EF$;
DO $EF$
BEGIN
    IF EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261023165549_AddSkillsAndBenefits') THEN
    DROP TABLE benefit;
    END IF;
END $EF$;
DO $EF$
BEGIN
    IF EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261023165549_AddSkillsAndBenefits') THEN
    DROP TABLE skill;
    END IF;
END $EF$;
DO $EF$
BEGIN
    IF EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261023165549_AddSkillsAndBenefits') THEN
    DELETE FROM "__EFMigrationsHistory"
    WHERE migration_id = '20261023165549_AddSkillsAndBenefits';
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;
DO $EF$
BEGIN
    IF EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261023161721_AddMatch') THEN
    DROP TABLE match;
    END IF;
END $EF$;
DO $EF$
BEGIN
    IF EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261023161721_AddMatch') THEN
    DELETE FROM "__EFMigrationsHistory"
    WHERE migration_id = '20261023161721_AddMatch';
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;
DO $EF$
BEGIN
    IF EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261023003000_AddJob') THEN
    DROP TABLE job;
    END IF;
END $EF$;
DO $EF$
BEGIN
    IF EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261023003000_AddJob') THEN
    DELETE FROM "__EFMigrationsHistory"
    WHERE migration_id = '20261023003000_AddJob';
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;
DO $EF$
BEGIN
    IF EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261022141118_AddCompany') THEN
    DROP TABLE company;
    END IF;
END $EF$;
DO $EF$
BEGIN
    IF EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261022141118_AddCompany') THEN
    DELETE FROM "__EFMigrationsHistory"
    WHERE migration_id = '20261022141118_AddCompany';
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;
DO $EF$
BEGIN
    IF EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261021200000_AddAdmin') THEN
    DROP TABLE admin;
    END IF;
END $EF$;
DO $EF$
BEGIN
    IF EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261021200000_AddAdmin') THEN
    DELETE FROM app_user WHERE user_type = 'ADMIN';
    END IF;
END $EF$;
DO $EF$
BEGIN
    IF EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261021200000_AddAdmin') THEN
    DELETE FROM "__EFMigrationsHistory"
    WHERE migration_id = '20261021200000_AddAdmin';
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;
DO $EF$
BEGIN
    IF EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261016184923_AddCandidate') THEN
    DROP TABLE candidate_link;
    END IF;
END $EF$;
DO $EF$
BEGIN
    IF EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261016184923_AddCandidate') THEN
    DROP TABLE candidate;
    END IF;
END $EF$;
DO $EF$
BEGIN
    IF EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261016184923_AddCandidate') THEN
    DROP TABLE location;
    END IF;
END $EF$;
DO $EF$
BEGIN
    IF EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261016184923_AddCandidate') THEN
    DELETE FROM "__EFMigrationsHistory"
    WHERE migration_id = '20261016184923_AddCandidate';
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;
DO $EF$
BEGIN
    IF EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261016184614_AddRecruiter') THEN
    DROP TABLE recruiter;
    END IF;
END $EF$;
DO $EF$
BEGIN
    IF EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261016184614_AddRecruiter') THEN
    DELETE FROM "__EFMigrationsHistory"
    WHERE migration_id = '20261016184614_AddRecruiter';
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;
DO $EF$
BEGIN
    IF EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261016184300_AddAppUser') THEN
    DROP INDEX uq_app_user_email;
    END IF;
END $EF$;
DO $EF$
BEGIN
    IF EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261016184300_AddAppUser') THEN
    DROP TABLE app_user;
    END IF;
END $EF$;
DO $EF$
BEGIN
    IF EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261016184300_AddAppUser') THEN
    DELETE FROM "__EFMigrationsHistory"
    WHERE migration_id = '20261016184300_AddAppUser';
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;
DO $EF$
BEGIN
    IF EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261015163645_InitialCreate') THEN
    DROP TYPE account_status;
    DROP TYPE availability;
    DROP TYPE company_status;
    DROP TYPE contract_type;
    DROP TYPE conversation_close_reason;
    DROP TYPE conversation_status;
    DROP TYPE industry;
    DROP TYPE job_status;
    DROP TYPE match_status;
    DROP TYPE operation_type;
    DROP TYPE user_type;
    DROP TYPE work_mode;
    END IF;
END $EF$;
DO $EF$
BEGIN
    IF EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261015163645_InitialCreate') THEN
    DELETE FROM "__EFMigrationsHistory"
    WHERE migration_id = '20261015163645_InitialCreate';
    END IF;
END $EF$;
COMMIT;

