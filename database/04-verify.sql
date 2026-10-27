-- Verificação do esquema e dos dados iniciais (I099): número de tabelas, ausência da tabela notification,
-- registos por tabela e volumes mínimos do P18. Só lê; não altera a base de dados.

-- 1. Tabelas do esquema (o modelo de dados v02 tem 17; __EFMigrationsHistory é a tabela de controlo do EF Core).
SELECT count(*) AS tabelas_do_modelo
FROM information_schema.tables
WHERE table_schema = 'public' AND table_type = 'BASE TABLE' AND table_name <> '__EFMigrationsHistory';

-- 2. A tabela notification não existe (F011 fora do âmbito): deve devolver false.
SELECT EXISTS (
    SELECT 1 FROM information_schema.tables WHERE table_schema = 'public' AND table_name = 'notification'
) AS existe_notification;

-- 3. Registos por tabela.
SELECT 'location' AS tabela, count(*) AS registos FROM location
UNION ALL SELECT 'app_user', count(*) FROM app_user
UNION ALL SELECT 'admin', count(*) FROM admin
UNION ALL SELECT 'recruiter', count(*) FROM recruiter
UNION ALL SELECT 'candidate', count(*) FROM candidate
UNION ALL SELECT 'candidate_link', count(*) FROM candidate_link
UNION ALL SELECT 'skill', count(*) FROM skill
UNION ALL SELECT 'benefit', count(*) FROM benefit
UNION ALL SELECT 'candidate_skill', count(*) FROM candidate_skill
UNION ALL SELECT 'candidate_custom_skill', count(*) FROM candidate_custom_skill
UNION ALL SELECT 'company', count(*) FROM company
UNION ALL SELECT 'job', count(*) FROM job
UNION ALL SELECT 'job_skill', count(*) FROM job_skill
UNION ALL SELECT 'job_benefit', count(*) FROM job_benefit
UNION ALL SELECT 'match', count(*) FROM match
UNION ALL SELECT 'message', count(*) FROM message
UNION ALL SELECT 'operation_log', count(*) FROM operation_log;

-- 4. Volumes mínimos do P18 (20 Candidatos ativos, 5 Empresas aprovadas, 2 pendentes, 50 vagas publicadas) e
--    conta de Administrador (RF083).
SELECT
    (SELECT count(*) FROM candidate c JOIN app_user u ON u.id = c.id WHERE u.status = 'ACTIVE') AS candidatos_ativos,
    (SELECT count(*) FROM company WHERE status = 'APPROVED') AS empresas_aprovadas,
    (SELECT count(*) FROM company WHERE status = 'PENDING') AS empresas_pendentes,
    (SELECT count(*) FROM job WHERE status = 'PUBLISHED' AND deleted_at IS NULL) AS vagas_publicadas,
    (SELECT count(*) FROM admin) AS administradores;

-- 5. Nenhuma palavra-passe em claro: todos os resumos têm o formato do ASP.NET Core Identity (deve devolver 0).
SELECT count(*) AS resumos_fora_do_formato
FROM app_user
WHERE password_hash !~ '^AQAAAA[A-Za-z0-9+/]{70,}={0,2}$';
