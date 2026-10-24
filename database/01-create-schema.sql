CREATE TABLE IF NOT EXISTS "__EFMigrationsHistory" (
    migration_id character varying(150) NOT NULL,
    product_version character varying(32) NOT NULL,
    CONSTRAINT pk___ef_migrations_history PRIMARY KEY (migration_id)
);

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261015163645_InitialCreate') THEN
    CREATE TYPE account_status AS ENUM ('ACTIVE', 'BLOCKED', 'SUSPENDED');
    CREATE TYPE availability AS ENUM ('IMMEDIATE', 'FIFTEEN_DAYS', 'ONE_MONTH', 'TO_BE_AGREED');
    CREATE TYPE company_status AS ENUM ('PENDING', 'APPROVED', 'REJECTED', 'SUSPENDED');
    CREATE TYPE contract_type AS ENUM ('PERMANENT', 'FIXED_TERM', 'INTERNSHIP', 'SERVICE_PROVISION');
    CREATE TYPE conversation_close_reason AS ENUM ('CLOSED_BY_PARTY', 'ACCOUNT_BLOCKED_OR_SUSPENDED', 'COMPANY_SUSPENDED');
    CREATE TYPE conversation_status AS ENUM ('OPEN', 'CLOSED');
    CREATE TYPE industry AS ENUM ('TECHNOLOGY', 'HEALTHCARE', 'RETAIL', 'HOSPITALITY', 'CONSTRUCTION', 'MANUFACTURING', 'EDUCATION', 'FINANCE', 'LOGISTICS', 'SERVICES', 'OTHER');
    CREATE TYPE job_status AS ENUM ('UNPUBLISHED', 'PUBLISHED', 'SUSPENDED', 'CLOSED');
    CREATE TYPE match_status AS ENUM ('DECLINED_BY_CANDIDATE', 'WAITING', 'MATCHED', 'REJECTED_BY_RECRUITER');
    CREATE TYPE operation_type AS ENUM ('COMPANY_SUBMITTED', 'COMPANY_UPDATED', 'COMPANY_APPROVED', 'COMPANY_REJECTED', 'COMPANY_SUSPENDED', 'COMPANY_REACTIVATED', 'JOB_CREATED', 'JOB_PUBLISHED', 'JOB_UPDATED', 'JOB_SUSPENDED', 'JOB_REPUBLISHED', 'JOB_CLOSED', 'JOB_DELETED', 'ACCOUNT_BLOCKED', 'ACCOUNT_REACTIVATED', 'ACCOUNT_SUSPENDED', 'SKILL_CREATED', 'SKILL_RENAMED', 'BENEFIT_CREATED', 'BENEFIT_RENAMED');
    CREATE TYPE user_type AS ENUM ('CANDIDATE', 'RECRUITER', 'ADMIN');
    CREATE TYPE work_mode AS ENUM ('ON_SITE', 'HYBRID', 'REMOTE');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261015163645_InitialCreate') THEN
    INSERT INTO "__EFMigrationsHistory" (migration_id, product_version)
    VALUES ('20261015163645_InitialCreate', '10.0.13');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261016184300_AddAppUser') THEN
    CREATE TABLE app_user (
        id uuid NOT NULL DEFAULT (gen_random_uuid()),
        email character varying(255) NOT NULL,
        password_hash character varying(255) NOT NULL,
        user_type user_type NOT NULL,
        status account_status NOT NULL DEFAULT 'ACTIVE'::account_status,
        terms_accepted_at timestamp with time zone,
        created_at timestamp with time zone NOT NULL DEFAULT (now()),
        updated_at timestamp with time zone NOT NULL DEFAULT (now()),
        CONSTRAINT pk_app_user PRIMARY KEY (id),
        CONSTRAINT ck_app_user_admin_active CHECK (user_type <> 'ADMIN' OR status = 'ACTIVE'),
        CONSTRAINT ck_app_user_email CHECK (email ~ '^[^@[:space:]]+@[^@[:space:]]+$'),
        CONSTRAINT ck_app_user_suspended CHECK (status <> 'SUSPENDED' OR user_type = 'CANDIDATE'),
        CONSTRAINT ck_app_user_terms CHECK (user_type = 'ADMIN' OR terms_accepted_at IS NOT NULL)
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261016184300_AddAppUser') THEN
    CREATE UNIQUE INDEX uq_app_user_email ON app_user (lower(email));
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261016184300_AddAppUser') THEN
    INSERT INTO "__EFMigrationsHistory" (migration_id, product_version)
    VALUES ('20261016184300_AddAppUser', '10.0.13');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261016184614_AddRecruiter') THEN
    CREATE TABLE recruiter (
        id uuid NOT NULL,
        CONSTRAINT pk_recruiter PRIMARY KEY (id),
        CONSTRAINT fk_recruiter_app_user_id FOREIGN KEY (id) REFERENCES app_user (id)
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261016184614_AddRecruiter') THEN
    INSERT INTO "__EFMigrationsHistory" (migration_id, product_version)
    VALUES ('20261016184614_AddRecruiter', '10.0.13');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261016184923_AddCandidate') THEN
    CREATE TABLE location (
        id uuid NOT NULL DEFAULT (gen_random_uuid()),
        name character varying(100) NOT NULL,
        district character varying(50) NOT NULL,
        latitude numeric(9,6) NOT NULL,
        longitude numeric(9,6) NOT NULL,
        CONSTRAINT pk_location PRIMARY KEY (id),
        CONSTRAINT ck_location_district CHECK (btrim(district) <> ''),
        CONSTRAINT ck_location_latitude CHECK (latitude BETWEEN -90 AND 90),
        CONSTRAINT ck_location_longitude CHECK (longitude BETWEEN -180 AND 180),
        CONSTRAINT ck_location_name CHECK (btrim(name) <> '')
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261016184923_AddCandidate') THEN
    CREATE TABLE candidate (
        id uuid NOT NULL,
        full_name character varying(150) NOT NULL,
        phone_number character varying(9) NOT NULL,
        location_id uuid NOT NULL,
        desired_role character varying(150),
        experience_summary character varying(1000),
        availability availability,
        profile_photo_path character varying(500),
        cv_path character varying(500),
        max_distance_km integer,
        min_salary_expectation numeric(10,2),
        preferred_work_modes work_mode[] NOT NULL DEFAULT ('{}'),
        preferred_contract_types contract_type[] NOT NULL DEFAULT ('{}'),
        swipe_rights_remaining smallint NOT NULL DEFAULT 10,
        next_swipe_available_at timestamp with time zone,
        CONSTRAINT pk_candidate PRIMARY KEY (id),
        CONSTRAINT ck_candidate_full_name CHECK (btrim(full_name) <> ''),
        CONSTRAINT ck_candidate_max_distance_km CHECK (max_distance_km BETWEEN 1 AND 500),
        CONSTRAINT ck_candidate_min_salary_expectation CHECK (min_salary_expectation >= 0),
        CONSTRAINT ck_candidate_phone_number CHECK (phone_number ~ '^[0-9]{9}$'),
        CONSTRAINT ck_candidate_quota_block CHECK (next_swipe_available_at IS NULL OR swipe_rights_remaining = 0),
        CONSTRAINT ck_candidate_swipe_rights_remaining CHECK (swipe_rights_remaining BETWEEN 0 AND 10),
        CONSTRAINT fk_candidate_app_user_id FOREIGN KEY (id) REFERENCES app_user (id),
        CONSTRAINT fk_candidate_location_location_id FOREIGN KEY (location_id) REFERENCES location (id)
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261016184923_AddCandidate') THEN
    CREATE TABLE candidate_link (
        id uuid NOT NULL DEFAULT (gen_random_uuid()),
        candidate_id uuid NOT NULL,
        position smallint NOT NULL,
        url character varying(500) NOT NULL,
        CONSTRAINT pk_candidate_link PRIMARY KEY (id),
        CONSTRAINT ck_candidate_link_position CHECK (position BETWEEN 1 AND 3),
        CONSTRAINT ck_candidate_link_url CHECK (url ~* '^https?://'),
        CONSTRAINT fk_candidate_link_candidate_candidate_id FOREIGN KEY (candidate_id) REFERENCES candidate (id) ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261016184923_AddCandidate') THEN
    CREATE INDEX ix_candidate_location ON candidate (location_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261016184923_AddCandidate') THEN
    CREATE UNIQUE INDEX uq_candidate_link_position ON candidate_link (candidate_id, position);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261016184923_AddCandidate') THEN
    CREATE UNIQUE INDEX uq_candidate_link_url ON candidate_link (candidate_id, url);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261016184923_AddCandidate') THEN
    CREATE UNIQUE INDEX uq_location_name_district ON location (name, district);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261016184923_AddCandidate') THEN
    INSERT INTO "__EFMigrationsHistory" (migration_id, product_version)
    VALUES ('20261016184923_AddCandidate', '10.0.13');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261021200000_AddAdmin') THEN
    CREATE TABLE admin (
        id uuid NOT NULL,
        CONSTRAINT pk_admin PRIMARY KEY (id),
        CONSTRAINT fk_admin_app_user_id FOREIGN KEY (id) REFERENCES app_user (id)
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261021200000_AddAdmin') THEN
    WITH new_user AS ( INSERT INTO app_user (email, password_hash, user_type, status) VALUES ('admin@katch.pt', 'AQAAAAIAAYagAAAAEJuSbUReCi69oI9/4Lx3fcPwDJ4W5y8hNFukNOvLVTaA4hpEWJNfjKPhja8BMy3zkw==', 'ADMIN', 'ACTIVE') RETURNING id) INSERT INTO admin (id) SELECT id FROM new_user;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261021200000_AddAdmin') THEN
    INSERT INTO "__EFMigrationsHistory" (migration_id, product_version)
    VALUES ('20261021200000_AddAdmin', '10.0.13');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261022141118_AddCompany') THEN
    CREATE TABLE company (
        id uuid NOT NULL DEFAULT (gen_random_uuid()),
        recruiter_id uuid NOT NULL,
        company_name character varying(200) NOT NULL,
        tax_id character varying(9) NOT NULL,
        industry industry NOT NULL,
        address character varying(255) NOT NULL,
        location_id uuid NOT NULL,
        contact_email character varying(255) NOT NULL,
        contact_phone character varying(9) NOT NULL,
        responsible_name character varying(150) NOT NULL,
        website character varying(500),
        description character varying(1000),
        logo_path character varying(500),
        photo_paths character varying(500)[] NOT NULL DEFAULT ('{}'),
        status company_status NOT NULL DEFAULT 'PENDING'::company_status,
        status_reason character varying(500),
        submitted_at timestamp with time zone NOT NULL DEFAULT (now()),
        CONSTRAINT pk_company PRIMARY KEY (id),
        CONSTRAINT ck_company_address CHECK (btrim(address) <> ''),
        CONSTRAINT ck_company_company_name CHECK (btrim(company_name) <> ''),
        CONSTRAINT ck_company_contact_email CHECK (contact_email ~ '^[^@[:space:]]+@[^@[:space:]]+$'),
        CONSTRAINT ck_company_contact_phone CHECK (contact_phone ~ '^[0-9]{9}$'),
        CONSTRAINT ck_company_photo_paths CHECK (cardinality(photo_paths) <= 6),
        CONSTRAINT ck_company_reason CHECK ((status = 'REJECTED') = (status_reason IS NOT NULL)),
        CONSTRAINT ck_company_reason_length CHECK (status_reason IS NULL OR char_length(btrim(status_reason)) BETWEEN 10 AND 500),
        CONSTRAINT ck_company_responsible_name CHECK (btrim(responsible_name) <> ''),
        CONSTRAINT ck_company_tax_id CHECK (tax_id ~ '^[0-9]{9}$'),
        CONSTRAINT ck_company_website CHECK (website ~* '^https?://'),
        CONSTRAINT fk_company_location_location_id FOREIGN KEY (location_id) REFERENCES location (id),
        CONSTRAINT fk_company_recruiter_recruiter_id FOREIGN KEY (recruiter_id) REFERENCES recruiter (id)
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261022141118_AddCompany') THEN
    CREATE INDEX ix_company_status ON company (status);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261022141118_AddCompany') THEN
    CREATE UNIQUE INDEX uq_company_recruiter ON company (recruiter_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261022141118_AddCompany') THEN
    CREATE UNIQUE INDEX uq_company_tax_id ON company (tax_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261022141118_AddCompany') THEN
    INSERT INTO "__EFMigrationsHistory" (migration_id, product_version)
    VALUES ('20261022141118_AddCompany', '10.0.13');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261023003000_AddJob') THEN
    CREATE TABLE job (
        id uuid NOT NULL DEFAULT (gen_random_uuid()),
        company_id uuid NOT NULL,
        title character varying(150) NOT NULL,
        description text NOT NULL,
        min_salary numeric(10,2) NOT NULL,
        max_salary numeric(10,2) NOT NULL,
        location_id uuid NOT NULL,
        contract_type contract_type NOT NULL,
        work_mode work_mode NOT NULL,
        is_urgent boolean NOT NULL DEFAULT FALSE,
        photo_paths character varying(500)[] NOT NULL DEFAULT ('{}'),
        expires_at timestamp with time zone,
        status job_status NOT NULL DEFAULT 'UNPUBLISHED'::job_status,
        first_published_at timestamp with time zone,
        published_at timestamp with time zone,
        suspended_at timestamp with time zone,
        deleted_at timestamp with time zone,
        CONSTRAINT pk_job PRIMARY KEY (id),
        CONSTRAINT ck_job_description CHECK (btrim(description) <> ''),
        CONSTRAINT ck_job_min_salary CHECK (min_salary > 0),
        CONSTRAINT ck_job_photo_paths CHECK (cardinality(photo_paths) <= 5),
        CONSTRAINT ck_job_published CHECK (status NOT IN ('PUBLISHED', 'SUSPENDED') OR first_published_at IS NOT NULL),
        CONSTRAINT ck_job_salary CHECK (max_salary >= min_salary),
        CONSTRAINT ck_job_title CHECK (btrim(title) <> ''),
        CONSTRAINT fk_job_company_company_id FOREIGN KEY (company_id) REFERENCES company (id),
        CONSTRAINT fk_job_location_location_id FOREIGN KEY (location_id) REFERENCES location (id)
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261023003000_AddJob') THEN
    CREATE INDEX ix_job_company_status ON job (company_id, status) WHERE deleted_at IS NULL;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261023003000_AddJob') THEN
    CREATE INDEX ix_job_expiry ON job (expires_at) WHERE status = 'PUBLISHED';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261023003000_AddJob') THEN
    CREATE INDEX ix_job_location ON job (location_id) WHERE deleted_at IS NULL;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261023003000_AddJob') THEN
    INSERT INTO "__EFMigrationsHistory" (migration_id, product_version)
    VALUES ('20261023003000_AddJob', '10.0.13');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261023161721_AddMatch') THEN
    CREATE TABLE match (
        id uuid NOT NULL DEFAULT (gen_random_uuid()),
        candidate_id uuid NOT NULL,
        job_id uuid NOT NULL,
        status match_status NOT NULL,
        candidate_action_at timestamp with time zone NOT NULL DEFAULT (now()),
        profile_opened_at timestamp with time zone,
        recruiter_action_at timestamp with time zone,
        decided_by uuid,
        conversation_status conversation_status,
        conversation_closed_at timestamp with time zone,
        conversation_closed_by uuid,
        conversation_close_reason conversation_close_reason,
        CONSTRAINT pk_match PRIMARY KEY (id),
        CONSTRAINT ck_match_closed_by CHECK ((conversation_closed_by IS NOT NULL) = coalesce(conversation_close_reason = 'CLOSED_BY_PARTY', false)),
        CONSTRAINT ck_match_conversation CHECK ((conversation_status IS NOT NULL) = (status = 'MATCHED')),
        CONSTRAINT ck_match_conversation_close CHECK (coalesce(conversation_status = 'CLOSED', false) = (conversation_closed_at IS NOT NULL) AND coalesce(conversation_status = 'CLOSED', false) = (conversation_close_reason IS NOT NULL)),
        CONSTRAINT ck_match_decision CHECK ((status IN ('MATCHED', 'REJECTED_BY_RECRUITER')) = (recruiter_action_at IS NOT NULL) AND (recruiter_action_at IS NOT NULL) = (decided_by IS NOT NULL) AND (recruiter_action_at IS NULL OR profile_opened_at IS NOT NULL)),
        CONSTRAINT ck_match_declined CHECK (status <> 'DECLINED_BY_CANDIDATE' OR (profile_opened_at IS NULL AND recruiter_action_at IS NULL)),
        CONSTRAINT fk_match_app_user_conversation_closed_by FOREIGN KEY (conversation_closed_by) REFERENCES app_user (id),
        CONSTRAINT fk_match_candidate_candidate_id FOREIGN KEY (candidate_id) REFERENCES candidate (id),
        CONSTRAINT fk_match_job_job_id FOREIGN KEY (job_id) REFERENCES job (id),
        CONSTRAINT fk_match_recruiter_decided_by FOREIGN KEY (decided_by) REFERENCES recruiter (id)
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261023161721_AddMatch') THEN
    CREATE INDEX ix_match_interest_at ON match (candidate_action_at) WHERE status <> 'DECLINED_BY_CANDIDATE';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261023161721_AddMatch') THEN
    CREATE INDEX ix_match_job_status ON match (job_id, status);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261023161721_AddMatch') THEN
    CREATE INDEX ix_match_matched ON match (status, recruiter_action_at);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261023161721_AddMatch') THEN
    CREATE UNIQUE INDEX uq_match_candidate_job ON match (candidate_id, job_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261023161721_AddMatch') THEN
    INSERT INTO "__EFMigrationsHistory" (migration_id, product_version)
    VALUES ('20261023161721_AddMatch', '10.0.13');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261023165549_AddSkillsAndBenefits') THEN
    CREATE TABLE benefit (
        id uuid NOT NULL DEFAULT (gen_random_uuid()),
        name character varying(100) NOT NULL,
        CONSTRAINT pk_benefit PRIMARY KEY (id),
        CONSTRAINT ck_benefit_name CHECK (btrim(name) <> '')
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261023165549_AddSkillsAndBenefits') THEN
    CREATE TABLE candidate_custom_skill (
        id uuid NOT NULL DEFAULT (gen_random_uuid()),
        candidate_id uuid NOT NULL,
        label character varying(40) NOT NULL,
        CONSTRAINT pk_candidate_custom_skill PRIMARY KEY (id),
        CONSTRAINT ck_candidate_custom_skill_label CHECK (char_length(label) BETWEEN 2 AND 40 AND btrim(label) <> '' AND label ~ '^[[:alnum:] +#.-]+$'),
        CONSTRAINT fk_candidate_custom_skill_candidate_candidate_id FOREIGN KEY (candidate_id) REFERENCES candidate (id) ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261023165549_AddSkillsAndBenefits') THEN
    CREATE TABLE skill (
        id uuid NOT NULL DEFAULT (gen_random_uuid()),
        name character varying(100) NOT NULL,
        CONSTRAINT pk_skill PRIMARY KEY (id),
        CONSTRAINT ck_skill_name CHECK (btrim(name) <> '')
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261023165549_AddSkillsAndBenefits') THEN
    CREATE TABLE job_benefit (
        job_id uuid NOT NULL,
        benefit_id uuid NOT NULL,
        CONSTRAINT pk_job_benefit PRIMARY KEY (job_id, benefit_id),
        CONSTRAINT fk_job_benefit_benefit_benefit_id FOREIGN KEY (benefit_id) REFERENCES benefit (id),
        CONSTRAINT fk_job_benefit_job_job_id FOREIGN KEY (job_id) REFERENCES job (id) ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261023165549_AddSkillsAndBenefits') THEN
    CREATE TABLE candidate_skill (
        candidate_id uuid NOT NULL,
        skill_id uuid NOT NULL,
        CONSTRAINT pk_candidate_skill PRIMARY KEY (candidate_id, skill_id),
        CONSTRAINT fk_candidate_skill_candidate_candidate_id FOREIGN KEY (candidate_id) REFERENCES candidate (id) ON DELETE CASCADE,
        CONSTRAINT fk_candidate_skill_skill_skill_id FOREIGN KEY (skill_id) REFERENCES skill (id)
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261023165549_AddSkillsAndBenefits') THEN
    CREATE TABLE job_skill (
        job_id uuid NOT NULL,
        skill_id uuid NOT NULL,
        CONSTRAINT pk_job_skill PRIMARY KEY (job_id, skill_id),
        CONSTRAINT fk_job_skill_job_job_id FOREIGN KEY (job_id) REFERENCES job (id) ON DELETE CASCADE,
        CONSTRAINT fk_job_skill_skill_skill_id FOREIGN KEY (skill_id) REFERENCES skill (id)
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261023165549_AddSkillsAndBenefits') THEN
    CREATE UNIQUE INDEX uq_skill_name ON skill (lower(name));
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261023165549_AddSkillsAndBenefits') THEN
    CREATE UNIQUE INDEX uq_benefit_name ON benefit (lower(name));
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261023165549_AddSkillsAndBenefits') THEN
    CREATE UNIQUE INDEX uq_candidate_custom_skill ON candidate_custom_skill (candidate_id, lower(label));
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261023165549_AddSkillsAndBenefits') THEN
    INSERT INTO "__EFMigrationsHistory" (migration_id, product_version)
    VALUES ('20261023165549_AddSkillsAndBenefits', '10.0.13');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261024180929_AddMessageAndOperationLog') THEN
    CREATE TABLE message (
        id uuid NOT NULL DEFAULT (gen_random_uuid()),
        match_id uuid NOT NULL,
        sender_id uuid NOT NULL,
        content character varying(1000) NOT NULL,
        sent_at timestamp with time zone NOT NULL DEFAULT (now()),
        read_at timestamp with time zone,
        CONSTRAINT pk_message PRIMARY KEY (id),
        CONSTRAINT ck_message_content CHECK (btrim(content) <> ''),
        CONSTRAINT ck_message_read CHECK (read_at IS NULL OR read_at >= sent_at),
        CONSTRAINT fk_message_app_user_sender_id FOREIGN KEY (sender_id) REFERENCES app_user (id),
        CONSTRAINT fk_message_match_match_id FOREIGN KEY (match_id) REFERENCES match (id)
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261024180929_AddMessageAndOperationLog') THEN
    CREATE TABLE operation_log (
        id bigint GENERATED ALWAYS AS IDENTITY,
        user_id uuid,
        operation operation_type NOT NULL,
        entity_id uuid NOT NULL,
        occurred_at timestamp with time zone NOT NULL DEFAULT (now()),
        CONSTRAINT pk_operation_log PRIMARY KEY (id),
        CONSTRAINT ck_operation_log_author CHECK (user_id IS NOT NULL OR operation = 'JOB_CLOSED'),
        CONSTRAINT fk_operation_log_app_user_user_id FOREIGN KEY (user_id) REFERENCES app_user (id)
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261024180929_AddMessageAndOperationLog') THEN
    CREATE INDEX ix_message_match_sent ON message (match_id, sent_at);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261024180929_AddMessageAndOperationLog') THEN
    CREATE INDEX ix_message_unread ON message (match_id) WHERE read_at IS NULL;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261024180929_AddMessageAndOperationLog') THEN
    CREATE INDEX ix_operation_log_entity ON operation_log (entity_id, occurred_at);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261024180929_AddMessageAndOperationLog') THEN
    CREATE INDEX ix_operation_log_occurred ON operation_log (operation, occurred_at);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261024180929_AddMessageAndOperationLog') THEN
    INSERT INTO "__EFMigrationsHistory" (migration_id, product_version)
    VALUES ('20261024180929_AddMessageAndOperationLog', '10.0.13');
    END IF;
END $EF$;
COMMIT;

